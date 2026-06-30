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

static std::string JsonEscape(const std::string& value) {
    std::string escaped;
    escaped.reserve(value.size() + 8);
    for (char ch : value) {
        switch (ch) {
            case '\\': escaped += "\\\\"; break;
            case '"': escaped += "\\\""; break;
            case '\b': escaped += "\\b"; break;
            case '\f': escaped += "\\f"; break;
            case '\n': escaped += "\\n"; break;
            case '\r': escaped += "\\r"; break;
            case '\t': escaped += "\\t"; break;
            default:
                if (static_cast<unsigned char>(ch) < 0x20) {
                    char buf[8] = {0};
                    snprintf(buf, sizeof(buf), "\\u%04x", static_cast<unsigned char>(ch));
                    escaped += buf;
                } else {
                    escaped += ch;
                }
                break;
        }
    }
    return escaped;
}

static bool SafeInvokeThirdPartyCallback(THZCYKJTHardWareEventCallback callback, const char* json) {
    __try {
        callback(json);
        return true;
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
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
static const size_t kMaxEventQueueSize = 512;
static const size_t kMaxPendingCallbackQueueSize = 512;

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
    LOG_DEBUG("事件分发", "事件分发线程已启动");
}

bool EventDispatcher::Stop(int timeoutMs) {
    m_running = false;
    WakeAllConditionVariable(&m_cv);
    if (m_thread && m_thread->joinable()) {
        if (m_thread->get_id() == std::this_thread::get_id()) {
            m_running = true;
            LOG_ERROR("事件分发", "禁止在第三方事件回调线程内调用ReleaseSdk");
            return false;
        }
        DWORD waitResult = WaitForSingleObject(
            static_cast<HANDLE>(m_thread->native_handle()),
            static_cast<DWORD>(timeoutMs > 0 ? timeoutMs : 1));
        if (waitResult != WAIT_OBJECT_0) {
            // Keep the runtime usable. The caller may retry ReleaseSdk after the
            // third-party callback returns.
            m_running = true;
            LOG_ERROR("事件分发", "第三方事件回调线程停止超时：timeout_ms=%d", timeoutMs);
            return false;
        }
        m_thread->join();
    }
    m_thread.reset();
    LOG_DEBUG("事件分发", "事件分发线程已停止");
    return true;
}

void EventDispatcher::SetCallback(THZCYKJTHardWareEventCallback callback) {
    bool callbackChanged = false;
    EnterCriticalSection(&m_cs);
    callbackChanged = (m_callback != callback);
    m_callback = callback;
    if (callbackChanged) {
        std::queue<HZCYKJTHardWare_EVENT> emptyEvents;
        std::queue<EventStrings> emptyStrings;
        std::queue<CallbackData> emptyCallbacks;
        m_queue.swap(emptyEvents);
        m_stringsQueue.swap(emptyStrings);
        m_pendingCallbacks.swap(emptyCallbacks);
    }
    LeaveCriticalSection(&m_cs);
    if (callbackChanged) {
        RequestSessionManager::Instance().CancelAllForCallbackReset();
    }
    LOG_INFO("事件分发", callback ? "第三方事件回调已注册" : "第三方事件回调已清除");
}

void EventDispatcher::PostEvent(const HZCYKJTHardWare_EVENT& event) {
    EnterCriticalSection(&m_cs);
    if (m_queue.size() >= kMaxEventQueueSize) {
        LOG_WARN("事件分发", "第三方事件队列已满，丢弃最旧事件：queue_size=%zu", m_queue.size());
        m_queue.pop();
        if (!m_stringsQueue.empty()) {
            m_stringsQueue.pop();
        }
    }
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
        LOG_WARN("事件分发", "回调分发未运行，已丢弃硬件控制程序回调：path=%s", cbData.path.c_str());
        return;
    }

    // 投递到 worker 线程处理，不阻塞 HTTP 线程
    EnterCriticalSection(&m_cs);
    if (m_pendingCallbacks.size() >= kMaxPendingCallbackQueueSize) {
        LOG_WARN("事件分发", "硬件控制程序回调处理队列已满，丢弃最旧回调：queue_size=%zu", m_pendingCallbacks.size());
        m_pendingCallbacks.pop();
    }
    m_pendingCallbacks.push(cbData);
    LeaveCriticalSection(&m_cs);
    WakeConditionVariable(&m_cv);
}

