#include "pch.h"
#include "delphi_proxy.h"
#include "http_client.h"
#include "json_helper.h"
#include "hzsjkjt_context.h"
#include "logger.h"

namespace HZCYKJTHardWare {

namespace {

std::string TrimTrailingSlash(std::string value) {
    while (!value.empty() && value.back() == '/') {
        value.pop_back();
    }
    return value;
}

std::string EscapeJsonString(const std::string& value) {
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

std::string JsonStringField(const char* name, const std::string& value) {
    return std::string("\"") + name + "\":\"" + EscapeJsonString(value) + "\"";
}

std::string JsonIntField(const char* name, intptr_t value) {
    return std::string("\"") + name + "\":" + std::to_string(value);
}

std::string BuildRequestIdSaveDirJson(const std::string& requestId,
                                      const std::string& saveDir) {
    return "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonStringField("save_dir", saveDir) +
        "}";
}

std::string BuildFingerprintCaptureJson(const std::string& requestId,
                                        const std::string& saveDir,
                                        const std::string& saveDirHk) {
    if (saveDirHk.empty()) {
        return BuildRequestIdSaveDirJson(requestId, saveDir);
    }
    return "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonStringField("save_dir", saveDir) + "," +
        JsonStringField("save_dir_hk", saveDirHk) +
        "}";
}

std::string BuildAsyncJson(const std::string& requestId,
                           const std::string& saveDir,
                           const std::string& callbackUrl) {
    return "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonStringField("save_dir", saveDir) + "," +
        JsonStringField("callback_url", callbackUrl) +
        "}";
}

std::string LogValue(const std::string& value, size_t maxLength = 256) {
    std::string result;
    result.reserve(value.size());
    for (char ch : value) {
        unsigned char c = static_cast<unsigned char>(ch);
        if (ch == '\r' || ch == '\n' || ch == '\t' || c < 0x20) {
            result += ' ';
        } else {
            result += ch;
        }
        if (result.size() >= maxLength) {
            result += "...";
            break;
        }
    }
    return result;
}

bool HasErrorResponse(const std::string& response, std::string& code, std::string& message) {
    if (!JsonHelper::GetBool(response, "error", false)) {
        return false;
    }
    code = JsonHelper::GetString(response, "code");
    message = JsonHelper::GetString(response, "message");
    return true;
}

int MapLatestPlateFrameErrorCode(const std::string& code) {
    if (code == "preview_not_running")
        return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
    if (code == "frame_not_ready")
        return HZCYKJTHardWare_FRAME_NOT_READY;
    if (code == "frame_stale")
        return HZCYKJTHardWare_FRAME_STALE;
    if (code == "invalid_camera" || code == "frame_invalid_camera")
        return HZCYKJTHardWare_FRAME_INVALID_CAMERA;
    if (code == "frame_data_invalid")
        return HZCYKJTHardWare_FRAME_DATA_INVALID;
    if (code == "frame_too_large")
        return HZCYKJTHardWare_FRAME_TOO_LARGE;
    if (code == "frame_busy" || code == "service_busy" || code == "busy")
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    if (code == "timeout")
        return HZCYKJTHardWare_RET_TIMEOUT;
    if (code == "not_supported")
        return HZCYKJTHardWare_RET_UNSUPPORTED;
    if (code == "invalid_request_id")
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    return HZCYKJTHardWare_RET_HTTP_FAILED;
}

bool HasJpegSignature(const std::string& data) {
    return data.size() >= 4 &&
        static_cast<unsigned char>(data[0]) == 0xFF &&
        static_cast<unsigned char>(data[1]) == 0xD8 &&
        static_cast<unsigned char>(data[data.size() - 2]) == 0xFF &&
        static_cast<unsigned char>(data[data.size() - 1]) == 0xD9;
}

bool IsJpegStartOfFrame(unsigned char marker) {
    return (marker >= 0xC0 && marker <= 0xC3) ||
           (marker >= 0xC5 && marker <= 0xC7) ||
           (marker >= 0xC9 && marker <= 0xCB) ||
           (marker >= 0xCD && marker <= 0xCF);
}

bool TryGetJpegDimensions(const std::string& data, int& width, int& height) {
    width = 0;
    height = 0;
    if (!HasJpegSignature(data)) return false;

    size_t index = 2;
    while (index + 1 < data.size()) {
        if (static_cast<unsigned char>(data[index]) != 0xFF)
            return false;
        while (index < data.size() &&
               static_cast<unsigned char>(data[index]) == 0xFF) {
            ++index;
        }
        if (index >= data.size()) return false;

        const unsigned char marker = static_cast<unsigned char>(data[index++]);
        if (marker == 0xD9 || marker == 0xDA) break;
        if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7)) continue;
        if (index + 1 >= data.size()) return false;

        const unsigned int segmentLength =
            (static_cast<unsigned char>(data[index]) << 8) |
            static_cast<unsigned char>(data[index + 1]);
        if (segmentLength < 2 || index + segmentLength > data.size())
            return false;

        if (IsJpegStartOfFrame(marker) && segmentLength >= 7) {
            height = (static_cast<unsigned char>(data[index + 3]) << 8) |
                     static_cast<unsigned char>(data[index + 4]);
            width = (static_cast<unsigned char>(data[index + 5]) << 8) |
                    static_cast<unsigned char>(data[index + 6]);
            return width > 0 && height > 0;
        }
        index += segmentLength;
    }
    return false;
}

std::string GetResponseHeader(const std::map<std::string, std::string>& headers,
                              const char* name) {
    if (!name) return "";
    auto it = headers.find(name);
    return it == headers.end() ? "" : it->second;
}

int GetResponseHeaderInt(const std::map<std::string, std::string>& headers,
                         const char* name) {
    const std::string value = GetResponseHeader(headers, name);
    if (value.empty()) return -1;
    char* end = nullptr;
    const long parsed = std::strtol(value.c_str(), &end, 10);
    if (end == value.c_str() || (end && *end != '\0')) return -1;
    return parsed < INT_MIN || parsed > INT_MAX ? -1 : static_cast<int>(parsed);
}

} // 匿名命名空间结束

