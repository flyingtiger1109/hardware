#include "pch.h"
#include "callback_server.h"
#include "event_dispatcher.h"
#include "json_helper.h"
#include "logger.h"

#include <limits>

namespace HZCYKJTHardWare {

namespace {

constexpr size_t kMaxHeaderSize = 16 * 1024;
constexpr size_t kMaxBodySize = 10 * 1024 * 1024;
constexpr int kReceiveTimeoutMs = 5000;

enum class ContentLengthStatus {
    Missing,
    Empty,
    Negative,
    NonNumeric,
    Duplicate,
    TooLarge,
    Valid,
};

bool HeaderNameEquals(const std::string& name, const char* expected) {
    size_t expectedLength = strlen(expected);
    if (name.size() != expectedLength) return false;

    for (size_t i = 0; i < name.size(); ++i) {
        char actual = name[i];
        char expectedChar = expected[i];
        if (actual >= 'A' && actual <= 'Z') actual = static_cast<char>(actual - 'A' + 'a');
        if (expectedChar >= 'A' && expectedChar <= 'Z') {
            expectedChar = static_cast<char>(expectedChar - 'A' + 'a');
        }
        if (actual != expectedChar) return false;
    }
    return true;
}

std::string TrimHeaderValue(const std::string& value) {
    size_t first = value.find_first_not_of(" \t");
    if (first == std::string::npos) return std::string();
    size_t last = value.find_last_not_of(" \t");
    return value.substr(first, last - first + 1);
}

bool GetHeaderValue(const std::string& header,
                    const char* headerName,
                    std::string& value,
                    bool& found,
                    bool& duplicate) {
    value.clear();
    found = false;
    duplicate = false;

    size_t lineStart = 0;
    while (lineStart <= header.size()) {
        size_t lineEnd = header.find("\r\n", lineStart);
        if (lineEnd == std::string::npos) lineEnd = header.size();

        size_t colon = header.find(':', lineStart);
        if (colon != std::string::npos && colon < lineEnd) {
            size_t nameStart = lineStart;
            while (nameStart < colon && (header[nameStart] == ' ' || header[nameStart] == '\t')) {
                ++nameStart;
            }
            size_t nameEnd = colon;
            while (nameEnd > nameStart && (header[nameEnd - 1] == ' ' || header[nameEnd - 1] == '\t')) {
                --nameEnd;
            }

            const std::string name = header.substr(nameStart, nameEnd - nameStart);
            if (HeaderNameEquals(name, headerName)) {
                if (found) {
                    duplicate = true;
                } else {
                    found = true;
                    value = TrimHeaderValue(header.substr(colon + 1, lineEnd - colon - 1));
                }
            }
        }

        if (lineEnd == header.size()) break;
        lineStart = lineEnd + 2;
    }

    return found;
}

ContentLengthStatus ParseContentLength(const std::string& header, size_t& contentLength) {
    std::string value;
    bool found = false;
    bool duplicate = false;
    GetHeaderValue(header, "Content-Length", value, found, duplicate);
    if (!found) return ContentLengthStatus::Missing;
    if (duplicate) return ContentLengthStatus::Duplicate;
    if (value.empty()) return ContentLengthStatus::Empty;
    if (value[0] == '-') return ContentLengthStatus::Negative;

    size_t parsed = 0;
    for (char ch : value) {
        if (ch < '0' || ch > '9') return ContentLengthStatus::NonNumeric;
        const size_t digit = static_cast<size_t>(ch - '0');
        if (parsed > ((std::numeric_limits<size_t>::max)() - digit) / 10) {
            contentLength = kMaxBodySize + 1;
            return ContentLengthStatus::TooLarge;
        }
        parsed = parsed * 10 + digit;
    }

    contentLength = parsed;
    if (contentLength > kMaxBodySize) return ContentLengthStatus::TooLarge;
    return ContentLengthStatus::Valid;
}

bool HasTransferEncoding(const std::string& header, std::string& value) {
    bool found = false;
    bool duplicate = false;
    GetHeaderValue(header, "Transfer-Encoding", value, found, duplicate);
    return found;
}

const char* SocketErrorKind(int errorCode) {
    if (errorCode == WSAETIMEDOUT) return "timeout";
    if (errorCode == WSAECONNRESET) return "connection_reset";
    if (errorCode == 0) return "send_zero";
    return "socket_error";
}

bool SendAll(SOCKET socket,
             const char* data,
             size_t length,
             size_t& sent,
             int& errorCode) {
    sent = 0;
    errorCode = 0;

    while (sent < length) {
        const size_t remaining = length - sent;
        const int sendLength = remaining > static_cast<size_t>((std::numeric_limits<int>::max)())
            ? (std::numeric_limits<int>::max)()
            : static_cast<int>(remaining);
        const int result = send(socket, data + sent, sendLength, 0);
        if (result == SOCKET_ERROR) {
            errorCode = WSAGetLastError();
            return false;
        }
        if (result == 0) return false;
        sent += static_cast<size_t>(result);
    }

    return true;
}

void SendHttpResponse(SOCKET socket, const char* statusLine, const char* responseBody) {
    char response[320] = {0};
    const int responseLength = _snprintf_s(
        response, sizeof(response), _TRUNCATE,
        "HTTP/1.1 %s\r\n"
        "Content-Type: application/json\r\n"
        "Content-Length: %zu\r\n"
        "Connection: close\r\n\r\n%s",
        statusLine, strlen(responseBody), responseBody);
    if (responseLength <= 0) return;

    size_t sent = 0;
    int errorCode = 0;
    if (!SendAll(socket, response, static_cast<size_t>(responseLength), sent, errorCode)) {
        LOG_WARN("回调服务",
                 "HTTP响应发送失败：Sent=%zu Total=%d Error=%d Type=%s",
                 sent, responseLength, errorCode, SocketErrorKind(errorCode));
    }
}

} // anonymous namespace

CallbackServer& CallbackServer::Instance() {
    static CallbackServer* instance = new CallbackServer();
    return *instance;
}

int CallbackServer::Start(const std::string& host, int port) {
    if (m_running) {
        LOG_DEBUG("回调服务", "硬件控制程序回调接收服务已在运行");
        return HZCYKJTHardWare_RET_OK;
    }

    m_host = host;
    m_port = port;

    // 初始化 Winsock
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        LOG_ERROR("回调服务", "硬件控制程序回调接收服务启动失败：WSAStartup失败");
        return HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED;
    }

