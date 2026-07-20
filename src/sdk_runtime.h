#pragma once

#include "pch.h"

namespace HZCYKJTHardWare {

enum class SdkLifecycleState {
    Stopped,
    Initializing,
    Running,
    Releasing,
    Faulted
};

// 对 InitSdk/ReleaseSdk 进行串行化，并防止导出业务函数执行期间重置运行时资源
class SdkRuntime {
public:
    static SdkRuntime& Instance();

    bool BeginInitialize(bool& shouldInitialize, int timeoutMs);
    void CompleteInitialize(bool success);

    bool TryEnterCall(const char* callName);
    bool TryEnterCallbackRegistration(const char* callName);
    void LeaveCall();

    bool BeginRelease(bool& shouldRelease, int timeoutMs);
    bool WaitForActiveCalls(int timeoutMs);
    void CompleteRelease(bool success, bool canResumeRunning);

    int ActiveCalls() const;
    std::string DescribeActiveCalls() const;
    SdkLifecycleState State() const;

private:
    SdkRuntime() = default;
    SdkRuntime(const SdkRuntime&) = delete;
    SdkRuntime& operator=(const SdkRuntime&) = delete;

    mutable std::mutex mutex_;
    std::condition_variable cv_;
    SdkLifecycleState state_ = SdkLifecycleState::Stopped;
    int activeCalls_ = 0;
    struct ActiveCallInfo {
        DWORD threadId = 0;
        ULONGLONG startedAt = 0;
        const char* name = "unknown";
    };
    std::vector<ActiveCallInfo> activeCallInfos_;
};

} // HZCYKJTHardWare 命名空间结束
