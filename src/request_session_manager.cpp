#include "pch.h"
#include "request_session_manager.h"
#include "logger.h"
#include "path_helper.h"
#include "hzsjkjt_context.h"

namespace HZCYKJTHardWare {

namespace { RequestSessionManager* g_pReqSessMgr = nullptr; }

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

RequestSessionManager& RequestSessionManager::Instance() {
    if (!g_pReqSessMgr) g_pReqSessMgr = new RequestSessionManager();
    return *g_pReqSessMgr;
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
             requestId.c_str(), timeoutMs, session->terminal_base_url.c_str());

    return requestId;
}

bool RequestSessionManager::MarkAccepted(const std::string& requestId) {
    CriticalSectionGuard guard(&m_cs);
    auto it = m_sessions.find(requestId);
    if (it == m_sessions.end()) return false;
    it->second->status = RequestStatus::Accepted;
    LOG_DEBUG("RequestSession", "异步请求已受理：request_id=%s", requestId.c_str());
    return true;
}

bool RequestSessionManager::MarkCallbackReceived(const std::string& requestId,
                                                   const std::string& callbackBody) {
    CriticalSectionGuard guard(&m_cs);
    auto it = m_sessions.find(requestId);
    if (it == m_sessions.end()) return false;
    it->second->status = RequestStatus::CallbackReceived;
    it->second->callback_body = callbackBody;
    it->second->callback_received = true;
    LOG_DEBUG("RequestSession", "异步请求已收到终端回调：request_id=%s", requestId.c_str());
    return true;
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
    for (auto& kv : m_sessions) {
        if (kv.second->status == RequestStatus::Pending ||
            kv.second->status == RequestStatus::Accepted) {
            kv.second->status = RequestStatus::Cancelled;
            LOG_DEBUG("RequestSession", "异步请求已取消：request_id=%s，原因=流程结束", kv.first.c_str());
        }
    }
}

void RequestSessionManager::ExpireAllForTerminalSwitch() {
    CriticalSectionGuard guard(&m_cs);
    for (auto& kv : m_sessions) {
        if (kv.second->status == RequestStatus::Pending ||
            kv.second->status == RequestStatus::Accepted) {
            kv.second->status = RequestStatus::Expired;
            LOG_DEBUG("RequestSession", "异步请求已过期：request_id=%s，原因=终端切换", kv.first.c_str());
        }
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

} // namespace HZCYKJTHardWare
