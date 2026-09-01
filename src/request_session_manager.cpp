#include "pch.h"
#include "request_session_manager.h"
#include "logger.h"
#include "path_helper.h"
#include "hzsjkjt_context.h"

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

static int64_t NowMs() {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count();
}

static const int64_t kCompletedKeepMs = 10 * 60 * 1000;
// 保留足量复合回调键用于重试去重，同时控制对 x86 地址空间的占用
static const size_t kMaxCompletedRequests = 8192;

RequestSessionManager& RequestSessionManager::Instance() {
    static RequestSessionManager* instance = new RequestSessionManager();
    return *instance;
}

RequestSessionManager::RequestSessionManager() { InitializeCriticalSection(&m_cs); }
RequestSessionManager::~RequestSessionManager() { DeleteCriticalSection(&m_cs); }

std::string RequestSessionManager::GenerateRequestId(const std::string& prefix) {
    CriticalSectionGuard guard(&m_cs);
    m_seq++;
    std::string timestamp = PathHelper::GetTimestampString();
    char seqBuf[16];
    snprintf(seqBuf, sizeof(seqBuf), "%03d", m_seq);
    return prefix + "_" + timestamp + "_" + seqBuf;
}

std::string RequestSessionManager::CreateSession(const std::string& resourceType,
                                                   const std::string& saveDir,
                                                   int timeoutMs) {
    std::string prefix;
    if (resourceType == HZCYKJTHardWare_RESOURCE_FACE_IMAGE) {
        prefix = "HZCYKJTHardWare_FACE";
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE) {
        prefix = "HZCYKJTHardWare_FP";
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT) {
        prefix = "HZCYKJTHardWare_OCR";
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_IRIS_IMAGE) {
        prefix = "HZCYKJTHardWare_IRIS";
    } else if (resourceType == HZCYKJTHardWare_RESOURCE_NFC_CARD) {
        prefix = "HZCYKJTHardWare_NFC";
    } else {
        prefix = "HZCYKJTHardWare_REQ";
    }

    std::string requestId = GenerateRequestId(prefix);

    auto session = std::make_shared<RequestSession>();
    session->request_id = requestId;
    session->resource_type = resourceType;
    session->save_dir = saveDir;
    session->start_time_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count();
    session->timeout_ms = timeoutMs;

    auto& ctx = HzsjkjtContext::Instance();
    {
        auto lock = ReadLock();
        session->terminal_base_url = ctx.current_terminal_base_url;
        session->terminal_index = ctx.current_terminal_index;
    }

    {
        CriticalSectionGuard guard(&m_cs);
        m_sessions[requestId] = session;
    }

    LOG_DEBUG("RequestSession", "创建异步请求会话：request_id=%s，timeout=%dms，terminal=%s",
             requestId.c_str(), timeoutMs,
             SanitizeUrlForLog(session->terminal_base_url).c_str());

    return requestId;
}

bool RequestSessionManager::MarkAccepted(const std::string& requestId) {
    CriticalSectionGuard guard(&m_cs);
    auto it = m_sessions.find(requestId);
    if (it == m_sessions.end()) {
        // 快速回调可能在同步提交响应返回前完成，此时应视为已受理，不返回忙状态。
        PruneCompletedLocked(NowMs());
        const auto completed = m_completedRequests.lower_bound(
            CompletedRequestKey(requestId, std::string()));
        return completed != m_completedRequests.end() &&
            completed->first.first == requestId;
    }
    if (it->second->status == RequestStatus::CallbackReceived ||
        it->second->status == RequestStatus::Completed ||
        it->second->status == RequestStatus::Accepted) {
        return true;
    }
    if (it->second->status != RequestStatus::Pending) return false;
    it->second->status = RequestStatus::Accepted;
    LOG_DEBUG("RequestSession", "异步请求已受理：request_id=%s", requestId.c_str());
    return true;
}

