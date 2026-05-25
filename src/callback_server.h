#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 回调数据结构
struct CallbackData {
    std::string path;          // 回调路径
    std::string body;          // JSON 请求体
    std::string remote_addr;   // 来源地址
};

// DLL 内部 HTTP 回调服务器
class CallbackServer {
public:
    static CallbackServer& Instance();

    // 启动服务器
    // host: 监听地址（如 0.0.0.0）
    // port: 监听端口
    // 返回 HZCYKJTHardWare_RET_OK 或错误码
    int Start(const std::string& host, int port);

    // 停止服务器
    void Stop();

    // 是否运行中
    bool IsRunning() const;

    // 获取实际监听端口
    int GetPort() const;

private:
    CallbackServer() = default;
    CallbackServer(const CallbackServer&) = delete;
    CallbackServer& operator=(const CallbackServer&) = delete;

    void ServerThread();

    // 解析 HTTP 请求
    bool ParseHttpRequest(const std::string& raw, std::string& method,
                          std::string& path, std::string& body);

    std::atomic<bool> m_running{false};
    std::atomic<int> m_port{0};
    std::string m_host;
    std::unique_ptr<std::thread> m_thread;
    SOCKET m_listenSocket = INVALID_SOCKET;
};

} // namespace HZCYKJTHardWare