DelphiProxy::DelphiProxy(const std::string& baseUrl)
    : baseUrl_(TrimTrailingSlash(baseUrl)) {
}

bool DelphiProxy::Ping() {
    std::string response;
    if (!Get("/ping", response, -1, true)) {
        return false;
    }
    if (!IsOkResponse(response)) {
        return false;
    }
    LOG_DEBUG("代理服务", "硬件控制程序连通性检查成功：地址=%s",
             SanitizeUrlForLog(baseUrl_).c_str());
    return true;
}

bool DelphiProxy::GetInstanceId(std::string& outInstanceId, int timeoutMs) {
    outInstanceId.clear();
    std::string response;
    if (!Get("/ping", response, timeoutMs, true)) {
        return false;
    }
    if (JsonHelper::GetString(response, "status") != "ok") {
        return false;
    }
    outInstanceId = JsonHelper::GetString(response, "proxy_instance_id");
    return !outInstanceId.empty();
}

bool DelphiProxy::ProcessStart(const std::string& requestId,
                               const std::string& saveDir,
                               const std::string& callbacksJson) {
    std::string body = "{" + JsonStringField("request_id", requestId) +
        "," + JsonStringField("save_dir", saveDir) +
        "," + callbacksJson.substr(1); // 将 request_id 和 save_dir 合并到回调 JSON
    std::string response;
    return PostJson("/process/start", body, response) && IsOkResponse(response);
}

bool DelphiProxy::ProcessEnd(const std::string& requestId, int timeoutMs) {
    std::string response;
    std::string body = "{" + JsonStringField("request_id", requestId) + "}";
    return PostJson("/process/end", body, response, timeoutMs) &&
        IsOkResponse(response);
}

