#include "pch.h"
#include "event_dispatcher.h"
#include "callback_server.h"
#include "request_session_manager.h"
#include "result_parser.h"
#include "image_saver.h"
#include "logger.h"
#include "hzsjkjt_context.h"
#include "path_helper.h"
#include "json_helper.h"

namespace HZCYKJTHardWare {

static intptr_t GetIntPtrValue(const std::string& json, const std::string& key) {
    std::string text = JsonHelper::GetString(json, key);
    if (!text.empty()) {
        return static_cast<intptr_t>(_strtoi64(text.c_str(), nullptr, 10));
    }

    std::string searchKey = "\"" + key + "\"";
    size_t pos = json.find(searchKey);
    if (pos == std::string::npos) return 0;
    pos = json.find(':', pos + searchKey.size());
    if (pos == std::string::npos) return 0;
    pos++;
    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n')) {
        pos++;
    }
    if (pos < json.size() && json[pos] == '"') {
        pos++;
    }

    std::string number;
    while (pos < json.size() && (isdigit(static_cast<unsigned char>(json[pos])) || json[pos] == '-')) {
        number += json[pos];
        pos++;
    }
    if (number.empty()) return 0;
    return static_cast<intptr_t>(_strtoi64(number.c_str(), nullptr, 10));
}

static bool IsDelphiErrorResponse(const std::string& body, std::string& errorCode, std::string& errorMsg) {
    if (!JsonHelper::GetBool(body, "error", false)) {
        return false;
    }
    errorCode = JsonHelper::GetString(body, "code");
    errorMsg = JsonHelper::GetString(body, "message");
    return true;
}

static std::string GetOcrLampFileName(const EvidenceImage& img) {
    if (img.lamp_type == "1") return "可见光";
    if (img.lamp_type == "2") return "红外光";
    if (img.lamp_type == "3") return "紫外光";
    return "";
}

static bool IsOcrPortraitImage(const EvidenceImage& img) {
    return img.image_type == "2";
}

namespace { EventDispatcher* g_pEventDisp = nullptr; }

EventDispatcher& EventDispatcher::Instance() {
    if (!g_pEventDisp) g_pEventDisp = new EventDispatcher();
    return *g_pEventDisp;
}

EventDispatcher::EventDispatcher() { InitializeCriticalSection(&m_cs); }
EventDispatcher::~EventDispatcher() { DeleteCriticalSection(&m_cs); }

void EventDispatcher::Start() {
    if (m_running) return;
    m_running = true;
    m_thread = std::make_unique<std::thread>(&EventDispatcher::WorkerLoop, this);
    LOG_DEBUG("EventDispatcher", "事件分发线程已启动");
}

void EventDispatcher::Stop() {
    m_running = false;
    WakeAllConditionVariable(&m_cv);
    if (m_thread && m_thread->joinable()) {
        m_thread->join();
    }
    m_thread.reset();
    LOG_DEBUG("EventDispatcher", "事件分发线程已停止");
}

void EventDispatcher::SetCallback(THZCYKJTHardWareEventCallback callback) {
    EnterCriticalSection(&m_cs);
    m_callback = callback;
    LeaveCriticalSection(&m_cs);
    LOG_INFO("EventDispatcher", "第三方事件回调已注册：callback=%p", callback);
}

void EventDispatcher::PostEvent(const HZCYKJTHardWare_EVENT& event) {
    EnterCriticalSection(&m_cs);
    EventStrings strs;
    strs.request_id = event.request_id ? event.request_id : "";
    strs.resource_type = event.resource_type ? event.resource_type : "";
    strs.error_code = event.error_code ? event.error_code : "";
    strs.message = event.message ? event.message : "";
    strs.terminal_base_url = event.terminal_base_url ? event.terminal_base_url : "";
    strs.save_path = event.save_path ? event.save_path : "";
    strs.raw_json = event.raw_json ? event.raw_json : "";
    strs.ic_number = event.ic_number ? event.ic_number : "";
    strs.mrz = event.mrz ? event.mrz : "";
    if (event.data && event.data_size > 0) {
        const char* dataBegin = static_cast<const char*>(event.data);
        strs.data.assign(dataBegin, dataBegin + event.data_size);
    }

    m_stringsQueue.push(std::move(strs));
    m_queue.push(event);
    WakeConditionVariable(&m_cv);
    LeaveCriticalSection(&m_cs);
}

