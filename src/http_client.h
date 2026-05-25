#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// HTTP 客户端：支持 POST JSON
class HttpClient {
public:
    HttpClient();
    ~HttpClient();

    // 发送 POST JSON 请求
    // url: 完整请求 URL
    // body: JSON 请求体
    // connectTimeoutMs: 连接超时（毫秒）
    // requestTimeoutMs: 请求总超时（毫秒）
    // responseBody: 输出响应体
    // responseStatusCode: 输出 HTTP 状态码
    // 返回 true 表示 HTTP 请求完成（不管状态码），false 表示网络错误
    bool PostJson(const std::string& url,
                  const std::string& body,
                  int connectTimeoutMs,
                  int requestTimeoutMs,
                  std::string& responseBody,
                  int& responseStatusCode);

    // 发送 GET 请求（用于终端状态检测）
    bool Get(const std::string& url,
             int connectTimeoutMs,
             int requestTimeoutMs,
             std::string& responseBody,
             int& responseStatusCode);

private:
    // WinHTTP session handle
    HINTERNET m_hSession = nullptr;
    CRITICAL_SECTION m_cs;
};

} // namespace HZCYKJTHardWare