bool DelphiProxy::SwitchTerminal(int terminalIndex,
                                 const std::string& requestId) {
    std::string body = "{" + JsonIntField("terminal_index", terminalIndex);
    if (!requestId.empty())
        body += "," + JsonStringField("request_id", requestId);
    body += "}";
    std::string response;
    return PostJson("/terminal/switch", body, response) && IsOkResponse(response);
}

bool DelphiProxy::CaptureFace(const std::string& requestId,
                              const std::string& saveDir,
                              std::string& outSavePath,
                              int timeoutMs) {
    std::string response;
    std::string body = BuildRequestIdSaveDirJson(requestId, saveDir);
    if (!PostJson("/capture/face", body, response, timeoutMs)) {
        return false;
    }
    return ExtractSavePath(response, outSavePath);
}

bool DelphiProxy::CaptureFingerprint(const std::string& requestId,
                                     const std::string& saveDir,
                                     const std::string& saveDirHk,
                                     std::string& outSavePath,
                                     int timeoutMs) {
    std::string response;
    std::string body = BuildFingerprintCaptureJson(requestId, saveDir, saveDirHk);
    if (!PostJson("/capture/fingerprint", body, response, timeoutMs)) {
        return false;
    }
    return ExtractSavePath(response, outSavePath);
}

bool DelphiProxy::CaptureIrisAsync(const std::string& requestId,
                                   const std::string& saveDir,
                                   const std::string& callbackUrl) {
    std::string response;
    return PostJson("/capture/iris", BuildAsyncJson(requestId, saveDir, callbackUrl), response) &&
        IsAcceptedResponse(response);
}

bool DelphiProxy::RequestOcrAsync(const std::string& requestId,
                                  const std::string& saveDir,
                                  const std::string& callbackUrl) {
    std::string response;
    return PostJson("/ocr", BuildAsyncJson(requestId, saveDir, callbackUrl), response) &&
        IsAcceptedResponse(response);
}

bool DelphiProxy::RequestNfcAsync(const std::string& requestId,
                                  const std::string& saveDir,
                                  const std::string& callbackUrl) {
    std::string response;
    return PostJson("/nfc", BuildAsyncJson(requestId, saveDir, callbackUrl), response) &&
        IsAcceptedResponse(response);
}

bool DelphiProxy::GetCameraPreviewUrl(const std::string& requestId, std::string& outPreviewUrl) {
    return GetPreviewUrl("/preview/camera/url", requestId, outPreviewUrl);
}

bool DelphiProxy::GetFingerprintPreviewUrl(const std::string& requestId, std::string& outPreviewUrl) {
    return GetPreviewUrl("/preview/fingerprint/url", requestId, outPreviewUrl);
}

bool DelphiProxy::GetIrisPreviewUrl(const std::string& requestId, std::string& outPreviewUrl) {
    return GetPreviewUrl("/preview/iris/url", requestId, outPreviewUrl);
}

bool DelphiProxy::StartCameraPreview(const std::string& requestId,
                                     intptr_t thirdPartyHwnd,
                                     const std::string& callbackUrl,
                                     int timeoutMs) {
    std::string body = "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonIntField("hwnd", thirdPartyHwnd) + "," +
        JsonStringField("callback_url", callbackUrl) +
        "}";

    std::string response;
    return PostJson("/preview/camera/start", body, response, timeoutMs) && IsAcceptedResponse(response);
}

bool DelphiProxy::StopCameraPreview(const std::string& requestId, int timeoutMs) {
    std::string body = "{" + JsonStringField("request_id", requestId) + "}";
    std::string response;
    return PostJson("/preview/camera/stop", body, response, timeoutMs) && IsOkResponse(response);
}

bool DelphiProxy::StartFingerprintPreview(const std::string& requestId,
                                           intptr_t thirdPartyHwnd,
                                           const std::string& callbackUrl,
                                           int timeoutMs) {
    std::string body = "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonIntField("hwnd", thirdPartyHwnd) + "," +
        JsonStringField("callback_url", callbackUrl) +
        "}";

    std::string response;
    return PostJson("/preview/fingerprint/start", body, response, timeoutMs) && IsAcceptedResponse(response);
}