// 处理回调服务器的数据并转化为事件
void EventDispatcher::PostCallbackData(const CallbackData& cbData) {
    if (!m_running) {
        LOG_WARN("EventDispatcher", "回调分发未运行，已丢弃Delphi程序回调：path=%s", cbData.path.c_str());
        return;
    }

    // 投递到 worker 线程处理，不阻塞 HTTP 线程
    EnterCriticalSection(&m_cs);
    m_pendingCallbacks.push(cbData);
    LeaveCriticalSection(&m_cs);
    LOG_DEBUG("EventDispatcher", "Delphi程序回调已投递到事件队列：path=%s，body_size=%zu",
             cbData.path.c_str(), cbData.body.size());
    WakeConditionVariable(&m_cv);
}

void EventDispatcher::ProcessCallback(const CallbackData& cbData) {
    std::string path = cbData.path;
    std::string body = cbData.body;

    LOG_DEBUG("EventDispatcher", "开始处理Delphi程序回调：path=%s，body_size=%zu", path.c_str(), body.size());

    std::string resourceType;
    bool isPreviewReady = false;
    if (path.find("/preview-ready") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_FACE_IMAGE;
        isPreviewReady = true;
    } else if (path.find("/ocr") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT;
    } else if (path.find("/iris") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_IRIS_IMAGE;
    } else if (path.find("/nfc-card") != std::string::npos || path.find("/nfc") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_NFC_CARD;
    } else {
        LOG_WARN("EventDispatcher", "收到未知Delphi程序回调路径：path=%s", path.c_str());
        return;
    }

    std::string requestId = JsonHelper::GetString(body, "request_id");
    if (requestId.empty()) {
        LOG_WARN("EventDispatcher", "收到Delphi程序回调但缺少request_id：path=%s", path.c_str());
        return;
    }

    if (isPreviewReady) {
        ProcessPreviewReadyCallback(requestId, body);
        return;
    }

    auto& sessionMgr = RequestSessionManager::Instance();
    auto session = sessionMgr.GetSession(requestId);
    if (!session) {
        bool processActive = false;
        {
            auto& ctx = HzsjkjtContext::Instance();
            ContextLock lock(&ctx.mutex);
            processActive = ctx.process_active;
        }

        if (processActive &&
            (resourceType == HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT ||
             resourceType == HZCYKJTHardWare_RESOURCE_NFC_CARD)) {
            std::string errorCode;
            std::string errorMsg;
            bool isError = IsDelphiErrorResponse(body, errorCode, errorMsg);

            LOG_INFO("EventDispatcher",
                     "按流程级回调处理Delphi程序结果：request_id=%s，resource=%s，path=%s",
                     requestId.c_str(), resourceType.c_str(), path.c_str());

            if (resourceType == HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT) {
                if (isError) {
                    SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_OCR_FAILED,
                              HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                              nullptr, body.c_str());
                } else {
                    std::string mrz = JsonHelper::GetString(body, "mrz");
                    std::string savePath = JsonHelper::GetString(body, "save_path");
                    SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_OCR_SUCCESS,
                              HZCYKJTHardWare_RET_OK, "", "OCR completed successfully",
                              savePath.empty() ? nullptr : savePath.c_str(), body.c_str(),
                              nullptr, mrz.c_str());
                }
                return;
            }

            if (resourceType == HZCYKJTHardWare_RESOURCE_NFC_CARD) {
                if (isError) {
                    SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                              HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                              nullptr, body.c_str());
                } else {
                    std::string cardText = JsonHelper::GetString(body, "card_text");
                    if (cardText.empty()) {
                        cardText = JsonHelper::GetString(body, "ic_number");
                    }
                    if (cardText.empty()) {
                        SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "",
                                  "NFC callback missing card_text", nullptr, body.c_str());
                    } else {
                        SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_NFC_CARD_SUCCESS,
                                  HZCYKJTHardWare_RET_OK, "", "", nullptr, body.c_str(),
                                  cardText.c_str());
                    }
                }
                return;
            }
        }

        LOG_WARN("EventDispatcher", "收到Delphi程序回调但请求不存在：request_id=%s，path=%s",
                 requestId.c_str(), path.c_str());
        return;
    }

    if (session->status == RequestStatus::Expired ||
        session->status == RequestStatus::Cancelled ||
        session->status == RequestStatus::Timeout) {
        LOG_DEBUG("EventDispatcher", "Delphi程序回调已忽略：request_id=%s，status=%d",
                  requestId.c_str(), static_cast<int>(session->status));
        return;
    }

    sessionMgr.MarkCallbackReceived(requestId, body);

    std::string errorCode;
    std::string errorMsg;
    bool isError = IsDelphiErrorResponse(body, errorCode, errorMsg);

    if (resourceType == HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_OCR_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                      nullptr, body.c_str());
        } else {
            ProcessOcrCallback(requestId, body, *session);
        }
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                      nullptr, body.c_str());
        } else {
            ProcessIrisCallback(requestId, body, *session);
        }
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_NFC_CARD) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                      nullptr, body.c_str());
        } else {
            ProcessNfcCardCallback(requestId, body, *session);
        }
    }
}

