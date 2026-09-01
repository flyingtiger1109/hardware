#include "pch.h"
#include "http_client.h"
#include "json_helper.h"
#include "logger.h"
#include "path_helper.h"

namespace HZCYKJTHardWare {

HttpClient::HttpClient() {
    m_hSession = WinHttpOpen(L"HZCYKJTHardWare-DLL/1.0",
                              WINHTTP_ACCESS_TYPE_NO_PROXY,
                              WINHTTP_NO_PROXY_NAME,
                              WINHTTP_NO_PROXY_BYPASS, 0);
    if (!m_hSession) {
        LOG_ERROR("HTTP请求", "HTTP客户端初始化失败：WinHttpOpen 错误码=%lu", GetLastError());
    }
}

HttpClient::~HttpClient() {
    if (m_hSession) {
        WinHttpCloseHandle(m_hSession);
        m_hSession = nullptr;
    }
}

bool HttpClient::PostJson(const std::string& url,
                          const std::string& body,
                          int connectTimeoutMs,
                          int requestTimeoutMs,
                          std::string& responseBody,
                          int& responseStatusCode) {
    const ULONGLONG startedAt = GetTickCount64();
    const std::string requestId = JsonHelper::GetString(body, "request_id");
    const char* requestIdForLog = requestId.empty() ? "<无>" : requestId.c_str();
    const std::string safeUrl = SanitizeUrlForLog(url);
    responseBody.clear();
    responseStatusCode = 0;

    if (!m_hSession) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST|session", "HTTP请求",
            "HTTP请求失败：WinHTTP会话未初始化，request_id=%s", requestIdForLog);
        return false;
    }

    // 解析 URL
    std::wstring wUrl = PathHelper::Utf8ToWide(url);
    URL_COMPONENTS urlComp = {0};
    urlComp.dwStructSize = sizeof(urlComp);

    wchar_t hostName[256] = {0};
    wchar_t urlPath[1024] = {0};
    wchar_t extraInfo[256] = {0};
    urlComp.lpszHostName = hostName;
    urlComp.dwHostNameLength = 256;
    urlComp.lpszUrlPath = urlPath;
    urlComp.dwUrlPathLength = 1024;
    urlComp.lpszExtraInfo = extraInfo;
    urlComp.dwExtraInfoLength = 256;

    if (!WinHttpCrackUrl(wUrl.c_str(), 0, 0, &urlComp)) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST|url_parse", "HTTP请求",
                  "HTTP请求失败：URL解析失败，地址=%s，request_id=%s",
                  safeUrl.c_str(), requestIdForLog);
        return false;
    }

    int port = urlComp.nPort == 0 ? 80 : urlComp.nPort;

    HINTERNET hConnect = WinHttpConnect(m_hSession, hostName, (INTERNET_PORT)port, 0);
    if (!hConnect) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST|connect", "HTTP请求",
                  "HTTP请求失败：连接终端失败，主机=%s，端口=%d，request_id=%s",
                  PathHelper::WideToUtf8(hostName).c_str(), port, requestIdForLog);
        return false;
    }

    DWORD flags = (urlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
    std::wstring wPath = std::wstring(urlPath) + std::wstring(extraInfo);

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"POST", wPath.c_str(),
                                             nullptr, WINHTTP_NO_REFERER,
                                             WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
    if (!hRequest) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST|open_request", "HTTP请求",
            "HTTP请求失败：WinHttpOpenRequest 失败，request_id=%s", requestIdForLog);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    // 设置超时
    WinHttpSetTimeouts(hRequest, connectTimeoutMs, connectTimeoutMs,
                       requestTimeoutMs, requestTimeoutMs);

    // 设置请求头
    LPCWSTR headers = L"Content-Type: application/json; charset=utf-8\r\n";
    std::string utf8Body = body;

    if (!WinHttpSendRequest(hRequest, headers, (DWORD)-1,
                            (LPVOID)utf8Body.c_str(), (DWORD)utf8Body.size(),
                            (DWORD)utf8Body.size(), 0)) {
        DWORD err = GetLastError();
        LOG_ERROR_RATE_LIMITED("HTTP|POST|send", "HTTP请求",
            "HTTP请求失败：发送请求失败，错误码=%lu，request_id=%s", err, requestIdForLog);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    if (!WinHttpReceiveResponse(hRequest, nullptr)) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST|receive", "HTTP请求",
                  "HTTP请求失败：接收响应失败，错误码=%lu，request_id=%s",
                  GetLastError(), requestIdForLog);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    // 获取状态码
    DWORD statusCode = 0;
    DWORD statusCodeSize = sizeof(statusCode);
    WinHttpQueryHeaders(hRequest,
                         WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                         WINHTTP_HEADER_NAME_BY_INDEX,
                         &statusCode, &statusCodeSize, WINHTTP_NO_HEADER_INDEX);
    responseStatusCode = (int)statusCode;

    // 读取响应体
    DWORD bytesAvailable = 0;
    char buffer[4096];
    while (WinHttpQueryDataAvailable(hRequest, &bytesAvailable) && bytesAvailable > 0) {
        DWORD bytesToRead = (bytesAvailable < sizeof(buffer) - 1) ? bytesAvailable : sizeof(buffer) - 1;
        DWORD bytesRead = 0;
        if (WinHttpReadData(hRequest, buffer, bytesToRead, &bytesRead)) {
            responseBody.append(buffer, bytesRead);
        } else {
            break;
        }
    }

    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);

    const ULONGLONG elapsedMs = GetTickCount64() - startedAt;
    LOG_DEBUG("HTTP请求", "HTTP POST完成：地址=%s，状态=%d，request_id=%s，请求长度=%zu，响应长度=%zu，耗时=%llums",
             safeUrl.c_str(), responseStatusCode, requestIdForLog, body.size(), responseBody.size(),
             static_cast<unsigned long long>(elapsedMs));

    return true;
}