bool DelphiProxy::StopFingerprintPreview(const std::string& requestId, int timeoutMs) {
    std::string body = "{" + JsonStringField("request_id", requestId) + "}";
    std::string response;
    return PostJson("/preview/fingerprint/stop", body, response, timeoutMs) && IsOkResponse(response);
}

bool DelphiProxy::StartIrisPreview(const std::string& requestId,
                                    intptr_t thirdPartyHwnd,
                                    const std::string& callbackUrl) {
    std::string body = "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonIntField("hwnd", thirdPartyHwnd) + "," +
        JsonStringField("callback_url", callbackUrl) +
        "}";

    std::string response;
    return PostJson("/preview/iris/start", body, response) && IsAcceptedResponse(response);
}

bool DelphiProxy::StopIrisPreview(const std::string& requestId) {
    std::string body = "{" + JsonStringField("request_id", requestId) + "}";
    std::string response;
    return PostJson("/preview/iris/stop", body, response) && IsOkResponse(response);
}

bool DelphiProxy::StartPlatePreview(const std::string& plateCode,
                                    const std::string& requestId,
                                    intptr_t thirdPartyHwnd,
                                    const std::string& callbackUrl,
                                    int timeoutMs) {
    std::string body = "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonIntField("hwnd", thirdPartyHwnd) + "," +
        JsonStringField("callback_url", callbackUrl) +
        "}";

    std::string response;
    return PostJson("/preview/plate/" + plateCode + "/start", body, response, timeoutMs) &&
        IsAcceptedResponse(response);
}

bool DelphiProxy::StopPlatePreview(const std::string& plateCode,
                                   const std::string& requestId,
                                   int timeoutMs) {
    std::string body = "{" + JsonStringField("request_id", requestId) + "}";
    std::string response;
    return PostJson("/preview/plate/" + plateCode + "/stop", body, response, timeoutMs) &&
        IsOkResponse(response);
}

bool DelphiProxy::GetLatestPlateFrame(const std::string& plateCode,
                                      const std::string& requestId,
                                      std::vector<unsigned char>& outJpeg,
                                      int timeoutMs) {
    return GetLatestPlateFrame(plateCode, requestId, outJpeg, timeoutMs,
                               "", nullptr);
}

