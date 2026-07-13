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

} // namespace

DelphiProxy::DelphiProxy(const std::string& baseUrl)
    : baseUrl_(TrimTrailingSlash(baseUrl)) {
}

bool DelphiProxy::Ping() {
    std::string response;
    if (!Get("/ping", response)) {
        LOG_ERROR("代理服务", "硬件控制程序连通性检查失败：地址=%s", baseUrl_.c_str());
        return false;
    }
    if (!IsOkResponse(response)) {
        LOG_ERROR("代理服务", "硬件控制程序连通性响应无效：response=%s", response.c_str());
        return false;
    }
    LOG_INFO("代理服务", "硬件控制程序连通性检查成功：地址=%s", baseUrl_.c_str());
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
        "," + callbacksJson.substr(1); // merge request_id + save_dir into callbacks JSON
    std::string response;
    return PostJson("/process/start", body, response) && IsOkResponse(response);
}

bool DelphiProxy::ProcessEnd() {
    std::string response;
    return PostJson("/process/end", "{}", response) && IsOkResponse(response);
}

bool DelphiProxy::SwitchTerminal(int terminalIndex) {
    std::string body = "{" + JsonIntField("terminal_index", terminalIndex) + "}";
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
    LOG_INFO("授权", "DLL转发授权请求至EXE：请求ID=%s，请求地址=%s，回调地址=%s",
             requestId.c_str(), BuildUrl("/authorize").c_str(),
             LogValue(callbackUrl).c_str());

    bool posted = PostJson("/authorize", body, response, timeoutMs, false);
    std::string errorCode;
    std::string errorMessage;
    HasErrorResponse(response, errorCode, errorMessage);
    bool accepted = posted && JsonHelper::GetBool(response, "accepted", false);
    LOG_INFO("授权", "EXE授权受理响应：请求ID=%s，HTTP提交=%s，已受理=%s，状态=%s，错误码=%s，消息=%s",
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
        LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：HTTP客户端未初始化，method=GET，path=%s", path.c_str());
        return false;
    }
    std::string url = BuildUrl(path);
    bool ok = http->Get(url, connectTimeout, requestTimeout, response, statusCode);
    if (!ok) {
        if (!quiet)
            LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：method=GET，url=%s", url.c_str());
        return false;
    }
    if (statusCode < 200 || statusCode >= 300) {
        if (!quiet)
            LOG_ERROR("代理服务", "硬件控制程序响应状态异常：method=GET，url=%s，status=%d，response=%s",
                      url.c_str(), statusCode, response.c_str());
        return false;
    }
    if (!quiet)
        LOG_DEBUG("代理服务", "DLL下发硬件控制程序成功：method=GET，url=%s，status=%d", url.c_str(), statusCode);
    return true;
}

bool DelphiProxy::PostJson(const std::string& path,
                           const std::string& body,
                           std::string& response,
                           int timeoutMs,
                           bool logRawResponse) {
    if (baseUrl_.empty()) {
        LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：base_url为空，method=POST，path=%s", path.c_str());
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
        LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：HTTP客户端未初始化，method=POST，path=%s", path.c_str());
        return false;
    }
    std::string url = BuildUrl(path);
    LOG_DEBUG("代理服务", "DLL正在下发硬件控制程序：method=POST，url=%s", url.c_str());
    bool ok = http->PostJson(url, body, connectTimeout, requestTimeout, response, statusCode);
    if (!ok) {
        LOG_ERROR("代理服务", "DLL下发硬件控制程序失败：method=POST，url=%s", url.c_str());
        return false;
    }
    if (statusCode < 200 || statusCode >= 300) {
        if (logRawResponse) {
            LOG_ERROR("代理服务", "硬件控制程序响应状态异常：method=POST，url=%s，status=%d，response=%s",
                      url.c_str(), statusCode, response.c_str());
        } else {
            LOG_ERROR("代理服务", "硬件控制程序响应状态异常：method=POST，url=%s，HTTP状态=%d，错误码=%s，消息=%s",
                      url.c_str(), statusCode,
                      LogValue(JsonHelper::GetString(response, "code")).c_str(),
                      LogValue(JsonHelper::GetString(response, "message")).c_str());
        }
        return false;
    }

    std::string errorCode;
    std::string errorMessage;
    if (HasErrorResponse(response, errorCode, errorMessage)) {
        if (logRawResponse) {
            LOG_ERROR("代理服务", "硬件控制程序返回业务错误：url=%s，code=%s，message=%s，response=%s",
                      url.c_str(), errorCode.c_str(), errorMessage.c_str(), response.c_str());
        } else {
            LOG_ERROR("代理服务", "硬件控制程序返回业务错误：url=%s，错误码=%s，消息=%s",
                      url.c_str(), LogValue(errorCode).c_str(), LogValue(errorMessage).c_str());
        }
        return false;
    }

    LOG_DEBUG("代理服务", "DLL下发硬件控制程序成功：method=POST，url=%s，status=%d",
              url.c_str(), statusCode);
    return true;
}

bool DelphiProxy::IsOkResponse(const std::string& response) {
    std::string errorCode;
    std::string errorMessage;
    if (HasErrorResponse(response, errorCode, errorMessage)) {
        LOG_ERROR("代理服务", "硬件控制程序响应包含错误：code=%s，message=%s，response=%s",
                  errorCode.c_str(), errorMessage.c_str(), response.c_str());
        return false;
    }

    std::string status = JsonHelper::GetString(response, "status");
    if (status == "ok") {
        return true;
    }

    LOG_ERROR("代理服务", "硬件控制程序响应未返回成功状态：response=%s", response.c_str());
    return false;
}

bool DelphiProxy::IsAcceptedResponse(const std::string& response) {
    std::string errorCode;
    std::string errorMessage;
    if (HasErrorResponse(response, errorCode, errorMessage)) {
        LOG_ERROR("代理服务", "硬件控制程序受理响应包含错误：code=%s，message=%s，response=%s",
                  errorCode.c_str(), errorMessage.c_str(), response.c_str());
        return false;
    }

    if (JsonHelper::GetBool(response, "accepted", false)) {
        return true;
    }

    LOG_ERROR("代理服务", "硬件控制程序未受理请求：response=%s", response.c_str());
    return false;
}

bool DelphiProxy::ExtractSavePath(const std::string& response,
                                  std::string& outSavePath) {
    if (!IsOkResponse(response)) {
        return false;
    }

    outSavePath = JsonHelper::GetString(response, "save_path");
    if (outSavePath.empty()) {
        LOG_ERROR("代理服务", "硬件控制程序同步抓拍响应缺少save_path：response=%s",
                  response.c_str());
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
        LOG_ERROR("代理服务", "硬件控制程序预览地址响应为空：path=%s，response=%s",
                  path.c_str(), response.c_str());
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

} // namespace HZCYKJTHardWare