bool HttpClient::PostBinary(const std::string& url,
                            const std::string& body,
                            int connectTimeoutMs,
                            int requestTimeoutMs,
                            size_t maxResponseBytes,
                            std::string& responseBody,
                            int& responseStatusCode) {
    const ULONGLONG startedAt = GetTickCount64();
    const std::string requestId = JsonHelper::GetString(body, "request_id");
    const char* requestIdForLog = requestId.empty() ? "<无>" : requestId.c_str();
    const std::string safeUrl = SanitizeUrlForLog(url);
    responseBody.clear();
    responseStatusCode = 0;

    if (!m_hSession) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|session", "HTTP请求",
                  "二进制HTTP请求失败：WinHTTP会话未初始化，request_id=%s",
                  requestIdForLog);
        return false;
    }

    std::wstring wUrl = PathHelper::Utf8ToWide(url);
    URL_COMPONENTS urlComp = {0};
    urlComp.dwStructSize = sizeof(urlComp);

    wchar_t hostName[256] = {0};
    wchar_t urlPath[1024] = {0};
    wchar_t extraInfo[256] = {0};
    urlComp.lpszHostName = hostName;
    urlComp.dwHostNameLength = 256;
    urlComp.lpszUrlPath = urlPath;
    urlComp.dwUrlPathLength = 1024;
    urlComp.lpszExtraInfo = extraInfo;
    urlComp.dwExtraInfoLength = 256;

    if (!WinHttpCrackUrl(wUrl.c_str(), 0, 0, &urlComp)) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|url_parse", "HTTP请求",
                  "二进制HTTP请求失败：URL解析失败，地址=%s，request_id=%s",
                  safeUrl.c_str(), requestIdForLog);
        return false;
    }

    int port = urlComp.nPort == 0 ? 80 : urlComp.nPort;
    HINTERNET hConnect = WinHttpConnect(m_hSession, hostName,
                                        (INTERNET_PORT)port, 0);
    if (!hConnect) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|connect", "HTTP请求",
                  "二进制HTTP请求失败：连接失败，主机=%s，端口=%d，request_id=%s",
                  PathHelper::WideToUtf8(hostName).c_str(), port, requestIdForLog);
        return false;
    }

    DWORD flags = (urlComp.nScheme == INTERNET_SCHEME_HTTPS)
        ? WINHTTP_FLAG_SECURE : 0;
    std::wstring wPath = std::wstring(urlPath) + std::wstring(extraInfo);
    HINTERNET hRequest = WinHttpOpenRequest(
        hConnect, L"POST", wPath.c_str(), nullptr, WINHTTP_NO_REFERER,
        WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
    if (!hRequest) {
        LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|open_request", "HTTP请求",
            "二进制HTTP请求失败：WinHttpOpenRequest失败，request_id=%s",
                  requestIdForLog);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    WinHttpSetTimeouts(hRequest, connectTimeoutMs, connectTimeoutMs,
                       requestTimeoutMs, requestTimeoutMs);

    LPCWSTR headers = L"Content-Type: application/json; charset=utf-8\r\n";
    if (!WinHttpSendRequest(hRequest, headers, (DWORD)-1,
                            (LPVOID)body.data(), (DWORD)body.size(),
                            (DWORD)body.size(), 0)) {
        const DWORD error = GetLastError();
        if (error == ERROR_WINHTTP_TIMEOUT) responseStatusCode = -2;
        LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|send", "HTTP请求",
                  "二进制HTTP请求失败：发送请求失败，错误码=%lu，request_id=%s",
                  error, requestIdForLog);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    if (!WinHttpReceiveResponse(hRequest, nullptr)) {
        const DWORD error = GetLastError();
        if (error == ERROR_WINHTTP_TIMEOUT) responseStatusCode = -2;
        LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|receive", "HTTP请求",
                  "二进制HTTP请求失败：接收响应失败，错误码=%lu，request_id=%s",
                  error, requestIdForLog);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    DWORD statusCode = 0;
    DWORD statusCodeSize = sizeof(statusCode);
    WinHttpQueryHeaders(hRequest,
                        WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                        WINHTTP_HEADER_NAME_BY_INDEX, &statusCode,
                        &statusCodeSize, WINHTTP_NO_HEADER_INDEX);
    responseStatusCode = static_cast<int>(statusCode);

    DWORD bytesAvailable = 0;
    char buffer[4096];
    while (true) {
        if (!WinHttpQueryDataAvailable(hRequest, &bytesAvailable)) {
            const DWORD error = GetLastError();
            if (error == ERROR_WINHTTP_TIMEOUT) responseStatusCode = -2;
            LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|read_available", "HTTP请求",
                      "二进制HTTP响应长度查询失败：错误码=%lu，地址=%s，"
                      "request_id=%s",
                      error, safeUrl.c_str(), requestIdForLog);
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            return false;
        }
        if (bytesAvailable == 0) break;

        if (maxResponseBytes > 0 &&
            (responseBody.size() > maxResponseBytes ||
             static_cast<size_t>(bytesAvailable) >
                 maxResponseBytes - responseBody.size())) {
            responseStatusCode = -1;
            LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|response_too_large", "HTTP请求",
                      "二进制HTTP响应超过大小限制：地址=%s，状态=%d，"
                      "request_id=%s，限制=%zu，已接收=%zu，待接收=%lu",
                      safeUrl.c_str(), responseStatusCode, requestIdForLog,
                      maxResponseBytes, responseBody.size(), bytesAvailable);
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            return false;
        }

        DWORD bytesToRead = (std::min)(bytesAvailable,
                                       static_cast<DWORD>(sizeof(buffer)));
        DWORD bytesRead = 0;
        if (!WinHttpReadData(hRequest, buffer, bytesToRead, &bytesRead)) {
            const DWORD error = GetLastError();
            if (error == ERROR_WINHTTP_TIMEOUT) responseStatusCode = -2;
            LOG_ERROR_RATE_LIMITED("HTTP|POST_BINARY|read", "HTTP请求",
                      "二进制HTTP响应读取失败：错误码=%lu，地址=%s，"
                      "request_id=%s",
                      error, safeUrl.c_str(), requestIdForLog);
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            return false;
        }
        if (bytesRead == 0) break;
        responseBody.append(buffer, bytesRead);
    }

    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);

    const ULONGLONG elapsedMs = GetTickCount64() - startedAt;
    LOG_DEBUG("HTTP请求", "HTTP二进制POST完成：地址=%s，状态=%d，request_id=%s，"
              "请求长度=%zu，响应长度=%zu，耗时=%llums",
              safeUrl.c_str(), responseStatusCode, requestIdForLog, body.size(),
              responseBody.size(), static_cast<unsigned long long>(elapsedMs));
    return true;
}