void EventDispatcher::ProcessCallback(const CallbackData& cbData) {
    std::string path = cbData.path;
    std::string body = cbData.body;

    std::string resourceType;
    bool isPreviewReady = false;
    if (path.find("/preview-ready") != std::string::npos) {
        isPreviewReady = true;
    } else if (path.find("/ocr") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT;
    } else if (path.find("/iris") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_IRIS_IMAGE;
    } else if (path.find("/nfc-card") != std::string::npos || path.find("/nfc") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_NFC_CARD;
    } else if (path.find("/authorize") != std::string::npos) {
        resourceType = HZCYKJTHardWare_RESOURCE_AUTHORIZATION;
    } else {
        LOG_WARN("事件分发", "收到未知硬件控制程序回调路径：path=%s", path.c_str());
        return;
    }

    std::string requestId = JsonHelper::GetString(body, "request_id");
    if (requestId.empty()) {
        LOG_WARN("事件分发", "收到硬件控制程序回调但缺少request_id：path=%s", path.c_str());
        return;
    }

    if (isPreviewReady) {
        ProcessPreviewReadyCallback(requestId, body);
        return;
    }

    auto& sessionMgr = RequestSessionManager::Instance();
    if (sessionMgr.IsRecentlyCompleted(requestId)) {
        LOG_WARN("事件分发", "硬件控制程序重复回调已忽略：request_id=%s，path=%s",
                 requestId.c_str(), path.c_str());
        return;
    }

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
             resourceType == HZCYKJTHardWare_RESOURCE_NFC_CARD ||
             resourceType == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE)) {
            std::string errorCode;
            std::string errorMsg;
            bool isError = IsDelphiErrorResponse(body, errorCode, errorMsg);

            LOG_DEBUG("事件分发", "处理回调结果：resource=%s", resourceType.c_str());

            if (resourceType == HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT) {
                if (isError) {
                    SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_OCR_FAILED,
                              HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                              nullptr, body.c_str());
                } else {
                    std::string mrz = JsonHelper::GetString(body, "mrz");
                    std::string savePath = JsonHelper::GetString(body, "save_path");
                    SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_OCR_SUCCESS,
                              HZCYKJTHardWare_RET_OK, "", "OCR识别完成",
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
                                  "IC卡回调缺少card_text", nullptr, body.c_str());
                    } else {
                        SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_NFC_CARD_SUCCESS,
                                  HZCYKJTHardWare_RET_OK, "", "", nullptr, body.c_str(),
                                  cardText.c_str());
                    }
                }
                return;
            }

            if (resourceType == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE) {
                if (isError) {
                    SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                              HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                              nullptr, body.c_str());
                } else {
                    RequestSession fallbackSession;
                    fallbackSession.request_id = requestId;
                    fallbackSession.resource_type = resourceType;
                    fallbackSession.save_dir = JsonHelper::GetString(body, "save_path");
                    ProcessIrisCallback(requestId, body, fallbackSession);
                }
                return;
            }
        }

        LOG_WARN("事件分发", "收到硬件控制程序回调但请求不存在：request_id=%s，path=%s",
                 requestId.c_str(), path.c_str());
        return;
    }

    if (session->status == RequestStatus::Expired ||
        session->status == RequestStatus::Cancelled ||
        session->status == RequestStatus::Timeout ||
        session->status == RequestStatus::CallbackReceived ||
        session->status == RequestStatus::Completed) {
        LOG_DEBUG("事件分发", "硬件控制程序回调已忽略：request_id=%s，status=%d",
                  requestId.c_str(), static_cast<int>(session->status));
        return;
    }

    if (!sessionMgr.MarkCallbackReceived(requestId, body)) {
        LOG_WARN("事件分发", "硬件控制程序重复回调已忽略：request_id=%s，path=%s",
                 requestId.c_str(), path.c_str());
        return;
    }

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
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_AUTHORIZATION) {
        if (isError) {
            SendEvent(requestId, resourceType, HZCYKJTHardWare_EVENT_AUTHORIZE_FAILED,
                      HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                      nullptr, body.c_str());
        } else {
            ProcessAuthorizeCallback(requestId, body);
        }
    }

    sessionMgr.MarkCompleted(requestId);
}