    m_listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (m_listenSocket == INVALID_SOCKET) {
        LOG_ERROR("回调服务", "硬件控制程序回调接收服务启动失败：socket()失败，错误码=%d", WSAGetLastError());
        WSACleanup();
        return HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED;
    }

    // 独占端口，确保端口已占用时 bind 立即失败。
    int exclusive = 1;
    if (setsockopt(m_listenSocket, SOL_SOCKET, SO_EXCLUSIVEADDRUSE,
                   (const char*)&exclusive, sizeof(exclusive)) == SOCKET_ERROR) {
        LOG_WARN("回调服务", "设置回调端口独占选项失败：错误码=%d", WSAGetLastError());
    }

    sockaddr_in addr = {0};
    addr.sin_family = AF_INET;
    addr.sin_port = htons((u_short)port);

    if (host == "0.0.0.0" || host.empty()) {
        addr.sin_addr.s_addr = INADDR_ANY;
    } else {
        const int inetPtonResult = inet_pton(AF_INET, host.c_str(), &addr.sin_addr);
        int errorCode = 0;
        if (inetPtonResult == -1) errorCode = WSAGetLastError();
        if (inetPtonResult != 1) {
            LOG_ERROR("回调服务",
                      "硬件控制程序回调接收服务启动失败：监听地址无效，host=%s，inet_pton_result=%d，错误码=%d",
                      host.c_str(), inetPtonResult, errorCode);
            SOCKET listenSocket = m_listenSocket.exchange(INVALID_SOCKET);
            if (listenSocket != INVALID_SOCKET) closesocket(listenSocket);
            m_host.clear();
            m_port = 0;
            WSACleanup();
            return HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED;
        }
    }