bool RequestSessionManager::MarkCallbackReceived(const std::string& requestId,
                                                   const std::string& resourceType,
                                                   const std::string& callbackBody) {
    CriticalSectionGuard guard(&m_cs);
    PruneCompletedLocked(NowMs());
    const CompletedRequestKey completedKey(requestId, resourceType);
    if (m_completedRequests.find(completedKey) != m_completedRequests.end()) {
        LOG_WARN("RequestSession", "重复回调已忽略：request_id=%s，原因=已完成", requestId.c_str());
        return false;
    }
    auto it = m_sessions.find(requestId);
    if (it == m_sessions.end()) return false;
    if (it->second->resource_type != resourceType) {
        LOG_WARN("RequestSession", "回调资源类型不匹配：request_id=%s，expected=%s，actual=%s",
                 requestId.c_str(), it->second->resource_type.c_str(), resourceType.c_str());
        return false;
    }
    if (it->second->status != RequestStatus::Pending &&
        it->second->status != RequestStatus::Accepted) {
        LOG_WARN("RequestSession", "重复回调已忽略：request_id=%s，status=%d",
                 requestId.c_str(), static_cast<int>(it->second->status));
        return false;
    }
    it->second->status = RequestStatus::CallbackReceived;
    it->second->callback_body = callbackBody;
    it->second->callback_received = true;
    LOG_DEBUG("RequestSession", "异步请求已收到终端回调：request_id=%s", requestId.c_str());
    return true;
}

void RequestSessionManager::MarkCompleted(const std::string& requestId) {
    if (requestId.empty()) return;
    CriticalSectionGuard guard(&m_cs);
    int64_t now = NowMs();
    std::string resourceType;
    auto it = m_sessions.find(requestId);
    if (it != m_sessions.end()) {
        resourceType = it->second->resource_type;
        it->second->status = RequestStatus::Completed;
        it->second->callback_body.clear();
        it->second->callback_received = false;
        m_sessions.erase(it);
    }
    if (!resourceType.empty()) {
        m_completedRequests[CompletedRequestKey(requestId, resourceType)] = now;
    }
    PruneCompletedLocked(now);
    LOG_DEBUG("RequestSession", "请求会话已完成并清理：request_id=%s", requestId.c_str());
}

void RequestSessionManager::MarkCompleted(const std::string& requestId,
                                          const std::string& resourceType) {
    if (requestId.empty() || resourceType.empty()) return;
    CriticalSectionGuard guard(&m_cs);
    const int64_t now = NowMs();
    auto it = m_sessions.find(requestId);
    if (it != m_sessions.end() && it->second->resource_type == resourceType) {
        it->second->status = RequestStatus::Completed;
        it->second->callback_body.clear();
        it->second->callback_received = false;
        m_sessions.erase(it);
    }
    m_completedRequests[CompletedRequestKey(requestId, resourceType)] = now;
    PruneCompletedLocked(now);
    LOG_DEBUG("RequestSession", "请求会话已完成并清理：request_id=%s，resource=%s",
              requestId.c_str(), resourceType.c_str());
}

bool RequestSessionManager::IsRecentlyCompleted(const std::string& requestId,
                                                 const std::string& resourceType) {
    if (requestId.empty() || resourceType.empty()) return false;
    CriticalSectionGuard guard(&m_cs);
    int64_t now = NowMs();
    PruneCompletedLocked(now);
    return m_completedRequests.find(CompletedRequestKey(requestId, resourceType)) !=
           m_completedRequests.end();
}

std::shared_ptr<RequestSession> RequestSessionManager::GetSession(const std::string& requestId) {
    CriticalSectionGuard guard(&m_cs);
    auto it = m_sessions.find(requestId);
    if (it == m_sessions.end()) return nullptr;
    return it->second;
}