bool DelphiProxy::GetLatestPlateFrame(const std::string& plateCode,
                                      const std::string& requestId,
                                      std::vector<unsigned char>& outJpeg,
                                      int timeoutMs,
                                      const std::string& captureRequestId,
                                      LatestPlateFrameMetadata* metadata) {
    constexpr size_t kMaxJpegBytes = 8U * 1024U * 1024U;
    outJpeg.clear();
    lastResultCode_ = HZCYKJTHardWare_RET_OK;
    if (metadata) {
        *metadata = LatestPlateFrameMetadata();
    }
    auto setProxyError = [&](const char* errorCode) {
        if (metadata)
            metadata->proxyErrorCode = errorCode ? errorCode : "unknown";
    };

    if (plateCode != "cj" && plateCode != "rj2" && plateCode != "rj3") {
        lastResultCode_ = HZCYKJTHardWare_FRAME_INVALID_CAMERA;
        setProxyError("invalid_camera");
        return false;
    }
    if (requestId.empty()) {
        lastResultCode_ = HZCYKJTHardWare_RET_INVALID_PARAM;
        setProxyError("invalid_request_id");
        LOG_DEBUG("代理服务", "获取最新车牌帧底层请求失败：request_id为空，车牌=%s",
                  plateCode.c_str());
        return false;
    }
    if (baseUrl_.empty()) {
        lastResultCode_ = HZCYKJTHardWare_RET_HTTP_FAILED;
        setProxyError("proxy_url_empty");
        LOG_DEBUG("代理服务", "获取最新车牌帧底层请求失败：基础地址为空，车牌=%s，request_id=%s",
                  plateCode.c_str(), requestId.c_str());
        return false;
    }

    auto& ctx = HzsjkjtContext::Instance();
    auto* http = ctx.http_client;
    if (!http) {
        lastResultCode_ = HZCYKJTHardWare_RET_NOT_INITIALIZED;
        setProxyError("http_client_not_initialized");
        LOG_DEBUG("代理服务", "获取最新车牌帧底层请求失败：HTTP客户端未初始化，车牌=%s，request_id=%s",
                  plateCode.c_str(), requestId.c_str());
        return false;
    }

    int connectTimeout = ctx.http_connect_timeout_ms;
    int requestTimeout = ctx.http_request_timeout_ms;
    if (timeoutMs > 0) {
        connectTimeout = (std::min)(connectTimeout, timeoutMs);
        requestTimeout = timeoutMs;
    }

    const std::string path = "/preview/plate/" + plateCode + "/latest-frame";
    std::string body = "{" + JsonStringField("request_id", requestId);
    if (!captureRequestId.empty())
        body += "," + JsonStringField("capture_request_id", captureRequestId);
    body += "}";
    const std::string url = BuildUrl(path);
    const std::string safeUrl = SanitizeUrlForLog(url);
    std::string response;
    int statusCode = 0;
    std::map<std::string, std::string> responseHeaders;
    LOG_DEBUG("代理服务", "获取最新车牌帧：车牌=%s，地址=%s，request_id=%s",
              plateCode.c_str(), safeUrl.c_str(), requestId.c_str());

    const bool posted = http->PostBinary(url, body, connectTimeout, requestTimeout,
                                         kMaxJpegBytes, response, statusCode,
                                         &responseHeaders);
    if (!posted) {
        if (statusCode == -1)
            lastResultCode_ = HZCYKJTHardWare_FRAME_TOO_LARGE;
        else if (statusCode == -2)
            lastResultCode_ = HZCYKJTHardWare_RET_TIMEOUT;
        else
            lastResultCode_ = HZCYKJTHardWare_RET_HTTP_FAILED;
        const char* proxyError = statusCode == -1 ? "frame_too_large" :
            (statusCode == -2 ? "timeout" : "http_failed");
        setProxyError(proxyError);
        LOG_DEBUG("代理服务", "获取最新车牌帧底层请求失败：二进制HTTP请求失败，车牌=%s，"
                  "状态=%d，request_id=%s，返回码=%d",
                  plateCode.c_str(), statusCode, requestId.c_str(), lastResultCode_);
        return false;
    }

    std::string errorCode;
    std::string errorMessage;
    const bool jsonLike = !response.empty() && response[0] == '{';
    if (statusCode < 200 || statusCode >= 300 ||
        (jsonLike && HasErrorResponse(response, errorCode, errorMessage))) {
        if (errorCode.empty())
            errorCode = statusCode < 200 || statusCode >= 300
                ? "http_status_error" : "proxy_rejected";
        lastResultCode_ = MapLatestPlateFrameErrorCode(errorCode);
        if (metadata && (errorCode == "frame_stale" ||
                         errorCode == "frame_not_ready"))
            metadata->source = "OnDemandSnapshot";
        setProxyError(errorCode.c_str());
        LOG_DEBUG("代理服务", "获取最新车牌帧被Proxy拒绝：车牌=%s，状态=%d，"
                  "request_id=%s，错误码=%s，消息=%s，返回码=%d",
                  plateCode.c_str(), statusCode, requestId.c_str(),
                  errorCode.c_str(),
                  errorMessage.empty() ? "" : errorMessage.c_str(),
                  lastResultCode_);
        return false;
    }

    if (response.size() > kMaxJpegBytes || !HasJpegSignature(response)) {
        lastResultCode_ = response.size() > kMaxJpegBytes
            ? HZCYKJTHardWare_FRAME_TOO_LARGE
            : HZCYKJTHardWare_FRAME_DATA_INVALID;
        setProxyError(response.size() > kMaxJpegBytes
            ? "frame_too_large" : "frame_data_invalid");
        LOG_DEBUG("代理服务", "获取最新车牌帧底层请求失败：响应不是有效JPEG，车牌=%s，"
                  "request_id=%s，bytes=%zu，返回码=%d",
                  plateCode.c_str(), requestId.c_str(), response.size(),
                  lastResultCode_);
        return false;
    }

    if (metadata) {
        metadata->source = GetResponseHeader(responseHeaders,
            "X-HZCY-Frame-Source");
        metadata->width = GetResponseHeaderInt(responseHeaders,
            "X-HZCY-Frame-Width");
        metadata->height = GetResponseHeaderInt(responseHeaders,
            "X-HZCY-Frame-Height");
        metadata->frameAgeMs = GetResponseHeaderInt(responseHeaders,
            "X-HZCY-Frame-Age-Ms");

        if (metadata->width <= 0 || metadata->height <= 0)
            TryGetJpegDimensions(response, metadata->width, metadata->height);
    }

    outJpeg.assign(response.begin(), response.end());
    return true;
}

