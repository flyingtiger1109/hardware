#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 请求状态
enum class RequestStatus {
    Pending,
    Accepted,       // 终端已受理
    CallbackReceived,
    Completed,
    Timeout,
    Cancelled,      // EndProcess 导致
    Expired,        // SwitchTerminal 导致
    Failed
};

// 单次请求会话
struct RequestSession {
    std::string request_id;
    std::string resource_type;   // face_image / fingerprint_image / ocr_document
    std::string save_dir;
    std::string terminal_base_url;
    int terminal_index = 0;
    int64_t start_time_ms = 0;
    int timeout_ms = 15000;
    RequestStatus status = RequestStatus::Pending;

    // 回调结果数据
    std::string callback_body;
    bool callback_received = false;
};

// 请求会话管理器：管理所有异步请求的生命周期
class RequestSessionManager {
public:
    static RequestSessionManager& Instance();

    // 创建新的请求会话，返回 request_id
    std::string CreateSession(const std::string& resourceType,
                              const std::string& saveDir,
                              int timeoutMs);

    // 标记请求已被终端受理
    bool MarkAccepted(const std::string& requestId);

    // 标记收到回调
    bool MarkCallbackReceived(const std::string& requestId,
                              const std::string& resourceType,
                              const std::string& callbackBody);

    void MarkCompleted(const std::string& requestId);
    void MarkCompleted(const std::string& requestId,
                       const std::string& resourceType);

    bool IsRecentlyCompleted(const std::string& requestId,
                             const std::string& resourceType);

    // 获取会话
    std::shared_ptr<RequestSession> GetSession(const std::string& requestId);

    // 检查超时，返回超时的请求列表
    std::vector<std::shared_ptr<RequestSession>> CheckTimeouts();

    // 取消所有 pending 请求（EndProcess）
    void CancelAll();

    // 将所有旧请求标记为过期（SwitchTerminal）
    void ExpireAllForTerminalSwitch();

    void CancelAllForCallbackReset();

    // 获取所有 pending 请求数
    int GetPendingCount() const;

private:
    RequestSessionManager();
    ~RequestSessionManager();
    RequestSessionManager(const RequestSessionManager&) = delete;
    RequestSessionManager& operator=(const RequestSessionManager&) = delete;

    std::string GenerateRequestId(const std::string& prefix);
    void PruneCompletedLocked(int64_t nowMs);

    mutable CRITICAL_SECTION m_cs;
    std::map<std::string, std::shared_ptr<RequestSession>> m_sessions;
    using CompletedRequestKey = std::pair<std::string, std::string>;
    std::map<CompletedRequestKey, int64_t> m_completedRequests;
    int m_seq = 0;
};

} // namespace HZCYKJTHardWare