#if 0
void EventDispatcher::ProcessCallbackLegacy(const CallbackData& cbData) {
    std::string path = cbData.path;
    std::string body = cbData.body;

    LOG_DEBUG("EventDispatcher", "开始处理终端回调：path=%s，body_size=%zu", path.c_str(), body.size());

    // 根据路径判断资源类型
    std::string resourceType;
    if (path.find("/face") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_FACE_IMAGE;
    } else if (path.find("/fingerprint") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE;
    } else if (path.find("/ocr") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT;
    } else if (path.find("/iris") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_IRIS_IMAGE;
    } else if (path.find("/nfc-card") != std::string::npos || path.find("/nfc") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_NFC_CARD;
    } else {
        LOG_WARN("EventDispatcher", "收到未知终端回调路径：path=%s", path.c_str());
        return;
    }

    // 提取 request_id
    std::string requestId = JsonHelper::GetString(body, "request_id");
    if (requestId.empty()) {
        LOG_WARN("EventDispatcher", "收到终端回调但缺少 request_id");
        return;
    }

    // 查找请求会话
    auto& sessionMgr = RequestSessionManager::Instance();
    auto session = sessionMgr.GetSession(requestId);
    if (!session) {
        LOG_WARN("EventDispatcher", "收到终端回调但请求已不存在：request_id=%s", requestId.c_str());
        return;
    }

    // 检查会话状态
    if (session->status == RequestStatus::Expired) {
        LOG_DEBUG("EventDispatcher", "Request %s is expired (terminal switched), callback ignored",
                 requestId.c_str());
        return;
    }
    if (session->status == RequestStatus::Cancelled) {
        LOG_DEBUG("EventDispatcher", "Request %s is cancelled (process ended), callback ignored",
                 requestId.c_str());
        return;
    }
    if (session->status == RequestStatus::Timeout) {
        LOG_DEBUG("EventDispatcher", "Request %s is timed out, callback ignored", requestId.c_str());
        return;
    }

    // 标记收到回调
    sessionMgr.MarkCallbackReceived(requestId, body);

    // 检查是否为错误响应
    std::string errorCode, errorMsg;
    bool isError = ResultParser::IsErrorResponse(body, errorCode, errorMsg);

    // 根据资源类型处理
    if (resourceType == HZCYKJTHardWare_RESOURCE_FACE_IMAGE) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_FACE_CAPTURE_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str());
        } else {
            ProcessFaceCallback(requestId, body, *session);
        }
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str());
        } else {
            ProcessFingerprintCallback(requestId, body, *session);
        }
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_OCR_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str());
        } else {
            ProcessOcrCallback(requestId, body, *session);
        }
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                      nullptr, body.c_str());
        } else {
            ProcessIrisCallback(requestId, body, *session);
        }
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_NFC_CARD) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                      nullptr, body.c_str());
        } else {
            ProcessNfcCardCallback(requestId, body, *session);
        }
    }
}