bool HttpClient::Get(const std::string& url,
                     int connectTimeoutMs,
                     int requestTimeoutMs,
                     std::string& responseBody,
                     int& responseStatusCode,
                     bool quiet) {
    const ULONGLONG startedAt = GetTickCount64();
    const std::string safeUrl = SanitizeUrlForLog(url);
    responseBody.clear();
    responseStatusCode = 0;

    if (!m_hSession) {
        if (!quiet)
            LOG_WARN_RATE_LIMITED("HTTP|GET|session", "HTTP请求",
                "HTTP GET失败：WinHTTP session 未初始化，url=%s", safeUrl.c_str());
        return false;
    }

    std::wstring wUrl = PathHelper::Utf8ToWide(url);
    URL_COMPONENTS urlComp = {0};
    urlComp.dwStructSize = sizeof(urlComp);

    wchar_t hostName[256] = {0};
    wchar_t urlPath[1024] = {0};
    urlComp.lpszHostName = hostName;
    urlComp.dwHostNameLength = 256;
    urlComp.lpszUrlPath = urlPath;
    urlComp.dwUrlPathLength = 1024;

    if (!WinHttpCrackUrl(wUrl.c_str(), 0, 0, &urlComp)) {
        if (!quiet)
            LOG_WARN_RATE_LIMITED("HTTP|GET|url_parse", "HTTP请求",
                "HTTP GET失败：URL解析失败，url=%s，错误码=%lu", safeUrl.c_str(), GetLastError());
        return false;
    }

    int port = urlComp.nPort == 0 ? 80 : urlComp.nPort;

    HINTERNET hConnect = WinHttpConnect(m_hSession, hostName, (INTERNET_PORT)port, 0);
    if (!hConnect) {
        if (!quiet)
            LOG_WARN_RATE_LIMITED("HTTP|GET|connect", "HTTP请求",
                "HTTP GET失败：创建连接失败，url=%s，错误码=%lu", safeUrl.c_str(), GetLastError());
        return false;
    }

    DWORD flags = (urlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"GET", urlPath,
                                             nullptr, WINHTTP_NO_REFERER,
                                             WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
    if (!hRequest) {
        if (!quiet)
            LOG_WARN_RATE_LIMITED("HTTP|GET|open_request", "HTTP请求",
                "HTTP GET失败：创建请求失败，url=%s，错误码=%lu", safeUrl.c_str(), GetLastError());
        WinHttpCloseHandle(hConnect);
        return false;
    }

    WinHttpSetTimeouts(hRequest, connectTimeoutMs, connectTimeoutMs,
                       requestTimeoutMs, requestTimeoutMs);

    if (!WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                            WINHTTP_NO_REQUEST_DATA, 0, 0, 0)) {
        if (!quiet)
            LOG_WARN_RATE_LIMITED("HTTP|GET|send", "HTTP请求",
                "HTTP GET失败：发送请求失败，url=%s，错误码=%lu", safeUrl.c_str(), GetLastError());
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    if (!WinHttpReceiveResponse(hRequest, nullptr)) {
        if (!quiet)
            LOG_WARN_RATE_LIMITED("HTTP|GET|receive", "HTTP请求",
                "HTTP GET失败：接收响应失败，url=%s，错误码=%lu", safeUrl.c_str(), GetLastError());
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    DWORD statusCode = 0;
    DWORD statusCodeSize = sizeof(statusCode);
    WinHttpQueryHeaders(hRequest,
                         WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                         WINHTTP_HEADER_NAME_BY_INDEX,
                         &statusCode, &statusCodeSize, WINHTTP_NO_HEADER_INDEX);
    responseStatusCode = (int)statusCode;

    DWORD bytesAvailable = 0;
    char buffer[4096];
    while (WinHttpQueryDataAvailable(hRequest, &bytesAvailable) && bytesAvailable > 0) {
        DWORD bytesToRead = (bytesAvailable < sizeof(buffer) - 1) ? bytesAvailable : sizeof(buffer) - 1;
        DWORD bytesRead = 0;
        if (WinHttpReadData(hRequest, buffer, bytesToRead, &bytesRead)) {
            responseBody.append(buffer, bytesRead);
        } else {
            break;
        }
    }

    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);

    const ULONGLONG elapsedMs = GetTickCount64() - startedAt;
    LOG_DEBUG("HTTP请求", "HTTP GET完成：url=%s，status=%d，response_size=%zu，elapsed_ms=%llu",
              safeUrl.c_str(), responseStatusCode, responseBody.size(),
              static_cast<unsigned long long>(elapsedMs));

    return true;
}

} // HZCYKJTHardWare 命名空间结束