#if 0
void EventDispatcher::ProcessCallbackLegacy(const CallbackData& cbData) {
    std::string path = cbData.path;
    std::string body = cbData.body;

    LOG_DEBUG("事件分发", "开始处理终端回调：path=%s，body_size=%zu", path.c_str(), body.size());

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
        LOG_WARN("事件分发", "收到未知终端回调路径：path=%s", path.c_str());
        return;
    }

    // 提取 request_id
    std::string requestId = JsonHelper::GetString(body, "request_id");
    if (requestId.empty()) {
        LOG_WARN("事件分发", "收到终端回调但缺少 request_id");
        return;
    }

    // 查找请求会话
    auto& sessionMgr = RequestSessionManager::Instance();
    auto session = sessionMgr.GetSession(requestId);
    if (!session) {
        LOG_WARN("事件分发", "收到终端回调但请求已不存在：request_id=%s", requestId.c_str());
        return;
    }

    // 检查会话状态
    if (session->status == RequestStatus::Expired) {
        LOG_DEBUG("事件分发", "请求已过期，已忽略回调：request_id=%s，原因=终端切换",
                  requestId.c_str());
        return;
    }
    if (session->status == RequestStatus::Cancelled) {
        LOG_DEBUG("事件分发", "请求已取消，已忽略回调：request_id=%s，原因=流程结束",
                  requestId.c_str());
        return;
    }
    if (session->status == RequestStatus::Timeout) {
        LOG_DEBUG("事件分发", "请求已超时，已忽略回调：request_id=%s", requestId.c_str());
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
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "人脸抓拍结果解析失败");
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
        LOG_ERROR("事件分发", "人脸回调处理失败：保存图片失败，request_id=%s，path=%s", requestId.c_str(), fullPath.c_str());
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FACE_IMAGE, HZCYKJTHardWare_EVENT_FACE_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_SAVE_FILE_FAILED, "", "人脸图片保存失败");
        return;
    }

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FACE_IMAGE, HZCYKJTHardWare_EVENT_FACE_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "人脸抓拍成功", fullPath.c_str(), body.c_str());
}

void EventDispatcher::ProcessFingerprintCallback(const std::string& requestId,
                                                  const std::string& body,
                                                  const RequestSession& session) {
    auto fpResult = ResultParser::ParseFingerprintResult(body);
    if (!fpResult.valid) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE, HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "指纹抓拍结果解析失败");
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
                  HZCYKJTHardWare_RET_SAVE_FILE_FAILED, "", "指纹图片保存失败");
        return;
    }

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE, HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "指纹抓拍成功", fullPath.c_str(), body.c_str());
}