#endif

void EventDispatcher::ProcessFaceCallback(const std::string& requestId,
                                           const std::string& body,
                                           const RequestSession& session) {
    auto faceResult = ResultParser::ParseFaceResult(body);
    if (!faceResult.valid) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FACE_IMAGE, HZCYKJTHardWare_EVENT_FACE_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "Failed to parse face result");
        return;
    }

    // 确定保存路径
    auto& ctx = HzsjkjtContext::Instance();
    std::string saveDir = session.save_dir;
    if (saveDir.empty()) {
        saveDir = ctx.runtime_save_path;
    }
    if (saveDir.empty()) {
        saveDir = ctx.save_default_root;
    }

    std::string savePath = ImageSaver::BuildSavePath(
        saveDir, requestId, ctx.save_create_date_folder, ctx.save_create_request_folder);

    // 保存图片
    std::string mimeType = faceResult.image_mime_type.empty() ? "image/bmp" : faceResult.image_mime_type;
    std::string ext = ImageSaver::GetExtensionFromMimeType(mimeType);
    std::string fileName = "face_" + requestId + ext;

    std::string fullPath;
    int saveRet = ImageSaver::SaveBase64Image(savePath, "face_capture", faceResult.image_base64,
                                               mimeType, fullPath);
    if (saveRet != HZCYKJTHardWare_RET_OK) {
        LOG_ERROR("EventDispatcher", "人脸回调处理失败：保存图片失败，request_id=%s，path=%s", requestId.c_str(), fullPath.c_str());
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FACE_IMAGE, HZCYKJTHardWare_EVENT_FACE_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_SAVE_FILE_FAILED, "", "Failed to save face image");
        return;
    }

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FACE_IMAGE, HZCYKJTHardWare_EVENT_FACE_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "Face captured successfully", fullPath.c_str(), body.c_str());
}

void EventDispatcher::ProcessFingerprintCallback(const std::string& requestId,
                                                  const std::string& body,
                                                  const RequestSession& session) {
    auto fpResult = ResultParser::ParseFingerprintResult(body);
    if (!fpResult.valid) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE, HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "Failed to parse fingerprint result");
        return;
    }

    auto& ctx = HzsjkjtContext::Instance();
    std::string saveDir = session.save_dir;
    if (saveDir.empty()) {
        saveDir = ctx.runtime_save_path;
    }
    if (saveDir.empty()) {
        saveDir = ctx.save_default_root;
    }

    std::string savePath = ImageSaver::BuildSavePath(
        saveDir, requestId, ctx.save_create_date_folder, ctx.save_create_request_folder);

    std::string mimeType = fpResult.image_mime_type.empty() ? "image/bmp" : fpResult.image_mime_type;
    std::string fullPath;
    int saveRet = ImageSaver::SaveBase64Image(savePath, "fingerprint_capture", fpResult.image_base64,
                                               mimeType, fullPath);
    if (saveRet != HZCYKJTHardWare_RET_OK) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE, HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_SAVE_FILE_FAILED, "", "Failed to save fingerprint image");
        return;
    }

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE, HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "Fingerprint captured successfully", fullPath.c_str(), body.c_str());
}

void EventDispatcher::ProcessOcrCallback(const std::string& requestId,
                                          const std::string& body,
                                          const RequestSession& session) {
    std::string mrz = JsonHelper::GetString(body, "mrz");
    std::string savePath = JsonHelper::GetString(body, "save_path");
    if (savePath.empty()) {
        savePath = session.save_dir;
    }

    LOG_INFO("EventDispatcher", "Delphi程序OCR回调处理完成：request_id=%s，save_path=%s，mrz=%s",
             requestId.c_str(), savePath.c_str(), mrz.c_str());
    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, HZCYKJTHardWare_EVENT_OCR_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "OCR completed successfully",
              savePath.c_str(), body.c_str(), nullptr, mrz.c_str());
}