    if (bind(m_listenSocket, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
        LOG_ERROR("回调服务", "硬件控制程序回调接收服务启动失败：bind(%s:%d)失败，错误码=%d", host.c_str(), port, WSAGetLastError());
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
        WSACleanup();
        return HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED;
    }

    if (listen(m_listenSocket, SOMAXCONN) == SOCKET_ERROR) {
        LOG_ERROR("回调服务", "硬件控制程序回调接收服务启动失败：listen()失败，错误码=%d", WSAGetLastError());
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
        WSACleanup();
        return HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED;
    }

    // 获取实际端口（如果 port 为 0 则系统自动分配）
    if (port == 0) {
        sockaddr_in boundAddr;
        int addrLen = sizeof(boundAddr);
        getsockname(m_listenSocket, (sockaddr*)&boundAddr, &addrLen);
        m_port = ntohs(boundAddr.sin_port);
    }

    m_running = true;
    m_thread = std::make_unique<std::thread>(&CallbackServer::ServerThread, this);

    LOG_INFO("回调服务", "硬件控制程序回调接收服务已启动：listen=%s:%d", host.c_str(), (int)m_port);
    return HZCYKJTHardWare_RET_OK;
}

bool CallbackServer::Stop(int timeoutMs) {
    m_running = false;

    // 同时关闭监听 Socket 和活动 Socket，使 accept/recv 立即中断
    SOCKET listenSocket = m_listenSocket.exchange(INVALID_SOCKET);
    if (listenSocket != INVALID_SOCKET) {
        closesocket(listenSocket);
    }
    SOCKET clientSocket = m_clientSocket.exchange(INVALID_SOCKET);
    if (clientSocket != INVALID_SOCKET) {
        shutdown(clientSocket, SD_BOTH);
        closesocket(clientSocket);
    }

    if (m_thread && m_thread->joinable()) {
        if (m_thread->get_id() == std::this_thread::get_id()) {
            LOG_ERROR("回调服务", "禁止在回调接收线程内停止回调服务");
            return false;
        }
        DWORD waitResult = WaitForSingleObject(
            static_cast<HANDLE>(m_thread->native_handle()),
            static_cast<DWORD>(timeoutMs > 0 ? timeoutMs : 1));
        if (waitResult != WAIT_OBJECT_0) {
            LOG_ERROR("回调服务", "回调接收线程停止超时：timeout_ms=%d", timeoutMs);
            return false;
        }
        m_thread->join();
    }
    m_thread.reset();

    WSACleanup();
    LOG_INFO("回调服务", "硬件控制程序回调接收服务已停止");
    return true;
}

bool CallbackServer::IsRunning() const {
    return m_running;
}

int CallbackServer::GetPort() const {
    return m_port;
}

bool CallbackServer::ParseHttpRequest(const std::string& raw,
                                       std::string& method,
                                       std::string& path,
                                       std::string& body) {
    // 分离头部和 body
    size_t bodyStart = raw.find("\r\n\r\n");
    if (bodyStart == std::string::npos) return false;

    std::string header = raw.substr(0, bodyStart);
    body = raw.substr(bodyStart + 4);

    // 解析第一行
    size_t lineEnd = header.find("\r\n");
    if (lineEnd == std::string::npos) return false;

    std::string firstLine = header.substr(0, lineEnd);
    size_t sp1 = firstLine.find(' ');
    size_t sp2 = firstLine.find(' ', sp1 + 1);
    if (sp1 == std::string::npos) return false;

    method = firstLine.substr(0, sp1);
    path = (sp2 != std::string::npos) ? firstLine.substr(sp1 + 1, sp2 - sp1 - 1)
                                      : firstLine.substr(sp1 + 1);

    return true;
}

void CallbackServer::ServerThread() {
    LOG_DEBUG("回调服务", "硬件控制程序回调接收线程已启动");

    while (m_running) {
        sockaddr_in clientAddr;
        int clientAddrLen = sizeof(clientAddr);
        SOCKET clientSocket = accept(m_listenSocket, (sockaddr*)&clientAddr, &clientAddrLen);

        if (clientSocket == INVALID_SOCKET) {
            if (m_running) {
                const int errorCode = WSAGetLastError();
                LOG_ERROR("回调服务", "硬件控制程序回调接收失败：accept 错误码=%d", errorCode);
            }
            continue;
        }

        m_clientSocket.store(clientSocket);

        // 设置接收超时
        int timeout = kReceiveTimeoutMs;
        if (setsockopt(clientSocket, SOL_SOCKET, SO_RCVTIMEO,
                       (const char*)&timeout, sizeof(timeout)) == SOCKET_ERROR) {
            const int errorCode = WSAGetLastError();
            LOG_WARN("回调服务", "设置回调请求接收超时失败：错误码=%d", errorCode);
        }

        std::string rawRequest;
        rawRequest.reserve(kMaxHeaderSize);
        size_t headerEnd = std::string::npos;
        bool headerComplete = false;
        bool requestBodyComplete = false;
        const char* rejectionStatusLine = nullptr;
        const char* rejectionBody = nullptr;

        // 先循环读取到完整的 HTTP 头，避免 TCP 分片导致提前解析。
        while (m_running && !headerComplete) {
            char buf[4096];
            const int recvLen = recv(clientSocket, buf, sizeof(buf), 0);
            if (recvLen > 0) {
                rawRequest.append(buf, static_cast<size_t>(recvLen));
                headerEnd = rawRequest.find("\r\n\r\n");
                if (headerEnd != std::string::npos) {
                    const size_t headerSize = headerEnd + 4;
                    if (headerSize > kMaxHeaderSize) {
                        LOG_WARN("回调服务",
                                 "HTTP请求头过大，已拒绝：Received=%zu Limit=%zu",
                                 headerSize, kMaxHeaderSize);
                        rejectionStatusLine = "400 Bad Request";
                        rejectionBody = "{\"status\":\"invalid\"}";
                    } else {
                        headerComplete = true;
                    }
                    break;
                }

                if (rawRequest.size() > kMaxHeaderSize) {
                    LOG_WARN("回调服务",
                             "HTTP请求头过大，已拒绝：Received=%zu Limit=%zu",
                             rawRequest.size(), kMaxHeaderSize);
                    rejectionStatusLine = "400 Bad Request";
                    rejectionBody = "{\"status\":\"invalid\"}";
                    break;
                }
                continue;
            }

            if (!m_running) break;
            if (recvLen == 0) {
                LOG_WARN("回调服务",
                         "HTTP请求头接收不完整：Received=%zu Expected=header Error=peer_closed",
                         rawRequest.size());
            } else {
                const int errorCode = WSAGetLastError();
                LOG_WARN("回调服务",
                         "HTTP请求头接收失败：Received=%zu Expected=header Error=%d Type=%s",
                         rawRequest.size(), errorCode, SocketErrorKind(errorCode));
            }
            break;
        }

        if (m_running && headerComplete && rejectionStatusLine == nullptr) {
            const size_t headerSize = headerEnd + 4;
            const std::string header = rawRequest.substr(0, headerEnd);

            std::string transferEncoding;
            if (HasTransferEncoding(header, transferEncoding)) {
                LOG_WARN("回调服务",
                         "HTTP请求Transfer-Encoding不受支持，已拒绝：value=%s",
                         transferEncoding.empty() ? "<empty>" : transferEncoding.c_str());
                rejectionStatusLine = "400 Bad Request";
                rejectionBody = "{\"status\":\"invalid\"}";
            } else {
                size_t contentLength = 0;
                const ContentLengthStatus contentLengthStatus =
                    ParseContentLength(header, contentLength);
                if (contentLengthStatus == ContentLengthStatus::Missing) {
                    LOG_WARN("回调服务", "HTTP请求Content-Length缺失，已拒绝");
                    rejectionStatusLine = "400 Bad Request";
                    rejectionBody = "{\"status\":\"invalid\"}";
                } else if (contentLengthStatus != ContentLengthStatus::Valid &&
                           contentLengthStatus != ContentLengthStatus::TooLarge) {
                    const char* reason = "invalid";
                    if (contentLengthStatus == ContentLengthStatus::Empty) {
                        reason = "empty";
                    } else if (contentLengthStatus == ContentLengthStatus::Negative) {
                        reason = "negative";
                    } else if (contentLengthStatus == ContentLengthStatus::NonNumeric) {
                        reason = "non_numeric";
                    } else if (contentLengthStatus == ContentLengthStatus::Duplicate) {
                        reason = "duplicate";
                    }
                    LOG_WARN("回调服务",
                             "HTTP请求Content-Length无效，已拒绝：reason=%s",
                             reason);
                    rejectionStatusLine = "400 Bad Request";
                    rejectionBody = "{\"status\":\"invalid\"}";
                } else if (contentLengthStatus == ContentLengthStatus::TooLarge) {
                    LOG_ERROR("回调服务",
                              "硬件控制程序回调请求体过大，已拒绝：Content-Length>=%zu Limit=%zu",
                              contentLength, kMaxBodySize);
                    rejectionStatusLine = "413 Payload Too Large";
                    rejectionBody = "";
                } else {
                    size_t currentBodySize = rawRequest.size() > headerSize
                        ? rawRequest.size() - headerSize : 0;
                    if (currentBodySize > contentLength) {
                        // 当前模式为单请求、Connection: close；多余字节不进入本次 body。
                        rawRequest.resize(headerSize + contentLength);
                        currentBodySize = contentLength;
                    }

                    if (currentBodySize < contentLength) {
                        rawRequest.resize(headerSize + contentLength);
                        size_t receivedBody = currentBodySize;
                        char* dest = &rawRequest[0] + headerSize + currentBodySize;

                        LOG_DEBUG("回调服务",
                                  "继续读取HTTP请求体：remaining=%zu，total=%zu",
                                  contentLength - currentBodySize, contentLength);

                        while (receivedBody < contentLength) {
                            const size_t remaining = contentLength - receivedBody;
                            const int recvSize = remaining > static_cast<size_t>((std::numeric_limits<int>::max)())
                                ? (std::numeric_limits<int>::max)()
                                : static_cast<int>(remaining);
                            const int chunk = recv(clientSocket, dest, recvSize, 0);
                            if (chunk > 0) {
                                dest += chunk;
                                receivedBody += static_cast<size_t>(chunk);
                                continue;
                            }

                            if (!m_running) break;
                            if (chunk == 0) {
                                LOG_WARN("回调服务",
                                         "HTTP请求体接收不完整：Received=%zu Expected=%zu Error=peer_closed",
                                         receivedBody, contentLength);
                            } else {
                                const int errorCode = WSAGetLastError();
                                LOG_WARN("回调服务",
                                         "HTTP请求体接收失败：Received=%zu Expected=%zu Error=%d Type=%s",
                                         receivedBody, contentLength, errorCode,
                                         SocketErrorKind(errorCode));
                            }
                            break;
                        }
                        requestBodyComplete = receivedBody == contentLength;
                    } else {
                        requestBodyComplete = true;
                    }
                }
            }
        }

        if (m_running) {
            std::string method, path, body;
            bool parsed = false;
            bool accepted = false;
            if (rejectionStatusLine == nullptr && requestBodyComplete &&
                ParseHttpRequest(rawRequest, method, path, body)) {
                parsed = true;
                char remoteIp[INET_ADDRSTRLEN];
                inet_ntop(AF_INET, &clientAddr.sin_addr, remoteIp, sizeof(remoteIp));

                const std::string requestId = JsonHelper::GetString(body, "request_id");
                const std::string resourceType = JsonHelper::GetString(body, "resource_type");
                const std::string safePath = SanitizeUrlForLog(path);
                LOG_DEBUG("回调服务",
                          "收到硬件控制程序回调：路径=%s，request_id=%s，资源=%s，来源=%s，正文长度=%zu",
                          safePath.c_str(), requestId.empty() ? "<无>" : requestId.c_str(),
                          resourceType.empty() ? "<无>" : resourceType.c_str(),
                          remoteIp, body.size());
                CallbackData cbData;
                cbData.path = path;
                cbData.body = body;
                cbData.remote_addr = remoteIp;

                accepted = EventDispatcher::Instance().TryPostCallbackData(cbData);
            } else {
                LOG_WARN_RATE_LIMITED("CallbackServer|parse_failed", "回调服务",
                    "硬件控制程序回调请求解析失败：bytes=%zu", rawRequest.size());
            }

            const char* statusLine = rejectionStatusLine;
            const char* responseBody = rejectionBody;
            if (statusLine == nullptr) {
                if (!parsed) {
                    statusLine = "400 Bad Request";
                    responseBody = "{\"status\":\"invalid\"}";
                } else if (!accepted) {
                    statusLine = "503 Service Unavailable";
                    responseBody = "{\"status\":\"busy\"}";
                } else {
                    statusLine = "202 Accepted";
                    responseBody = "{\"status\":\"ok\"}";
                }
            }

            SendHttpResponse(clientSocket, statusLine, responseBody);
        }

        SOCKET ownedClient = m_clientSocket.exchange(INVALID_SOCKET);
        if (ownedClient != INVALID_SOCKET) closesocket(ownedClient);
    }

    LOG_DEBUG("回调服务", "硬件控制程序回调接收线程已退出");
}

} // HZCYKJTHardWare 命名空间结束
