#pragma once
#include "pch.h"
#include "rtsp_renderer.h"

// 前向声明 libVLC 类型
struct libvlc_instance_t;
struct libvlc_media_t;
struct libvlc_media_player_t;

namespace HZCYKJTHardWare {

// 基于 libVLC 的 RTSP 渲染器实现
class LibVlcRtspRenderer : public IRtspRenderer {
public:
    LibVlcRtspRenderer();
    ~LibVlcRtspRenderer() override;

    int Start(const std::string& url, HWND hwnd) override;
    int Stop() override;
    bool IsRunning() const override;
    std::string LastErrorMessage() const override;

private:
    // 动态加载 libVLC
    bool LoadLibVlc();
    void UnloadLibVlc();
    bool TryLoadLibVlcFromDir(const std::string& dir);
    void SetLastErrorMessage(const std::string& message);
    void ApplyWindowFit(HWND hwnd);
    void LayoutLoop();

    HMODULE m_hLibVlcCore = nullptr;
    HMODULE m_hLibVlc = nullptr;

    // 函数指针
    void* m_libvlc_new = nullptr;
    void* m_libvlc_release = nullptr;
    void* m_libvlc_media_new_location = nullptr;
    void* m_libvlc_media_add_option = nullptr;
    void* m_libvlc_media_release = nullptr;
    void* m_libvlc_media_player_new_from_media = nullptr;
    void* m_libvlc_media_player_release = nullptr;
    void* m_libvlc_media_player_set_hwnd = nullptr;
    void* m_libvlc_media_player_play = nullptr;
    void* m_libvlc_media_player_stop = nullptr;
    void* m_libvlc_media_player_is_playing = nullptr;
    void* m_libvlc_video_set_aspect_ratio = nullptr;
    void* m_libvlc_video_set_crop_geometry = nullptr;
    void* m_libvlc_video_set_scale = nullptr;

    libvlc_instance_t* m_vlcInstance = nullptr;
    libvlc_media_t* m_media = nullptr;
    libvlc_media_player_t* m_mediaPlayer = nullptr;

    std::atomic<bool> m_running{false};
    HWND m_renderHwnd = nullptr;
    CRITICAL_SECTION m_cs;
    std::string m_lastError;
    std::string m_vlcDir;
    std::unique_ptr<std::thread> m_layoutThread;
    std::atomic<bool> m_stopLayout{false};
    int m_layoutIntervalMs = 500;
};

} // namespace HZCYKJTHardWare