#if 0
void EventDispatcher::ProcessOcrCallbackLegacy(const std::string& requestId,
                                               const std::string& body,
                                               const RequestSession& session) {
    auto ocrResult = ResultParser::ParseOcrResult(body);
    if (!ocrResult.valid) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, HZCYKJTHardWare_EVENT_OCR_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, ocrResult.error_code.c_str(),
                  ocrResult.message.c_str());
        return;
    }

    auto& ctx2 = HzsjkjtContext::Instance();
    std::string saveDir = session.save_dir;
    if (saveDir.empty()) {
        saveDir = ctx2.runtime_save_path;
    }
    if (saveDir.empty()) {
        saveDir = ctx2.save_default_root;
    }

    std::string savePath = ImageSaver::BuildSavePath(
        saveDir, requestId, ctx2.save_create_date_folder, ctx2.save_create_request_folder);

    // 保存 OCR.json
    std::string ocrJsonPath;
    ImageSaver::SaveJsonFile(savePath, "OCR", body, ocrJsonPath);

    // 保存 person_info.json
    if (!ocrResult.person_info_json.empty()) {
        std::string personPath;
        ImageSaver::SaveJsonFile(savePath, "person_info", ocrResult.person_info_json, personPath);
    }

    // 保存证据图片，统一转码为 JPEG
    bool savedVisible = false;
    bool savedInfrared = false;
    bool savedUltraviolet = false;
    bool savedPortrait = false;
    for (size_t i = 0; i < ocrResult.evidence_images.size(); i++) {
        const auto& img = ocrResult.evidence_images[i];
        std::string lampName = GetOcrLampFileName(img);
        if (!lampName.empty()) {
            bool* savedFlag = nullptr;
            if (lampName == "可见光") savedFlag = &savedVisible;
            else if (lampName == "红外光") savedFlag = &savedInfrared;
            else if (lampName == "紫外光") savedFlag = &savedUltraviolet;

            if (!savedFlag || !*savedFlag) {
                std::string imgPath;
                int saveRet = ImageSaver::SaveBase64ImageAsJpeg(savePath, lampName, img.image_data, imgPath);
                if (saveRet == HZCYKJTHardWare_RET_OK && savedFlag) {
                    *savedFlag = true;
                } else if (saveRet != HZCYKJTHardWare_RET_OK) {
                    LOG_ERROR("EventDispatcher", "OCR回调处理失败：保存证据图片失败，name=%s，ret=%d",
                              lampName.c_str(), saveRet);
                }
            }
        }

        if (!savedPortrait && IsOcrPortraitImage(img)) {
            std::string imgPath;
            int saveRet = ImageSaver::SaveBase64ImageAsJpeg(savePath, "人像", img.image_data, imgPath);
            if (saveRet == HZCYKJTHardWare_RET_OK) {
                savedPortrait = true;
            } else {
                LOG_ERROR("EventDispatcher", "OCR回调处理失败：保存证件人像失败，ret=%d", saveRet);
            }
        }
    }

    LOG_INFO("EventDispatcher", "OCR回调处理完成：request_id=%s，save=%s，mrz=%s",
             requestId.c_str(), savePath.c_str(), ocrResult.mrz.c_str());
    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, HZCYKJTHardWare_EVENT_OCR_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "OCR completed successfully", savePath.c_str(), body.c_str(),
              nullptr, ocrResult.mrz.c_str());
}

#endif

void EventDispatcher::ProcessIrisCallback(const std::string& requestId,
                                           const std::string& body,
                                           const RequestSession& session) {
    std::string savePath = JsonHelper::GetString(body, "save_path");
    if (savePath.empty()) {
        savePath = session.save_dir;
    }

    LOG_INFO("EventDispatcher", "Delphi程序虹膜回调处理完成：request_id=%s，save_path=%s",
             requestId.c_str(), savePath.c_str());
    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "Iris captured successfully",
              savePath.c_str(), body.c_str());
}