bool DelphiProxy::RequestAuthorize(const std::string& requestId,
                                    const std::string& ZJHM,
                                    const std::string& ZJLB,
                                    const std::string& GJDQDM,
                                    const std::string& XM,
                                    const std::string& XB,
                                    const std::string& CSRQ,
                                    const std::string& KADM,
                                    const std::string& callbackUrl,
                                    int timeoutMs) {
    std::string body = "{" +
        JsonStringField("request_id", requestId) + "," +
        JsonStringField("ZJHM", ZJHM) + "," +
        JsonStringField("ZJLB", ZJLB) + "," +
        JsonStringField("GJDQDM", GJDQDM) + "," +
        JsonStringField("XM", XM) + "," +
        JsonStringField("XB", XB) + "," +
        JsonStringField("CSRQ", CSRQ) + "," +
        JsonStringField("KADM", KADM) + "," +
        JsonStringField("callback_url", callbackUrl) +
        "}";
    std::string response;
    const std::string safeAuthorizeUrl = SanitizeUrlForLog(BuildUrl("/authorize"));
    LOG_DEBUG("授权", "DLL转发授权请求至EXE：请求ID=%s，请求地址=%s，回调地址=%s",
              requestId.c_str(), safeAuthorizeUrl.c_str(),
              SanitizeUrlForLog(callbackUrl).c_str());

    bool posted = PostJson("/authorize", body, response, timeoutMs, false);
    std::string errorCode;
    std::string errorMessage;
    HasErrorResponse(response, errorCode, errorMessage);
    bool accepted = posted && JsonHelper::GetBool(response, "accepted", false);
    LOG_DEBUG("授权", "EXE授权受理响应：请求ID=%s，HTTP提交=%s，已受理=%s，状态=%s，错误码=%s，消息=%s",
              requestId.c_str(), posted ? "是" : "否",
              accepted ? "是" : "否",
              LogValue(JsonHelper::GetString(response, "status")).c_str(),
              LogValue(errorCode).c_str(), LogValue(errorMessage).c_str());
    if (!accepted && posted) {
        LOG_ERROR("授权", "EXE未受理授权请求：请求ID=%s，状态=%s，错误码=%s，消息=%s",
                  requestId.c_str(),
                  LogValue(JsonHelper::GetString(response, "status")).c_str(),
                  LogValue(errorCode).c_str(), LogValue(errorMessage).c_str());
    }
    return accepted;
}