void EventDispatcher::ProcessOcrCallback(const std::string& requestId,
                                          const std::string& body,
                                          const RequestSession& session) {
    std::string mrz = JsonHelper::GetString(body, "mrz");
    std::string savePath = JsonHelper::GetString(body, "save_path");
    if (savePath.empty()) {
        savePath = session.save_dir;
    }

    LOG_INFO("事件分发", "OCR回调完成：MRZ=%s", mrz.c_str());
    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, HZCYKJTHardWare_EVENT_OCR_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "OCR识别完成",
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
                    LOG_ERROR("事件分发", "OCR回调处理失败：保存证据图片失败，name=%s，ret=%d",
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
                LOG_ERROR("事件分发", "OCR回调处理失败：保存证件人像失败，ret=%d", saveRet);
            }
        }
    }

    LOG_INFO("事件分发", "OCR回调完成：MRZ=%s", ocrResult.mrz.c_str());
    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, HZCYKJTHardWare_EVENT_OCR_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "OCR识别完成", savePath.c_str(), body.c_str(),
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

    LOG_INFO("事件分发", "虹膜回调完成");
    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "虹膜抓拍成功",
              savePath.c_str(), body.c_str());
}

#if 0
void EventDispatcher::ProcessIrisCallbackLegacy(const std::string& requestId,
                                                const std::string& body,
                                                const RequestSession& session) {
    auto irisResult = ResultParser::ParseIrisResult(body);
    if (!irisResult.valid) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "虹膜抓拍结果解析失败",
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
                      saveRet, "", "左眼虹膜图片保存失败", nullptr, body.c_str());
            return;
        }
        savedCount++;
    }
    if (!irisResult.right_iris_base64.empty()) {
        int saveRet = ImageSaver::SaveBase64ImageAsJpeg(
            savePath, "iris_right", irisResult.right_iris_base64, rightPath);
        if (saveRet != HZCYKJTHardWare_RET_OK) {
            SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                      saveRet, "", "右眼虹膜图片保存失败", nullptr, body.c_str());
            return;
        }
        savedCount++;
    }

    if (savedCount == 0) {
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                  HZCYKJTHardWare_RET_BASE64_FAILED, "", "未保存到虹膜图片", nullptr, body.c_str());
        return;
    }

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "虹膜抓拍成功", savePath.c_str(), body.c_str());
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
        LOG_ERROR("NFC", "硬件控制程序IC卡回调缺少card_text：request_id=%s，body=%s",
                  requestId.c_str(), body.c_str());
        SendEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "IC卡回调缺少card_text",
                  nullptr, body.c_str());
        return;
    }

    LOG_INFO("NFC", "IC卡回调完成：卡号=%s", cardText.c_str());
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
                  HZCYKJTHardWare_RET_PARSE_JSON_FAILED, "", "IC卡识别结果解析失败",
                  nullptr, body.c_str());
        return;
    }

    LOG_INFO("NFC", "IC卡识别成功：卡号=%s", nfcResult.ic_number.c_str());

    SendEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_SUCCESS,
              HZCYKJTHardWare_RET_OK, "", "", nullptr, body.c_str(), nfcResult.ic_number.c_str());
}

#endif