#if 0
void EventDispatcher::ProcessIrisCallbackLegacy(const std::string& requestId,
                                                const std::string& body,
                                                const RequestSession& session) {
    auto irisResult = ResultParser::ParseIrisResult(body);
    if (!irisResult.valid) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "Failed to parse iris result",
                  nullptr, body.c_str());
        return;
    }

    auto& ctx = HzsjkjtContext::Instance();
    std::string saveDir = session.save_dir;
    if (saveDir.empty()) {
        saveDir = ctx.runtime_save_path;
    }
    if (saveDir.empty()) {
        saveDir = ctx.save_default_root;
    }

    std::string savePath = ImageSaver::BuildSavePath(
        saveDir, requestId, ctx.save_create_date_folder, ctx.save_create_request_folder);

    std::string leftPath;
    std::string rightPath;
    int savedCount = 0;
    if (!irisResult.left_iris_base64.empty()) {
        int saveRet = ImageSaver::SaveBase64ImageAsJpeg(
            savePath, "iris_left", irisResult.left_iris_base64, leftPath);
        if (saveRet != HZCYKJTHardWare_RET_OK) {
            SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                      saveRet, "", "Failed to save left iris image", nullptr, body.c_str());
            return;
        }
        savedCount++;
    }
    if (!irisResult.right_iris_base64.empty()) {
        int saveRet = ImageSaver::SaveBase64ImageAsJpeg(
            savePath, "iris_right", irisResult.right_iris_base64, rightPath);
        if (saveRet != HZCYKJTHardWare_RET_OK) {
            SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                      saveRet, "", "Failed to save right iris image", nullptr, body.c_str());
            return;
        }
        savedCount++;
    }

    if (savedCount == 0) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_BASE64_FAILED, "", "No iris image saved", nullptr, body.c_str());
        return;
    }

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "Iris captured successfully", savePath.c_str(), body.c_str());
}

#endif

void EventDispatcher::ProcessNfcCardCallback(const std::string& requestId,
                                              const std::string& body,
                                              const RequestSession& session) {
    std::string cardText = JsonHelper::GetString(body, "card_text");
    if (cardText.empty()) {
        cardText = JsonHelper::GetString(body, "ic_number");
    }
    if (cardText.empty()) {
        LOG_ERROR("NFC", "Delphi程序IC卡回调缺少card_text：request_id=%s，body=%s",
                  requestId.c_str(), body.c_str());
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "NFC callback missing card_text",
                  nullptr, body.c_str());
        return;
    }

    LOG_INFO("NFC", "Delphi程序IC卡回调处理完成：request_id=%s，card_text=%s",
             requestId.c_str(), cardText.c_str());
    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "", nullptr, body.c_str(), cardText.c_str());
}

#if 0
void EventDispatcher::ProcessNfcCardCallbackLegacy(const std::string& requestId,
                                                   const std::string& body,
                                                   const RequestSession& session) {
    LOG_DEBUG("NFC", "收到终端IC卡识别回调：request_id=%s",
             requestId.c_str());
    LOG_DEBUG("NFC", "IC卡识别回调原始JSON：request_id=%s，raw_json=%s",
             requestId.c_str(), body.c_str());

    auto nfcResult = ResultParser::ParseNfcCardResult(body);
    if (!nfcResult.valid) {
        LOG_ERROR("NFC", "IC卡识别回调解析失败：request_id=%s", requestId.c_str());
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "Failed to parse NFC card result",
                  nullptr, body.c_str());
        return;
    }

    LOG_INFO("NFC", "IC卡识别回调成功：request_id=%s，ic_number=%s",
             requestId.c_str(), nfcResult.ic_number.c_str());

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "", nullptr, body.c_str(), nfcResult.ic_number.c_str());
}

#endif

