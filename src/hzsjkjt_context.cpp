#include "pch.h"
#include "hzsjkjt_context.h"

namespace HZCYKJTHardWare {

HzsjkjtContext& HzsjkjtContext::Instance() {
    static HzsjkjtContext ctx;
    return ctx;
}

HzsjkjtContext::HzsjkjtContext() {
    InitializeCriticalSection(&mutex);
}

HzsjkjtContext::~HzsjkjtContext() {
    DeleteCriticalSection(&mutex);
}

void HzsjkjtContext::Reset() {
    initialized = false;
    current_terminal_index = 0;
    current_terminal_base_url.clear();
    selected_lan_ip.clear();
    selected_subnet_prefix.clear();
    delphi_server_url.clear();
    callback_server_host.clear();
    callback_server_port = 39091;
    callback_url_host.clear();
    callback_server_running = false;
    runtime_save_path.clear();
    save_default_root.clear();
    save_create_date_folder = true;
    save_create_request_folder = true;
    event_callback = nullptr;
    event_user_data = nullptr;
    camera_preview_running = false;
    fingerprint_preview_running = false;
    camera_preview_request_id.clear();
    camera_preview_third_party_hwnd = 0;
    camera_preview_vlc_hwnd = 0;
    camera_preview_delphi_host_hwnd = 0;
    rtsp_network_caching_ms = 150;
    rtsp_live_caching_ms = 150;
    rtsp_transport = "tcp";
    process_active = false;
}

} // namespace HZCYKJTHardWare
