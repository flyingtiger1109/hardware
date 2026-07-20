#include "pch.h"
#include "sdk_runtime.h"

namespace HZCYKJTHardWare {

SdkRuntime& SdkRuntime::Instance() {
    static SdkRuntime runtime;
    return runtime;
}

bool SdkRuntime::BeginInitialize(bool& shouldInitialize, int timeoutMs) {
    shouldInitialize = false;
    std::unique_lock<std::mutex> lock(mutex_);
    const auto deadline = std::chrono::steady_clock::now() +
        std::chrono::milliseconds(timeoutMs > 0 ? timeoutMs : 1);

    while (state_ == SdkLifecycleState::Initializing ||
           state_ == SdkLifecycleState::Releasing) {
        if (cv_.wait_until(lock, deadline) == std::cv_status::timeout) {
            return false;
        }
    }

    if (state_ == SdkLifecycleState::Running) {
        return true;
    }
    if (state_ == SdkLifecycleState::Faulted) {
        return false;
    }

    state_ = SdkLifecycleState::Initializing;
    while (activeCalls_ > 0) {
        if (cv_.wait_until(lock, deadline) == std::cv_status::timeout) {
            state_ = SdkLifecycleState::Stopped;
            cv_.notify_all();
            return false;
        }
    }

    shouldInitialize = true;
    return true;
}

void SdkRuntime::CompleteInitialize(bool success) {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        state_ = success ? SdkLifecycleState::Running : SdkLifecycleState::Stopped;
    }
    cv_.notify_all();
}

bool SdkRuntime::TryEnterCall(const char* callName) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (state_ != SdkLifecycleState::Running) {
        return false;
    }
    try {
        ActiveCallInfo info;
        info.threadId = GetCurrentThreadId();
        info.startedAt = GetTickCount64();
        info.name = callName ? callName : "unknown";
        activeCallInfos_.push_back(info);
    } catch (...) {
        return false;
    }
    ++activeCalls_;
    return true;
}

bool SdkRuntime::TryEnterCallbackRegistration(const char* callName) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (state_ == SdkLifecycleState::Initializing ||
        state_ == SdkLifecycleState::Releasing ||
        state_ == SdkLifecycleState::Faulted) {
        return false;
    }
    try {
        ActiveCallInfo info;
        info.threadId = GetCurrentThreadId();
        info.startedAt = GetTickCount64();
        info.name = callName ? callName : "unknown";
        activeCallInfos_.push_back(info);
    } catch (...) {
        return false;
    }
    ++activeCalls_;
    return true;
}

void SdkRuntime::LeaveCall() {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        const DWORD threadId = GetCurrentThreadId();
        for (size_t i = activeCallInfos_.size(); i > 0; --i) {
            if (activeCallInfos_[i - 1].threadId == threadId) {
                activeCallInfos_.erase(activeCallInfos_.begin() + (i - 1));
                break;
            }
        }
        if (activeCalls_ > 0) {
            --activeCalls_;
        }
    }
    cv_.notify_all();
}

bool SdkRuntime::BeginRelease(bool& shouldRelease, int timeoutMs) {
    shouldRelease = false;
    std::unique_lock<std::mutex> lock(mutex_);
    const auto deadline = std::chrono::steady_clock::now() +
        std::chrono::milliseconds(timeoutMs > 0 ? timeoutMs : 1);

    while (state_ == SdkLifecycleState::Initializing ||
           state_ == SdkLifecycleState::Releasing) {
        if (cv_.wait_until(lock, deadline) == std::cv_status::timeout) {
            return false;
        }
    }

    // 不可逆清理后的拆除失败可能遗留工作线程；此时仅重启宿主进程能够保证运行时恢复到干净状态。
    if (state_ == SdkLifecycleState::Faulted) {
        return false;
    }

    if (state_ == SdkLifecycleState::Stopped) {
        state_ = SdkLifecycleState::Releasing;
        while (activeCalls_ > 0) {
            if (cv_.wait_until(lock, deadline) == std::cv_status::timeout) {
                state_ = SdkLifecycleState::Stopped;
                cv_.notify_all();
                return false;
            }
        }
        state_ = SdkLifecycleState::Stopped;
        cv_.notify_all();
        return true;
    }

    state_ = SdkLifecycleState::Releasing;
    shouldRelease = true;
    return true;
}

bool SdkRuntime::WaitForActiveCalls(int timeoutMs) {
    std::unique_lock<std::mutex> lock(mutex_);
    if (activeCalls_ == 0) {
        return true;
    }
    if (timeoutMs <= 0) {
        return false;
    }
    return cv_.wait_for(lock, std::chrono::milliseconds(timeoutMs),
                        [this]() { return activeCalls_ == 0; });
}

void SdkRuntime::CompleteRelease(bool success, bool canResumeRunning) {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        if (success) {
            state_ = SdkLifecycleState::Stopped;
        } else {
            state_ = canResumeRunning
                ? SdkLifecycleState::Running
                : SdkLifecycleState::Faulted;
        }
    }
    cv_.notify_all();
}

int SdkRuntime::ActiveCalls() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return activeCalls_;
}

std::string SdkRuntime::DescribeActiveCalls() const {
    std::lock_guard<std::mutex> lock(mutex_);
    if (activeCallInfos_.empty()) {
        return "none";
    }

    const ULONGLONG now = GetTickCount64();
    std::ostringstream details;
    for (size_t i = 0; i < activeCallInfos_.size(); ++i) {
        if (i > 0) details << "; ";
        const ActiveCallInfo& call = activeCallInfos_[i];
        const ULONGLONG ageMs = now >= call.startedAt ? now - call.startedAt : 0;
        details << call.name
                << "(tid=" << call.threadId
                << ",age_ms=" << ageMs << ")";
    }
    return details.str();
}

SdkLifecycleState SdkRuntime::State() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return state_;
}

} // HZCYKJTHardWare 命名空间结束