void EventDispatcher::ProcessPreviewReadyCallback(const std::string& requestId,
                                                  const std::string& body) {
    intptr_t vlcHwnd = GetIntPtrValue(body, "vlc_hwnd");
    intptr_t delphiHostHwnd = GetIntPtrValue(body, "delphi_host_hwnd");
    std::string resourceType = JsonHelper::GetString(body, "resource_type");
    if (resourceType.empty()) {
        resourceType = HZCYKJTHardWare_RESOURCE_FACE_IMAGE;
    }

    auto& ctx = HzsjkjtContext::Instance();
    intptr_t thirdPartyHwndValue = 0;
    {
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id != requestId) {
            LOG_WARN("EventDispatcher", "Delphi程序预览就绪回调request_id不匹配：callback=%s，current=%s",
                     requestId.c_str(), ctx.camera_preview_request_id.c_str());
            return;
        }
        thirdPartyHwndValue = ctx.camera_preview_third_party_hwnd;
        ctx.camera_preview_vlc_hwnd = vlcHwnd;
        if (delphiHostHwnd != 0) {
            ctx.camera_preview_delphi_host_hwnd = delphiHostHwnd;
        }
    }

    HWND vlcWindow = reinterpret_cast<HWND>(vlcHwnd);
    HWND thirdPartyWindow = reinterpret_cast<HWND>(thirdPartyHwndValue);

    if (vlcHwnd == 0 || !IsWindow(vlcWindow)) {
        LOG_ERROR("EventDispatcher", "Delphi程序预览就绪回调处理失败：vlc_hwnd无效，request_id=%s，vlc_hwnd=%p",
                  requestId.c_str(), vlcWindow);
        SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED,
                  HZCYKJTHardWare_RET_INVALID_HWND, "", "Invalid VLC preview hwnd",
                  nullptr, body.c_str());
        return;
    }

    if (thirdPartyHwndValue == 0 || !IsWindow(thirdPartyWindow)) {
        LOG_ERROR("EventDispatcher", "Delphi程序预览就绪回调处理失败：第三方HWND无效，request_id=%s，third_party_hwnd=%p，vlc_hwnd=%p",
                  requestId.c_str(), thirdPartyWindow, vlcWindow);
        SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED,
                  HZCYKJTHardWare_RET_INVALID_HWND, "", "Invalid third-party preview hwnd",
                  nullptr, body.c_str());
        return;
    }

    SetLastError(0);
    HWND oldParent = SetParent(vlcWindow, thirdPartyWindow);
    DWORD setParentError = GetLastError();

    RECT rc = {0};
    SetLastError(0);
    BOOL gotRect = GetClientRect(thirdPartyWindow, &rc);
    DWORD getRectError = GetLastError();

    BOOL moved = FALSE;
    DWORD moveError = 0;
    if (gotRect) {
        SetLastError(0);
        moved = MoveWindow(vlcWindow, 0, 0, rc.right - rc.left, rc.bottom - rc.top, TRUE);
        moveError = GetLastError();
    }

    LOG_INFO("EventDispatcher", "Delphi程序预览就绪并嵌入第三方窗口：request_id=%s，resource=%s，third_party_hwnd=%p，vlc_hwnd=%p，delphi_host_hwnd=%p，old_parent=%p，set_parent_error=%lu，get_rect=%d，get_rect_error=%lu，move=%d，move_error=%lu，width=%ld，height=%ld",
             requestId.c_str(), resourceType.c_str(), thirdPartyWindow, vlcWindow,
             reinterpret_cast<void*>(delphiHostHwnd), oldParent, setParentError,
             gotRect, getRectError, moved, moveError,
             gotRect ? (rc.right - rc.left) : 0,
             gotRect ? (rc.bottom - rc.top) : 0);

    if (setParentError != 0 || !gotRect || !moved) {
        SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED,
                  HZCYKJTHardWare_RET_PREVIEW_RENDER_FAILED, "", "Failed to move preview window",
                  nullptr, body.c_str());
        return;
    }

    SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STARTED,
              HZCYKJTHardWare_RET_OK, "", "Camera preview ready",
              nullptr, body.c_str());
}

