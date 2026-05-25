#include "pch.h"
#include "http_client.h"
#include "logger.h"
#include "path_helper.h"

namespace HZCYKJTHardWare {

namespace {
class CriticalSectionGuard {
public:
    explicit CriticalSectionGuard(CRITICAL_SECTION* cs) : m_cs(cs) {
        EnterCriticalSection(m_cs);
    }
    ~CriticalSectionGuard() {
        LeaveCriticalSection(m_cs);
    }
private:
    CRITICAL_SECTION* m_cs;
};
}

HttpClient::HttpClient() {
    InitializeCriticalSection(&m_cs);
    m_hSession = WinHttpOpen(L"HZCYKJTHardWare-DLL/1.0",
                              WINHTTP_ACCESS_TYPE_NO_PROXY,
                              WINHTTP_NO_PROXY_NAME,
                              WINHTTP_NO_PROXY_BYPASS, 0);
    if (!m_hSession) {
        LOG_ERROR("HttpClient", "HTTP客户端初始化失败：WinHttpOpen error=%lu", GetLastError());
    }
}

HttpClient::~HttpClient() {
    if (m_hSession) {
        WinHttpCloseHandle(m_hSession);
        m_hSession = nullptr;
    }
    DeleteCriticalSection(&m_cs);
}

bool HttpClient::PostJson(const std::string& url,
                          const std::string& body,
                          int connectTimeoutMs,
                          int requestTimeoutMs,
                          std::string& responseBody,
                          int& responseStatusCode) {
    responseBody.clear();
    responseStatusCode = 0;

    if (!m_hSession) {
        LOG_ERROR("HttpClient", "HTTP请求失败：WinHTTP session 未初始化");
        return false;
    }

    CriticalSectionGuard guard(&m_cs);

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
        LOG_ERROR("HttpClient", "HTTP请求失败：URL解析失败，url=%s", url.c_str());
        return false;
    }

    int port = urlComp.nPort == 0 ? 80 : urlComp.nPort;

    HINTERNET hConnect = WinHttpConnect(m_hSession, hostName, (INTERNET_PORT)port, 0);
    if (!hConnect) {
        LOG_ERROR("HttpClient", "HTTP请求失败：连接终端失败，host=%s，port=%d", PathHelper::WideToUtf8(hostName).c_str(), port);
        return false;
    }

    DWORD flags = (urlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
    std::wstring wPath = std::wstring(urlPath) + std::wstring(extraInfo);

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"POST", wPath.c_str(),
                                             nullptr, WINHTTP_NO_REFERER,
                                             WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
    if (!hRequest) {
        LOG_ERROR("HttpClient", "HTTP请求失败：WinHttpOpenRequest 失败");
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
        LOG_ERROR("HttpClient", "HTTP请求失败：发送请求失败，error=%lu", err);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    if (!WinHttpReceiveResponse(hRequest, nullptr)) {
        LOG_ERROR("HttpClient", "HTTP请求失败：接收响应失败，error=%lu", GetLastError());
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

    LOG_DEBUG("HttpClient", "HTTP POST完成：url=%s，status=%d，response_size=%zu",
              url.c_str(), responseStatusCode, responseBody.size());

    return true;
}

bool HttpClient::Get(const std::string& url,
                     int connectTimeoutMs,
                     int requestTimeoutMs,
                     std::string& responseBody,
                     int& responseStatusCode) {
    responseBody.clear();
    responseStatusCode = 0;

    if (!m_hSession) return false;

    CriticalSectionGuard guard(&m_cs);

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
        return false;
    }

    int port = urlComp.nPort == 0 ? 80 : urlComp.nPort;

    HINTERNET hConnect = WinHttpConnect(m_hSession, hostName, (INTERNET_PORT)port, 0);
    if (!hConnect) return false;

    DWORD flags = (urlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"GET", urlPath,
                                             nullptr, WINHTTP_NO_REFERER,
                                             WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
    if (!hRequest) {
        WinHttpCloseHandle(hConnect);
        return false;
    }

    WinHttpSetTimeouts(hRequest, connectTimeoutMs, connectTimeoutMs,
                       requestTimeoutMs, requestTimeoutMs);

    if (!WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                            WINHTTP_NO_REQUEST_DATA, 0, 0, 0)) {
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return false;
    }

    if (!WinHttpReceiveResponse(hRequest, nullptr)) {
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

    return true;
}

} // namespace HZCYKJTHardWare
