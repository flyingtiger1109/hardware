#pragma once
#include "pch.h"
#include "include/HZCYKJTHardWare_types.h"

namespace HZCYKJTHardWare {

class HttpClient;  // forward declaration

struct PlatePreviewState {
    bool running = false;
    std::string request_id;
    intptr_t third_party_hwnd = 0;
    bool enabled = false;
    std::string rtsp_url;
    int stream_channel = 101;
};

// 全局单例上下文，管理 DLL 运行时状态
class HzsjkjtContext {
public:
    static HzsjkjtContext& Instance();

    // 初始化状态
    bool initialized = false;

    // 终端上下文
    int current_terminal_index = 0;
    std::string current_terminal_base_url;
    std::string selected_lan_ip;
    std::string selected_subnet_prefix;
    std::string delphi_server_url;

    // 回调服务器配置
    std::string callback_server_host;
    int callback_server_port = 39091;
    std::string callback_url_host;  // 用于生成 callback_url 的 host
    std::string callback_server_base_path;  // 从配置文件读取的回调基础路径
    bool callback_server_running = false;

    // 保存路径（运行时设置，优先级低于配置文件）
    std::string runtime_save_path;
    std::string save_default_root;
    std::string save_camera_default_path;
    std::string save_fingerprint_default_path;
    bool save_create_date_folder = true;
    bool save_create_request_folder = true;

    // 事件回调
    THZCYKJTHardWareEventCallback event_callback = nullptr;
    void* event_user_data = nullptr;

    // 超时配置（毫秒）
    int http_connect_timeout_ms = 3000;
    int http_request_timeout_ms = 5000;
    int face_capture_timeout_ms = 15000;
    int fingerprint_capture_timeout_ms = 15000;
    int ocr_timeout_ms = 20000;
    int authorize_timeout_ms = 60000;

    // 预览状态
    bool camera_preview_running = false;
    bool fingerprint_preview_running = false;
    bool iris_preview_running = false;
    std::string camera_preview_request_id;
    std::string fingerprint_preview_request_id;
    std::string iris_preview_request_id;
    intptr_t camera_preview_third_party_hwnd = 0;
    intptr_t fingerprint_preview_third_party_hwnd = 0;
    intptr_t iris_preview_third_party_hwnd = 0;
    int rtsp_network_caching_ms = 150;
    int rtsp_live_caching_ms = 150;
    std::string rtsp_transport = "tcp";
    int preview_check_hwnd_interval_ms = 500;
    PlatePreviewState plate_preview_cj;
    PlatePreviewState plate_preview_rj2;
    PlatePreviewState plate_preview_rj3;

    // 流程状态
    bool process_active = false;
    std::atomic<bool> http_busy{false};  // 防止 Delphi 代理请求重入
    std::atomic<bool> switch_pending{false};  // 终端切换进行中，拦截新操作

    // DLL 模块路径
    std::string dll_dir;

    // 全局 HTTP 客户端（InitSdk 时创建，ReleaseSdk 时销毁）
    HttpClient* http_client = nullptr;

    // 线程安全
    mutable CRITICAL_SECTION mutex;

    // 重置（ReleaseSdk 时调用）
    void Reset();

private:
    HzsjkjtContext();
    ~HzsjkjtContext();
    HzsjkjtContext(const HzsjkjtContext&) = delete;
    HzsjkjtContext& operator=(const HzsjkjtContext&) = delete;
};

class ContextLock {
public:
    explicit ContextLock(CRITICAL_SECTION* cs) : m_cs(cs), m_locked(true) {
        EnterCriticalSection(m_cs);
    }

    ContextLock(ContextLock&& other) noexcept : m_cs(other.m_cs), m_locked(other.m_locked) {
        other.m_cs = nullptr;
        other.m_locked = false;
    }

    ContextLock& operator=(ContextLock&& other) noexcept {
        if (this != &other) {
            unlock();
            m_cs = other.m_cs;
            m_locked = other.m_locked;
            other.m_cs = nullptr;
            other.m_locked = false;
        }
        return *this;
    }

    ~ContextLock() {
        unlock();
    }

    void unlock() {
        if (m_locked && m_cs) {
            LeaveCriticalSection(m_cs);
            m_locked = false;
        }
    }

    ContextLock(const ContextLock&) = delete;
    ContextLock& operator=(const ContextLock&) = delete;

private:
    CRITICAL_SECTION* m_cs;
    bool m_locked;
};

inline ContextLock ReadLock() {
    return ContextLock(&HzsjkjtContext::Instance().mutex);
}

inline ContextLock WriteLock() {
    return ContextLock(&HzsjkjtContext::Instance().mutex);
}

} // namespace HZCYKJTHardWare
