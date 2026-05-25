#pragma once
#include "pch.h"
#include "include/HZCYKJTHardWare_types.h"
#include "callback_server.h"   // for CallbackData definition

namespace HZCYKJTHardWare {

struct RequestSession;

// 事件分发器：内部队列 + worker 线程
// HTTP 回调线程只投递事件，worker 线程执行第三方回调
class EventDispatcher {
public:
    static EventDispatcher& Instance();

    // 启动 worker 线程
    void Start();

    // 停止 worker 线程
    void Stop();

    // 投递事件（线程安全，可在 HTTP 回调线程调用）
    void PostEvent(const HZCYKJTHardWare_EVENT& event);

    // 处理来自 CallbackServer 的回调数据
    void PostCallbackData(const CallbackData& cbData);

    // 设置第三方回调
    void SetCallback(THZCYKJTHardWareEventCallback callback);

private:
    EventDispatcher();
    ~EventDispatcher();
    EventDispatcher(const EventDispatcher&) = delete;
    EventDispatcher& operator=(const EventDispatcher&) = delete;

    void WorkerLoop();
    void ProcessCallback(const CallbackData& cbData);

    void ProcessFaceCallback(const std::string& requestId,
                             const std::string& body,
                             const RequestSession& session);
    void ProcessFingerprintCallback(const std::string& requestId,
                                    const std::string& body,
                                    const RequestSession& session);
    void ProcessOcrCallback(const std::string& requestId,
                            const std::string& body,
                            const RequestSession& session);
    void ProcessIrisCallback(const std::string& requestId,
                             const std::string& body,
                             const RequestSession& session);
    void ProcessNfcCardCallback(const std::string& requestId,
                                const std::string& body,
                                const RequestSession& session);
    void ProcessPreviewReadyCallback(const std::string& requestId,
                                     const std::string& body);

    void SendEvent(const std::string& requestId,
                   const std::string& resourceType,
                   int eventType, int status,
                   const char* errorCode, const char* message,
                   const char* savePath = nullptr,
                   const char* rawJson = nullptr,
                   const char* icNumber = nullptr,
                   const char* mrz = nullptr);

    std::atomic<bool> m_running{false};
    std::unique_ptr<std::thread> m_thread;

    CRITICAL_SECTION m_cs;
    CONDITION_VARIABLE m_cv;
    std::queue<HZCYKJTHardWare_EVENT> m_queue;
    std::queue<CallbackData> m_pendingCallbacks;

    THZCYKJTHardWareEventCallback m_callback = nullptr;

    // 存储事件中的字符串数据（确保指针在回调期间有效）
    struct EventStrings {
        std::string request_id;
        std::string resource_type;
        std::string error_code;
        std::string message;
        std::string terminal_base_url;
        std::string save_path;
        std::string raw_json;
        std::string ic_number;
        std::string mrz;
        std::vector<char> data;
    };
    std::queue<EventStrings> m_stringsQueue;
};

} // namespace HZCYKJTHardWare