std::vector<std::shared_ptr<RequestSession>> RequestSessionManager::CheckTimeouts() {
    std::vector<std::shared_ptr<RequestSession>> timeouts;

    int64_t now = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count();

    CriticalSectionGuard guard(&m_cs);
    for (auto& kv : m_sessions) {
        auto& s = kv.second;
        if (s->status == RequestStatus::Pending || s->status == RequestStatus::Accepted) {
            int64_t elapsed = now - s->start_time_ms;
            if (elapsed > s->timeout_ms) {
                s->status = RequestStatus::Timeout;
                timeouts.push_back(s);
                LOG_WARN("RequestSession", "异步请求超时：request_id=%s，elapsed=%lldms，timeout=%dms",
                         s->request_id.c_str(), elapsed, s->timeout_ms);
            }
        }
    }

    return timeouts;
}

void RequestSessionManager::CancelAll() {
    CriticalSectionGuard guard(&m_cs);
    int64_t now = NowMs();
    for (auto it = m_sessions.begin(); it != m_sessions.end(); ) {
        auto& kv = *it;
        if (kv.second->status == RequestStatus::Pending ||
            kv.second->status == RequestStatus::Accepted) {
            kv.second->status = RequestStatus::Cancelled;
            LOG_DEBUG("RequestSession", "异步请求已取消：request_id=%s，原因=SDK或回调服务停止", kv.first.c_str());
            m_completedRequests[CompletedRequestKey(
                kv.first, kv.second->resource_type)] = now;
            it = m_sessions.erase(it);
        } else {
            ++it;
        }
    }
    PruneCompletedLocked(now);
}

void RequestSessionManager::ExpireAllForTerminalSwitch() {
    CriticalSectionGuard guard(&m_cs);
    int64_t now = NowMs();
    for (auto it = m_sessions.begin(); it != m_sessions.end(); ) {
        auto& kv = *it;
        if (kv.second->status == RequestStatus::Pending ||
            kv.second->status == RequestStatus::Accepted) {
            kv.second->status = RequestStatus::Expired;
            LOG_DEBUG("RequestSession", "异步请求已过期：request_id=%s，原因=终端切换", kv.first.c_str());
            m_completedRequests[CompletedRequestKey(
                kv.first, kv.second->resource_type)] = now;
            it = m_sessions.erase(it);
        } else {
            ++it;
        }
    }
    PruneCompletedLocked(now);
}

void RequestSessionManager::CancelAllForCallbackReset() {
    CriticalSectionGuard guard(&m_cs);
    int64_t now = NowMs();
    for (auto it = m_sessions.begin(); it != m_sessions.end(); ) {
        auto& kv = *it;
        if (kv.second->status == RequestStatus::Pending ||
            kv.second->status == RequestStatus::Accepted ||
            kv.second->status == RequestStatus::CallbackReceived) {
            kv.second->status = RequestStatus::Cancelled;
            kv.second->callback_body.clear();
            kv.second->callback_received = false;
            LOG_DEBUG("RequestSession", "异步请求已取消：request_id=%s，原因=第三方重新注册回调", kv.first.c_str());
            m_completedRequests[CompletedRequestKey(
                kv.first, kv.second->resource_type)] = now;
            it = m_sessions.erase(it);
        } else {
            ++it;
        }
    }
    PruneCompletedLocked(now);
}

void RequestSessionManager::PruneCompletedLocked(int64_t nowMs) {
    for (auto it = m_completedRequests.begin(); it != m_completedRequests.end(); ) {
        if (nowMs - it->second > kCompletedKeepMs) {
            it = m_completedRequests.erase(it);
        } else {
            ++it;
        }
    }

    while (m_completedRequests.size() > kMaxCompletedRequests) {
        auto oldest = m_completedRequests.begin();
        for (auto it = m_completedRequests.begin(); it != m_completedRequests.end(); ++it) {
            if (it->second < oldest->second) {
                oldest = it;
            }
        }
        m_completedRequests.erase(oldest);
    }
}

int RequestSessionManager::GetPendingCount() const {
    CriticalSectionGuard guard(&m_cs);
    int count = 0;
    for (const auto& kv : m_sessions) {
        if (kv.second->status == RequestStatus::Pending ||
            kv.second->status == RequestStatus::Accepted) {
            count++;
        }
    }
    return count;
}

} // HZCYKJTHardWare 命名空间结束
