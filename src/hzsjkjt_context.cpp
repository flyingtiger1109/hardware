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
    save_camera_default_path.clear();
    save_fingerprint_default_path.clear();
    save_create_date_folder = true;
    save_create_request_folder = true;
    event_callback = nullptr;
    event_user_data = nullptr;
    camera_preview_running = false;
    fingerprint_preview_running = false;
    iris_preview_running = false;
    camera_preview_request_id.clear();
    fingerprint_preview_request_id.clear();
    iris_preview_request_id.clear();
    camera_preview_third_party_hwnd = 0;
    fingerprint_preview_third_party_hwnd = 0;
    iris_preview_third_party_hwnd = 0;
    rtsp_network_caching_ms = 150;
    rtsp_live_caching_ms = 150;
    rtsp_transport = "tcp";
    preview_check_hwnd_interval_ms = 500;
    plate_preview_cj = PlatePreviewState{};
    plate_preview_rj2 = PlatePreviewState{};
    plate_preview_rj3 = PlatePreviewState{};
    process_active = false;
    http_busy.store(false);
    switch_pending.store(false);
}

} // namespace HZCYKJTHardWare
