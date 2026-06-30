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

bool SdkRuntime::TryEnterCall() {
    std::lock_guard<std::mutex> lock(mutex_);
    if (state_ != SdkLifecycleState::Running) {
        return false;
    }
    ++activeCalls_;
    return true;
}

bool SdkRuntime::TryEnterCallbackRegistration() {
    std::lock_guard<std::mutex> lock(mutex_);
    if (state_ == SdkLifecycleState::Initializing ||
        state_ == SdkLifecycleState::Releasing) {
        return false;
    }
    ++activeCalls_;
    return true;
}

void SdkRuntime::LeaveCall() {
    {
        std::lock_guard<std::mutex> lock(mutex_);
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

void SdkRuntime::CompleteRelease(bool success) {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        state_ = success ? SdkLifecycleState::Stopped : SdkLifecycleState::Running;
    }
    cv_.notify_all();
}

int SdkRuntime::ActiveCalls() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return activeCalls_;
}

SdkLifecycleState SdkRuntime::State() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return state_;
}

} // namespace HZCYKJTHardWare