bool DelphiProxy::Get(const std::string& path, std::string& response,
                      int timeoutMs, bool quiet) {
    if (baseUrl_.empty()) {
        if (!quiet)
            LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：base_url为空，method=GET，path=%s", path.c_str());
        return false;
    }

    auto& ctx = HzsjkjtContext::Instance();
    int connectTimeout = ctx.http_connect_timeout_ms;
    int requestTimeout = ctx.http_request_timeout_ms;
    if (timeoutMs > 0) {
        connectTimeout = (std::min)(connectTimeout, timeoutMs);
        requestTimeout = timeoutMs;
    }

    int statusCode = 0;
    auto* http = ctx.http_client;
    if (!http) {
        if (!quiet)
            LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：HTTP客户端未初始化，method=GET，path=%s", path.c_str());
        return false;
    }
    std::string url = BuildUrl(path);
    const std::string safeUrl = SanitizeUrlForLog(url);
    bool ok = http->Get(url, connectTimeout, requestTimeout, response, statusCode, quiet);
    if (!ok) {
        if (!quiet)
            LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：method=GET，url=%s", safeUrl.c_str());
        return false;
    }
    if (statusCode < 200 || statusCode >= 300) {
        if (!quiet)
            LOG_ERROR("代理服务", "硬件控制程序响应状态异常：method=GET，url=%s，status=%d，%s",
                      safeUrl.c_str(), statusCode,
                      SanitizeLargePayloadForLog(response).c_str());
        return false;
    }
    if (!quiet)
        LOG_DEBUG("代理服务", "DLL下发硬件控制程序成功：method=GET，url=%s，status=%d", safeUrl.c_str(), statusCode);
    return true;
}

bool DelphiProxy::PostJson(const std::string& path,
                           const std::string& body,
                           std::string& response,
                           int timeoutMs,
                           bool logRawResponse) {
    lastResultCode_ = HZCYKJTHardWare_RET_OK;
    const std::string requestId = JsonHelper::GetString(body, "request_id");
    const char* requestIdForLog = requestId.empty() ? "<无>" : requestId.c_str();
    if (baseUrl_.empty()) {
        lastResultCode_ = HZCYKJTHardWare_RET_HTTP_FAILED;
        LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：基础地址为空，方法=POST，路径=%s，request_id=%s",
                  path.c_str(), requestIdForLog);
        return false;
    }

    auto& ctx = HzsjkjtContext::Instance();
    int connectTimeout = ctx.http_connect_timeout_ms;
    int requestTimeout = ctx.http_request_timeout_ms;
    if (timeoutMs > 0) {
        connectTimeout = (std::min)(connectTimeout, timeoutMs);
        requestTimeout = timeoutMs;
    }

    int statusCode = 0;
    auto* http = ctx.http_client;
    if (!http) {
        lastResultCode_ = HZCYKJTHardWare_RET_NOT_INITIALIZED;
        LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：HTTP客户端未初始化，方法=POST，路径=%s，request_id=%s",
                  path.c_str(), requestIdForLog);
        return false;
    }
    std::string url = BuildUrl(path);
    const std::string safeUrl = SanitizeUrlForLog(url);
    LOG_DEBUG("代理服务", "DLL正在下发硬件控制程序：方法=POST，地址=%s，request_id=%s",
              safeUrl.c_str(), requestIdForLog);
    bool ok = http->PostJson(url, body, connectTimeout, requestTimeout, response, statusCode);
    if (!ok) {
        lastResultCode_ = HZCYKJTHardWare_RET_HTTP_FAILED;
        LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：方法=POST，地址=%s，request_id=%s",
                  safeUrl.c_str(), requestIdForLog);
        return false;
    }
    if (statusCode < 200 || statusCode >= 300) {
        lastResultCode_ = HZCYKJTHardWare_RET_HTTP_FAILED;
        const std::string responseCode = JsonHelper::GetString(response, "code");
        if (responseCode == "not_supported")
            lastResultCode_ = HZCYKJTHardWare_RET_UNSUPPORTED;
        if (logRawResponse) {
            LOG_ERROR("代理服务", "硬件控制程序响应状态异常：方法=POST，地址=%s，状态=%d，request_id=%s，%s",
                      safeUrl.c_str(), statusCode, requestIdForLog,
                      SanitizeLargePayloadForLog(response, requestId).c_str());
        } else {
            LOG_ERROR("代理服务", "硬件控制程序响应状态异常：方法=POST，地址=%s，HTTP状态=%d，request_id=%s，错误码=%s，消息=%s",
                      safeUrl.c_str(), statusCode, requestIdForLog,
                      LogValue(JsonHelper::GetString(response, "code")).c_str(),
                      LogValue(JsonHelper::GetString(response, "message")).c_str());
        }
        return false;
    }

    std::string errorCode;
    std::string errorMessage;
    if (HasErrorResponse(response, errorCode, errorMessage)) {
        if (errorCode == "not_supported")
            lastResultCode_ = HZCYKJTHardWare_RET_UNSUPPORTED;
        if (logRawResponse) {
            LOG_ERROR("代理服务", "硬件控制程序返回业务错误：地址=%s，request_id=%s，代码=%s，消息=%s，%s",
                      safeUrl.c_str(), requestIdForLog, errorCode.c_str(), errorMessage.c_str(),
                      SanitizeLargePayloadForLog(response, requestId).c_str());
        } else {
            LOG_ERROR("代理服务", "硬件控制程序返回业务错误：地址=%s，request_id=%s，错误码=%s，消息=%s",
                      safeUrl.c_str(), requestIdForLog,
                      LogValue(errorCode).c_str(), LogValue(errorMessage).c_str());
        }
        return false;
    }

    LOG_DEBUG("代理服务", "DLL下发硬件控制程序成功：方法=POST，地址=%s，状态=%d，request_id=%s",
              safeUrl.c_str(), statusCode, requestIdForLog);
    return true;
}