void EventDispatcher::ProcessPreviewReadyCallback(const std::string& requestId,
                                                   const std::string& body) {
    intptr_t renderHwnd = GetIntPtrValue(body, "render_hwnd");
    std::string resourceType = JsonHelper::GetString(body, "resource_type");
    if (resourceType.empty()) {
        resourceType = HZCYKJTHardWare_RESOURCE_FACE_IMAGE;
    }

    // 检查异步预览失败回调（适配硬件控制程序 468157e TAsyncStartPreviewThread）
    std::string errorCode, errorMsg;
    if (IsDelphiErrorResponse(body, errorCode, errorMsg)) {
        int failedEventType = HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED;
        if (resourceType == HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE) {
            failedEventType = HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_FAILED;
        } else if (resourceType == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE) {
            failedEventType = HZCYKJTHardWare_EVENT_IRIS_PREVIEW_FAILED;
        }
        LOG_ERROR("事件分发", "硬件控制程序异步预览启动失败：request_id=%s，resource=%s，code=%s，msg=%s",
                  requestId.c_str(), resourceType.c_str(), errorCode.c_str(), errorMsg.c_str());
        SendEvent(requestId, resourceType, failedEventType,
                  HZCYKJTHardWare_RET_FAILED, errorCode.c_str(), errorMsg.c_str(),
                  nullptr, body.c_str());
        return;
    }

    auto& ctx = HzsjkjtContext::Instance();
    intptr_t thirdPartyHwndValue = 0;
    std::string currentRequestId;
    int startedEventType = HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STARTED;
    int failedEventType = HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED;
    {
        auto lock = ReadLock();
        if (resourceType == HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE) {
            currentRequestId = ctx.fingerprint_preview_request_id;
            thirdPartyHwndValue = ctx.fingerprint_preview_third_party_hwnd;
            startedEventType = HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STARTED;
            failedEventType = HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_FAILED;
        } else if (resourceType == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE) {
            currentRequestId = ctx.iris_preview_request_id;
            thirdPartyHwndValue = ctx.iris_preview_third_party_hwnd;
            startedEventType = HZCYKJTHardWare_EVENT_IRIS_PREVIEW_STARTED;
            failedEventType = HZCYKJTHardWare_EVENT_IRIS_PREVIEW_FAILED;
        } else {
            currentRequestId = ctx.camera_preview_request_id;
            thirdPartyHwndValue = ctx.camera_preview_third_party_hwnd;
        }
        if (currentRequestId != requestId) {
            LOG_WARN("事件分发", "硬件控制程序预览就绪回调request_id不匹配：resource=%s，callback=%s，current=%s",
                     resourceType.c_str(), requestId.c_str(), currentRequestId.c_str());
            return;
        }
    }

    HWND renderWindow = reinterpret_cast<HWND>(renderHwnd);
    HWND thirdPartyWindow = reinterpret_cast<HWND>(thirdPartyHwndValue);

    if (renderHwnd == 0 || !IsWindow(renderWindow)) {
        LOG_ERROR("事件分发", "硬件控制程序预览就绪回调处理失败：render_hwnd无效，request_id=%s，render_hwnd=%p",
                  requestId.c_str(), renderWindow);
        SendEvent(requestId, resourceType, failedEventType,
                  HZCYKJTHardWare_RET_INVALID_HWND, "", "预览渲染窗口句柄无效",
                  nullptr, body.c_str());
        return;
    }

    if (thirdPartyHwndValue == 0 || !IsWindow(thirdPartyWindow)) {
        LOG_ERROR("事件分发", "硬件控制程序预览就绪回调处理失败：第三方HWND无效，request_id=%s，third_party_hwnd=%p，render_hwnd=%p",
                  requestId.c_str(), thirdPartyWindow, renderWindow);
        SendEvent(requestId, resourceType, failedEventType,
                  HZCYKJTHardWare_RET_INVALID_HWND, "", "第三方预览窗口句柄无效",
                  nullptr, body.c_str());
        return;
    }

    if (renderWindow != thirdPartyWindow) {
        LOG_ERROR("事件分发", "硬件控制程序预览渲染目标不一致：request_id=%s，resource=%s，third_party_hwnd=%p，render_hwnd=%p",
                  requestId.c_str(), resourceType.c_str(), thirdPartyWindow, renderWindow);
        SendEvent(requestId, resourceType, failedEventType,
                  HZCYKJTHardWare_RET_PREVIEW_RENDER_FAILED, "", "预览渲染窗口与传入窗口不一致",
                  nullptr, body.c_str());
        return;
    }

    LOG_INFO("事件分发", "预览已启动");
    SendEvent(requestId, resourceType, startedEventType,
              HZCYKJTHardWare_RET_OK, "", "预览已就绪",
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

void EventDispatcher::ProcessTimeouts() {
    auto timeouts = RequestSessionManager::Instance().CheckTimeouts();
    for (const auto& session : timeouts) {
        if (!session) {
            continue;
        }

        int eventType = HZCYKJTHardWare_EVENT_REQUEST_TIMEOUT;
        const char* message = "异步请求超时";
        if (session->resource_type == HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT) {
            eventType = HZCYKJTHardWare_EVENT_OCR_FAILED;
            message = "OCR请求超时";
        } else if (session->resource_type == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE) {
            eventType = HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED;
            message = "虹膜抓拍请求超时";
        } else if (session->resource_type == HZCYKJTHardWare_RESOURCE_NFC_CARD) {
            eventType = HZCYKJTHardWare_EVENT_NFC_CARD_FAILED;
            message = "IC卡识别请求超时";
        } else if (session->resource_type == HZCYKJTHardWare_RESOURCE_AUTHORIZATION) {
            eventType = HZCYKJTHardWare_EVENT_AUTHORIZE_FAILED;
            message = "授权请求超时";
        }

        SendEvent(session->request_id, session->resource_type, eventType,
                  HZCYKJTHardWare_RET_TIMEOUT, "TIMEOUT", message,
                  nullptr, nullptr);
        RequestSessionManager::Instance().MarkCompleted(session->request_id);
    }
}

void EventDispatcher::WorkerLoop() {
LOG_DEBUG("事件分发", "第三方回调分发线程已启动");

    auto lastTimeoutCheck = std::chrono::steady_clock::now();
    while (m_running) {
        auto nowTick = std::chrono::steady_clock::now();
        if (std::chrono::duration_cast<std::chrono::milliseconds>(nowTick - lastTimeoutCheck).count() >= 1000) {
            ProcessTimeouts();
            lastTimeoutCheck = nowTick;
        }

        {
            EnterCriticalSection(&m_cs);
            while (m_queue.empty() && m_pendingCallbacks.empty() && m_running) {
                SleepConditionVariableCS(&m_cv, &m_cs, 100);
                auto waitTick = std::chrono::steady_clock::now();
                if (std::chrono::duration_cast<std::chrono::milliseconds>(waitTick - lastTimeoutCheck).count() >= 1000) {
                    break;
                }
            }
            if (!m_running) { LeaveCriticalSection(&m_cs); break; }
            if (m_queue.empty() && m_pendingCallbacks.empty()) {
                LeaveCriticalSection(&m_cs);
                continue;
            }

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
                        ",\"request_id\":\"" + JsonEscape(strs.request_id) + "\"" +
                        ",\"resource_type\":\"" + JsonEscape(strs.resource_type) + "\"" +
                        ",\"status\":" + std::to_string(event.status) +
                        ",\"error_code\":\"" + JsonEscape(strs.error_code) + "\"" +
                        ",\"message\":\"" + JsonEscape(strs.message) + "\"" +
                        ",\"terminal_index\":" + std::to_string(event.terminal_index) +
                        ",\"terminal_base_url\":\"" + JsonEscape(strs.terminal_base_url) + "\"" +
                        ",\"save_path\":\"" + JsonEscape(strs.save_path) + "\"";
                    if (!strs.ic_number.empty())
                        json += ",\"ic_number\":\"" + JsonEscape(strs.ic_number) + "\"";
                    if (!strs.mrz.empty())
                        json += ",\"mrz\":\"" + JsonEscape(strs.mrz) + "\"";
                    if (!strs.auth_result.empty())
                        json += ",\"auth_result\":" + strs.auth_result;
                    if (!strs.auth_zjhm.empty())
                        json += ",\"ZJHM\":\"" + JsonEscape(strs.auth_zjhm) + "\"";
                    if (!strs.auth_zjlb.empty())
                        json += ",\"ZJLB\":\"" + JsonEscape(strs.auth_zjlb) + "\"";
                    if (!strs.auth_gjdqdm.empty())
                        json += ",\"GJDQDM\":\"" + JsonEscape(strs.auth_gjdqdm) + "\"";
                    if (!strs.auth_xm.empty())
                        json += ",\"XM\":\"" + JsonEscape(strs.auth_xm) + "\"";
                    if (!strs.auth_xb.empty())
                        json += ",\"XB\":\"" + JsonEscape(strs.auth_xb) + "\"";
                    if (!strs.auth_csrq.empty())
                        json += ",\"CSRQ\":\"" + JsonEscape(strs.auth_csrq) + "\"";
                    if (!strs.auth_kadm.empty())
                        json += ",\"KADM\":\"" + JsonEscape(strs.auth_kadm) + "\"";
                    json += "}";
                    if (SafeInvokeThirdPartyCallback(cb, json.c_str())) {
                        LOG_DEBUG("事件分发", "已回调第三方：event=%d", event.event_type);
                    } else {
                        LOG_ERROR("事件分发", "第三方事件回调执行异常，已保护：event=%d，request_id=%s",
                                  event.event_type, strs.request_id.c_str());
                    }
                } else {
                    LOG_DEBUG("事件分发", "第三方事件回调未注册：event=%d request_id=%s",
                             event.event_type, strs.request_id.c_str());
                }
            } else {
                LeaveCriticalSection(&m_cs);
            }
        }
    }

LOG_DEBUG("事件分发", "第三方回调分发线程已退出");
}

