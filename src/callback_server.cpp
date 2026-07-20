#include "pch.h"
#include "callback_server.h"
#include "event_dispatcher.h"
#include "logger.h"

namespace HZCYKJTHardWare {

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
        // 审查风险：未检查 inet_pton 返回值，非法地址可能使 addr 保持为 0.0.0.0 并扩大监听范围。
        // 建议解析失败时关闭 Socket、调用 WSACleanup，并返回配置错误。
        inet_pton(AF_INET, host.c_str(), &addr.sin_addr);
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
                LOG_ERROR("回调服务", "硬件控制程序回调接收失败：accept 错误码=%d", WSAGetLastError());
            }
            continue;
        }

        m_clientSocket.store(clientSocket);

        // 设置接收超时
        int timeout = 5000;
        setsockopt(clientSocket, SOL_SOCKET, SO_RCVTIMEO, (const char*)&timeout, sizeof(timeout));

        // 第一步：读取 HTTP 头部 + 初始 body（最多 16KB）
        char buf[16384];
        // 审查风险：TCP 不保证一次 recv 收到完整 HTTP 头；分片到达时会误判为无效请求。
        // 建议循环读取到 \r\n\r\n，并设置明确的请求头大小上限。
        int recvLen = recv(clientSocket, buf, sizeof(buf) - 1, 0);

        if (recvLen > 0) {
            buf[recvLen] = '\0';
            std::string rawRequest(buf, recvLen);

            // 第二步：提取 Content-Length，按需补读 body
            size_t headerEnd = rawRequest.find("\r\n\r\n");
            bool requestBodyComplete = (headerEnd != std::string::npos);
            if (headerEnd != std::string::npos) {
                std::string header = rawRequest.substr(0, headerEnd);
                size_t headerSize = headerEnd + 4;
                size_t currentBodySize = (rawRequest.size() > headerSize)
                    ? (rawRequest.size() - headerSize) : 0;

                int contentLength = -1;
                size_t clPos = header.find("Content-Length:");
                if (clPos == std::string::npos)
                    clPos = header.find("content-length:");
                if (clPos != std::string::npos) {
                    size_t valStart = header.find_first_not_of(" \t", clPos + 15);
                    if (valStart != std::string::npos) {
                        size_t valEnd = header.find("\r\n", valStart);
                        contentLength = atoi(header.substr(valStart,
                            (valEnd == std::string::npos) ? std::string::npos
                                                          : valEnd - valStart).c_str());
                    }
                }

                if (contentLength > 0 && static_cast<int>(currentBodySize) < contentLength) {
                    static const int MAX_BODY = 10 * 1024 * 1024; // 10MB
                    if (contentLength > MAX_BODY) {
                        LOG_ERROR("回调服务",
                                  "硬件控制程序回调请求体过大，已拒绝：Content-Length=%d", contentLength);
                        const char* tooLarge =
                            "HTTP/1.1 413 Payload Too Large\r\n"
                            "Content-Length: 0\r\n"
                            "Connection: close\r\n\r\n";
                        send(clientSocket, tooLarge, (int)strlen(tooLarge), 0);
                        SOCKET ownedClient = m_clientSocket.exchange(INVALID_SOCKET);
                        if (ownedClient != INVALID_SOCKET) closesocket(ownedClient);
                        continue;
                    }

                    rawRequest.resize(headerSize + contentLength);
                    char* dest = &rawRequest[0] + headerSize + currentBodySize;
                    size_t remaining = contentLength - currentBodySize;

                    LOG_DEBUG("回调服务",
                              "继续读取HTTP请求体：remaining=%zu，total=%d",
                              remaining, contentLength);

                    while (remaining > 0) {
                        int chunk = recv(clientSocket, dest, (int)remaining, 0);
                        if (chunk <= 0) {
                            LOG_WARN("回调服务",
                                     "HTTP请求体接收不完整：got=%zu，total=%d",
                                     contentLength - remaining, contentLength);
                            break;
                        }
                        dest += chunk;
                        remaining -= chunk;
                    }
                    requestBodyComplete = (remaining == 0);
                }
            }

            std::string method, path, body;
            bool parsed = false;
            bool accepted = false;
            if (requestBodyComplete && ParseHttpRequest(rawRequest, method, path, body)) {
                parsed = true;
                char remoteIp[INET_ADDRSTRLEN];
                inet_ntop(AF_INET, &clientAddr.sin_addr, remoteIp, sizeof(remoteIp));

                LOG_INFO("回调服务",
                         "收到硬件控制程序回调：path=%s，remote=%s，body_size=%zu",
                         path.c_str(), remoteIp, body.size());
                CallbackData cbData;
                cbData.path = path;
                cbData.body = body;
                cbData.remote_addr = remoteIp;

                accepted = EventDispatcher::Instance().TryPostCallbackData(cbData);
            } else {
                LOG_WARN("回调服务", "硬件控制程序回调请求解析失败：bytes=%zu", rawRequest.size());
            }

            const char* statusLine = nullptr;
            const char* responseBody = nullptr;
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

            char response[320] = {0};
            const int responseLength = _snprintf_s(
                response, sizeof(response), _TRUNCATE,
                "HTTP/1.1 %s\r\n"
                "Content-Type: application/json\r\n"
                "Content-Length: %zu\r\n"
                "Connection: close\r\n\r\n%s",
                statusLine, strlen(responseBody), responseBody);
            if (responseLength > 0) {
                send(clientSocket, response, responseLength, 0);
            }
        }

        SOCKET ownedClient = m_clientSocket.exchange(INVALID_SOCKET);
        if (ownedClient != INVALID_SOCKET) closesocket(ownedClient);
    }

LOG_DEBUG("回调服务", "硬件控制程序回调接收线程已退出");
}

} // HZCYKJTHardWare 命名空间结束