bool DelphiProxy::IsOkResponse(const std::string& response) {
    std::string errorCode;
    std::string errorMessage;
    if (HasErrorResponse(response, errorCode, errorMessage)) {
        LOG_ERROR("代理服务", "硬件控制程序响应包含错误：code=%s，message=%s，%s",
                  errorCode.c_str(), errorMessage.c_str(),
                  SanitizeLargePayloadForLog(response).c_str());
        return false;
    }

    std::string status = JsonHelper::GetString(response, "status");
    if (status == "ok") {
        return true;
    }

    LOG_ERROR("代理服务", "硬件控制程序响应未返回成功状态：%s",
              SanitizeLargePayloadForLog(response).c_str());
    return false;
}

bool DelphiProxy::IsAcceptedResponse(const std::string& response) {
    std::string errorCode;
    std::string errorMessage;
    if (HasErrorResponse(response, errorCode, errorMessage)) {
        LOG_ERROR("代理服务", "硬件控制程序受理响应包含错误：code=%s，message=%s，%s",
                  errorCode.c_str(), errorMessage.c_str(),
                  SanitizeLargePayloadForLog(response).c_str());
        return false;
    }

    if (JsonHelper::GetBool(response, "accepted", false)) {
        return true;
    }

    LOG_ERROR("代理服务", "硬件控制程序未受理请求：%s",
              SanitizeLargePayloadForLog(response).c_str());
    return false;
}

bool DelphiProxy::ExtractSavePath(const std::string& response,
                                  std::string& outSavePath) {
    if (!IsOkResponse(response)) {
        return false;
    }

    outSavePath = JsonHelper::GetString(response, "save_path");
    if (outSavePath.empty()) {
        LOG_ERROR("代理服务", "硬件控制程序同步抓拍响应缺少save_path：%s",
                  SanitizeLargePayloadForLog(response).c_str());
        return false;
    }
    return true;
}

bool DelphiProxy::GetPreviewUrl(const std::string& path,
                                const std::string& requestId,
                                std::string& outPreviewUrl) {
    std::string response;
    std::string body = "{" + JsonStringField("request_id", requestId) + "}";
    if (!PostJson(path, body, response) || !IsOkResponse(response)) {
        return false;
    }

    outPreviewUrl = JsonHelper::GetString(response, "preview_url");
    if (outPreviewUrl.empty()) {
        LOG_ERROR("代理服务", "硬件控制程序预览地址响应为空：path=%s，%s",
                  path.c_str(), SanitizeLargePayloadForLog(response, requestId).c_str());
        return false;
    }
    return true;
}

std::string DelphiProxy::BuildUrl(const std::string& path) const {
    if (path.empty()) {
        return baseUrl_;
    }
    if (path[0] == '/') {
        return baseUrl_ + path;
    }
    return baseUrl_ + "/" + path;
}

} // HZCYKJTHardWare 命名空间结束