void EventDispatcher::ProcessAuthorizeCallback(const std::string& requestId,
                                               const std::string& body) {
    int authResult = JsonHelper::GetInt(body, "auth_result", 0);
    std::string message = JsonHelper::GetString(body, "message");
    if (message.empty()) {
        message = (authResult == 1) ? "授权通过" : "授权未通过";
    }

    // Build the basic event
    auto& ctx = HzsjkjtContext::Instance();
    HZCYKJTHardWare_EVENT event;
    memset(&event, 0, sizeof(event));
    event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
    event.event_type = (authResult == 1)
        ? HZCYKJTHardWare_EVENT_AUTHORIZE_SUCCESS
        : HZCYKJTHardWare_EVENT_AUTHORIZE_FAILED;
    event.request_id = requestId.c_str();
    event.resource_type = HZCYKJTHardWare_RESOURCE_AUTHORIZATION;
    event.status = (authResult == 1) ? HZCYKJTHardWare_RET_OK : HZCYKJTHardWare_RET_FAILED;
    event.message = message.c_str();
    event.terminal_base_url = ctx.current_terminal_base_url.c_str();
    event.terminal_index = ctx.current_terminal_index;
    event.raw_json = body.c_str();

    // Build EventStrings with auth fields
    EnterCriticalSection(&m_cs);
    EventStrings strs;
    strs.request_id = requestId;
    strs.resource_type = HZCYKJTHardWare_RESOURCE_AUTHORIZATION;
    strs.message = message;
    strs.terminal_base_url = ctx.current_terminal_base_url;
    strs.raw_json = body;
    strs.auth_result = std::to_string(authResult);
    strs.auth_zjhm = JsonHelper::GetString(body, "ZJHM");
    strs.auth_zjlb = JsonHelper::GetString(body, "ZJLB");
    strs.auth_gjdqdm = JsonHelper::GetString(body, "GJDQDM");
    strs.auth_xm = JsonHelper::GetString(body, "XM");
    strs.auth_xb = JsonHelper::GetString(body, "XB");
    strs.auth_csrq = JsonHelper::GetString(body, "CSRQ");
    strs.auth_kadm = JsonHelper::GetString(body, "KADM");
    if (m_queue.size() >= kMaxEventQueueSize) {
        LOG_WARN("事件分发", "第三方事件队列已满，丢弃最旧事件：queue_size=%zu", m_queue.size());
        m_queue.pop();
        if (!m_stringsQueue.empty()) {
            m_stringsQueue.pop();
        }
    }
    m_stringsQueue.push(std::move(strs));
    m_queue.push(event);
    WakeConditionVariable(&m_cv);
    LeaveCriticalSection(&m_cs);
}

} // namespace HZCYKJTHardWare
