#pragma once

#include "pch.h"

namespace HZCYKJTHardWare {

enum class SdkLifecycleState {
    Stopped,
    Initializing,
    Running,
    Releasing
};

// Serializes InitSdk/ReleaseSdk and protects runtime resources from being reset
// while an exported business function is still executing.
class SdkRuntime {
public:
    static SdkRuntime& Instance();

    bool BeginInitialize(bool& shouldInitialize, int timeoutMs);
    void CompleteInitialize(bool success);

    bool TryEnterCall();
    bool TryEnterCallbackRegistration();
    void LeaveCall();

    bool BeginRelease(bool& shouldRelease, int timeoutMs);
    bool WaitForActiveCalls(int timeoutMs);
    void CompleteRelease(bool success);

    int ActiveCalls() const;
    SdkLifecycleState State() const;

private:
    SdkRuntime() = default;
    SdkRuntime(const SdkRuntime&) = delete;
    SdkRuntime& operator=(const SdkRuntime&) = delete;

    mutable std::mutex mutex_;
    std::condition_variable cv_;
    SdkLifecycleState state_ = SdkLifecycleState::Stopped;
    int activeCalls_ = 0;
};

} // namespace HZCYKJTHardWare