void EventDispatcher::SendEvent(const std::string& requestId,
                                 const std::string& resourceType,
                                 int eventType,
                                 int status,
                                 const char* errorCode,
                                 const char* message,
                                 const char* savePath,
                                 const char* rawJson,
                                 const char* icNumber,
                                 const char* mrz) {
    auto& ctx = HzsjkjtContext::Instance();

    HZCYKJTHardWare_EVENT event;
    memset(&event, 0, sizeof(event));
    event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
    event.event_type = eventType;
    event.request_id = requestId.c_str();
    event.resource_type = resourceType.c_str();
    event.status = status;
    event.error_code = errorCode;
    event.message = message;
    event.terminal_base_url = ctx.current_terminal_base_url.c_str();
    event.terminal_index = ctx.current_terminal_index;
    event.save_path = savePath;
    event.raw_json = rawJson;
    event.data = nullptr;
    event.data_size = 0;
    event.ic_number = icNumber;
    event.mrz = mrz;

    PostEvent(event);
}

void EventDispatcher::WorkerLoop() {
LOG_DEBUG("EventDispatcher", "第三方回调分发线程已启动");

    while (m_running) {
        bool hasWork = false;

        {
            EnterCriticalSection(&m_cs);
            while (m_queue.empty() && m_pendingCallbacks.empty() && m_running) {
                SleepConditionVariableCS(&m_cv, &m_cs, 100);
            }
            if (!m_running) { LeaveCriticalSection(&m_cs); break; }

            // 先处理回调数据
            if (!m_pendingCallbacks.empty()) {
                CallbackData cb = std::move(m_pendingCallbacks.front());
                m_pendingCallbacks.pop();
                LeaveCriticalSection(&m_cs);  // 释放锁再处理
                ProcessCallback(cb);
                continue;
            }

            // 处理事件分发
            if (!m_queue.empty()) {
                HZCYKJTHardWare_EVENT event = m_queue.front();
                m_queue.pop();
                EventStrings strs = std::move(m_stringsQueue.front());
                m_stringsQueue.pop();

                // 更新指针指向队列中的字符串
                event.request_id = strs.request_id.c_str();
                event.resource_type = strs.resource_type.c_str();
                event.error_code = strs.error_code.c_str();
                event.message = strs.message.c_str();
                event.terminal_base_url = strs.terminal_base_url.c_str();
                event.save_path = strs.save_path.c_str();
                event.raw_json = strs.raw_json.c_str();
                event.ic_number = strs.ic_number.c_str();
                event.mrz = strs.mrz.c_str();
                event.data = strs.data.empty() ? nullptr : strs.data.data();
                event.data_size = (int)strs.data.size();

                THZCYKJTHardWareEventCallback cb = m_callback;

                LeaveCriticalSection(&m_cs);

                if (cb) {
                    // Build JSON string and pass to callback
                    std::string json = "{\"event_type\":" + std::to_string(event.event_type) +
                        ",\"request_id\":\"" + strs.request_id + "\"" +
                        ",\"resource_type\":\"" + strs.resource_type + "\"" +
                        ",\"status\":" + std::to_string(event.status) +
                        ",\"error_code\":\"" + strs.error_code + "\"" +
                        ",\"message\":\"" + strs.message + "\"" +
                        ",\"terminal_index\":" + std::to_string(event.terminal_index) +
                        ",\"terminal_base_url\":\"" + strs.terminal_base_url + "\"" +
                        ",\"save_path\":\"" + strs.save_path + "\"";
                    if (!strs.ic_number.empty())
                        json += ",\"ic_number\":\"" + strs.ic_number + "\"";
                    if (!strs.mrz.empty())
                        json += ",\"mrz\":\"" + strs.mrz + "\"";
                    json += "}";
                    cb(json.c_str());
                } else {
                    LOG_DEBUG("EventDispatcher", "第三方事件回调未注册：event=%d request_id=%s",
                             event.event_type, strs.request_id.c_str());
                }

                LOG_INFO("EventDispatcher", "DLL已回调第三方：event=%d，request_id=%s，resource=%s",
                         event.event_type, strs.request_id.c_str(), strs.resource_type.c_str());
            } else {
                LeaveCriticalSection(&m_cs);
            }
        }
    }

LOG_DEBUG("EventDispatcher", "第三方回调分发线程已退出");
}

} // namespace HZCYKJTHardWare
