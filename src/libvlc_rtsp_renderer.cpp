#include "pch.h"
#include "libvlc_rtsp_renderer.h"
#include "logger.h"
#include "path_helper.h"
#include "hzsjkjt_context.h"

namespace HZCYKJTHardWare {

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

std::string SanitizeUrlForLog(const std::string& url) {
    const size_t schemeEnd = url.find("://");
    if (schemeEnd == std::string::npos) return url;

    const size_t authorityStart = schemeEnd + 3;
    const size_t pathStart = url.find('/', authorityStart);
    const size_t at = url.find('@', authorityStart);
    if (at == std::string::npos ||
        (pathStart != std::string::npos && at > pathStart)) {
        return url;
    }

    return url.substr(0, authorityStart) + "***:***@" + url.substr(at + 1);
}
}

static std::string FormatWindowsError(DWORD err) {
    if (err == 0) return "";

    LPWSTR buffer = nullptr;
    DWORD len = FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER |
                               FORMAT_MESSAGE_FROM_SYSTEM |
                               FORMAT_MESSAGE_IGNORE_INSERTS,
                               nullptr, err, 0, (LPWSTR)&buffer, 0, nullptr);
    if (len == 0 || !buffer) {
        return "Windows error " + std::to_string(err);
    }

    std::wstring msg(buffer, len);
    LocalFree(buffer);
    while (!msg.empty() && (msg.back() == L'\r' || msg.back() == L'\n' || msg.back() == L' ')) {
        msg.pop_back();
    }
    return PathHelper::WideToUtf8(msg);
}

LibVlcRtspRenderer::LibVlcRtspRenderer() {
    InitializeCriticalSection(&m_cs);
}

LibVlcRtspRenderer::~LibVlcRtspRenderer() {
    Stop();
    UnloadLibVlc();
    DeleteCriticalSection(&m_cs);
}

bool LibVlcRtspRenderer::LoadLibVlc() {
    if (m_hLibVlcCore && m_hLibVlc) return true;

    std::string dllDir = HzsjkjtContext::Instance().dll_dir;
    std::vector<std::string> candidateDirs = {
        dllDir,
        PathHelper::Join(dllDir, "vlc"),
        PathHelper::Join(dllDir, "VLC"),
        "D:\\VLC",
        "C:\\Program Files\\VideoLAN\\VLC",
        "C:\\Program Files (x86)\\VideoLAN\\VLC",
    };

    for (const auto& dir : candidateDirs) {
        if (TryLoadLibVlcFromDir(dir)) {
            return true;
        }
    }

    SetLastErrorMessage("未找到 32 位 libVLC 依赖。请安装 32 位 VLC，或将 libvlc.dll、libvlccore.dll 和 plugins 目录放到 DLL 同目录。");
    LOG_ERROR("LibVlcRenderer", "%s", m_lastError.c_str());
    return false;
}

bool LibVlcRtspRenderer::TryLoadLibVlcFromDir(const std::string& dir) {
    if (dir.empty()) return false;

    std::string corePath = PathHelper::Join(dir, "libvlccore.dll");
    std::string vlcPath = PathHelper::Join(dir, "libvlc.dll");
    if (!PathHelper::FileExists(corePath) || !PathHelper::FileExists(vlcPath)) {
        return false;
    }

    // Use an absolute module path with a per-load dependency search directory.
    // SetDllDirectory is process-wide and would otherwise alter the Delphi host's
    // DLL resolution for unrelated third-party modules.
    m_hLibVlcCore = LoadLibraryExW(
        PathHelper::Utf8ToWide(corePath).c_str(), nullptr,
        LOAD_WITH_ALTERED_SEARCH_PATH);
    if (!m_hLibVlcCore) {
        DWORD err = GetLastError();
        SetLastErrorMessage("加载 libvlccore.dll 失败: " + corePath + ", " + FormatWindowsError(err));
        LOG_ERROR("LibVlcRenderer", "%s", m_lastError.c_str());
        return false;
    }

    m_hLibVlc = LoadLibraryExW(
        PathHelper::Utf8ToWide(vlcPath).c_str(), nullptr,
        LOAD_WITH_ALTERED_SEARCH_PATH);
    if (!m_hLibVlc) {
        DWORD err = GetLastError();
        SetLastErrorMessage("加载 libvlc.dll 失败: " + vlcPath + ", " + FormatWindowsError(err));
        LOG_ERROR("LibVlcRenderer", "%s", m_lastError.c_str());
        UnloadLibVlc();
        return false;
    }

    #define LOAD_VLC_FUNC(name) \
        m_##name = GetProcAddress(m_hLibVlc, #name); \
        if (!m_##name) { \
            SetLastErrorMessage(std::string("libVLC 缺少函数: ") + #name); \
            LOG_ERROR("LibVlcRenderer", "%s", m_lastError.c_str()); \
            UnloadLibVlc(); \
            return false; \
        }

    LOAD_VLC_FUNC(libvlc_new);
    LOAD_VLC_FUNC(libvlc_release);
    LOAD_VLC_FUNC(libvlc_media_new_location);
    LOAD_VLC_FUNC(libvlc_media_add_option);
    LOAD_VLC_FUNC(libvlc_media_release);
    LOAD_VLC_FUNC(libvlc_media_player_new_from_media);
    LOAD_VLC_FUNC(libvlc_media_player_release);
    LOAD_VLC_FUNC(libvlc_media_player_set_hwnd);
    LOAD_VLC_FUNC(libvlc_media_player_play);
    LOAD_VLC_FUNC(libvlc_media_player_stop);
    LOAD_VLC_FUNC(libvlc_media_player_is_playing);

    #undef LOAD_VLC_FUNC

    m_libvlc_video_set_aspect_ratio = GetProcAddress(m_hLibVlc, "libvlc_video_set_aspect_ratio");
    m_libvlc_video_set_crop_geometry = GetProcAddress(m_hLibVlc, "libvlc_video_set_crop_geometry");
    m_libvlc_video_set_scale = GetProcAddress(m_hLibVlc, "libvlc_video_set_scale");

    m_vlcDir = dir;
    m_lastError.clear();
    LOG_DEBUG("LibVlcRenderer", "libVLC 加载成功：dir=%s", dir.c_str());
    return true;
}

void LibVlcRtspRenderer::UnloadLibVlc() {
    if (m_hLibVlc) {
        FreeLibrary(m_hLibVlc);
        m_hLibVlc = nullptr;
    }
    if (m_hLibVlcCore) {
        FreeLibrary(m_hLibVlcCore);
        m_hLibVlcCore = nullptr;
    }

    m_libvlc_new = nullptr;
    m_libvlc_release = nullptr;
    m_libvlc_media_new_location = nullptr;
    m_libvlc_media_add_option = nullptr;
    m_libvlc_media_release = nullptr;
    m_libvlc_media_player_new_from_media = nullptr;
    m_libvlc_media_player_release = nullptr;
    m_libvlc_media_player_set_hwnd = nullptr;
    m_libvlc_media_player_play = nullptr;
    m_libvlc_media_player_stop = nullptr;
    m_libvlc_media_player_is_playing = nullptr;
    m_libvlc_video_set_aspect_ratio = nullptr;
    m_libvlc_video_set_crop_geometry = nullptr;
    m_libvlc_video_set_scale = nullptr;
}

void LibVlcRtspRenderer::SetLastErrorMessage(const std::string& message) {
    m_lastError = message;
}

void LibVlcRtspRenderer::CaptureExistingChildWindows(HWND parentHwnd) {
    m_existingChildWindows.clear();
    if (!IsWindow(parentHwnd)) {
        return;
    }

    EnumChildWindows(parentHwnd, [](HWND child, LPARAM lParam) -> BOOL {
        auto* children = reinterpret_cast<std::vector<HWND>*>(lParam);
        children->push_back(child);
        return TRUE;
    }, reinterpret_cast<LPARAM>(&m_existingChildWindows));
    m_lastWidth = -1;
    m_lastHeight = -1;
}

bool LibVlcRtspRenderer::IsExistingChildWindow(HWND hwnd) const {
    return std::find(m_existingChildWindows.begin(), m_existingChildWindows.end(), hwnd) !=
        m_existingChildWindows.end();
}

int LibVlcRtspRenderer::Start(const std::string& url, HWND hwnd) {
    CriticalSectionGuard guard(&m_cs);

    if (m_running) {
        Stop();
    }

    if (url.empty()) {
        LOG_ERROR("LibVlcRenderer", "启动 RTSP 失败：URL 为空");
        SetLastErrorMessage("RTSP 地址为空。");
        return HZCYKJTHardWare_RET_RTSP_URL_EMPTY;
    }

    if (!IsWindow(hwnd)) {
        LOG_ERROR("LibVlcRenderer", "启动 RTSP 失败：HWND 无效，hwnd=%p", hwnd);
        SetLastErrorMessage("预览窗口句柄无效。");
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    if (!LoadLibVlc()) {
        LOG_ERROR("LibVlcRenderer", "启动 RTSP 失败：加载 libVLC 失败，detail=%s", m_lastError.c_str());
        return HZCYKJTHardWare_RET_VLC_INIT_FAILED;
    }

    // 创建 VLC 实例
    typedef libvlc_instance_t* (*vlc_new_t)(int, const char* const*);
    std::string pluginArg;
    std::vector<const char*> vlcArgs = {
        "--no-video-title-show",
        "--no-xlib",
        "--quiet",
    };
    std::string pluginDir = PathHelper::Join(m_vlcDir, "plugins");
    if (!m_vlcDir.empty() && PathHelper::DirectoryExists(pluginDir)) {
        pluginArg = "--plugin-path=" + pluginDir;
        vlcArgs.push_back(pluginArg.c_str());
    }
    int argc = (int)vlcArgs.size();
    m_vlcInstance = ((vlc_new_t)m_libvlc_new)(argc, vlcArgs.data());

    if (!m_vlcInstance) {
        LOG_ERROR("LibVlcRenderer", "启动 RTSP 失败：创建 VLC 实例失败");
        SetLastErrorMessage("创建 VLC 实例失败，请确认 plugins 目录与 VLC DLL 位数匹配。");
        return HZCYKJTHardWare_RET_VLC_INIT_FAILED;
    }

    // 创建 media
    typedef libvlc_media_t* (*media_new_location_t)(libvlc_instance_t*, const char*);
    m_media = ((media_new_location_t)m_libvlc_media_new_location)(m_vlcInstance, url.c_str());
    if (!m_media) {
        const std::string safeUrl = SanitizeUrlForLog(url);
        LOG_ERROR("LibVlcRenderer", "启动 RTSP 失败：创建 media 失败，url=%s", safeUrl.c_str());
        SetLastErrorMessage("创建 RTSP 媒体失败: " + safeUrl);
        ((void(*)(libvlc_instance_t*))m_libvlc_release)(m_vlcInstance);
        m_vlcInstance = nullptr;
        return HZCYKJTHardWare_RET_PREVIEW_RENDER_FAILED;
    }

    // 创建 media player
    int networkCachingMs = 150;
    int liveCachingMs = 150;
    std::string rtspTransport = "tcp";
    {
        auto ctxLock = ReadLock();
        const auto& ctx = HzsjkjtContext::Instance();
        networkCachingMs = ctx.rtsp_network_caching_ms;
        liveCachingMs = ctx.rtsp_live_caching_ms;
        rtspTransport = ctx.rtsp_transport;
        m_layoutIntervalMs = ctx.preview_check_hwnd_interval_ms;
    }

    if (networkCachingMs < 0) networkCachingMs = 0;
    if (liveCachingMs < 0) liveCachingMs = 0;
    if (m_layoutIntervalMs < 50) m_layoutIntervalMs = 50;

    typedef void (*media_add_option_t)(libvlc_media_t*, const char*);
    auto addMediaOption = (media_add_option_t)m_libvlc_media_add_option;
    std::string networkCachingOption = ":network-caching=" + std::to_string(networkCachingMs);
    std::string liveCachingOption = ":live-caching=" + std::to_string(liveCachingMs);
    addMediaOption(m_media, networkCachingOption.c_str());
    addMediaOption(m_media, liveCachingOption.c_str());

    if (rtspTransport == "tcp" || rtspTransport == "TCP" || rtspTransport == "Tcp") {
        addMediaOption(m_media, ":rtsp-tcp");
    }

    LOG_DEBUG("LibVlcRenderer", "RTSP 参数：network-caching=%d，live-caching=%d，transport=%s",
             networkCachingMs, liveCachingMs, rtspTransport.c_str());

    typedef libvlc_media_player_t* (*player_new_t)(libvlc_media_t*);
    m_mediaPlayer = ((player_new_t)m_libvlc_media_player_new_from_media)(m_media);
    if (!m_mediaPlayer) {
        LOG_ERROR("LibVlcRenderer", "启动 RTSP 失败：创建播放器失败");
        SetLastErrorMessage("创建 RTSP 播放器失败。");
        ((void(*)(libvlc_media_t*))m_libvlc_media_release)(m_media);
        m_media = nullptr;
        ((void(*)(libvlc_instance_t*))m_libvlc_release)(m_vlcInstance);
        m_vlcInstance = nullptr;
        return HZCYKJTHardWare_RET_PREVIEW_RENDER_FAILED;
    }

    // 设置渲染窗口
    CaptureExistingChildWindows(hwnd);
    typedef void (*set_hwnd_t)(libvlc_media_player_t*, void*);
    ((set_hwnd_t)m_libvlc_media_player_set_hwnd)(m_mediaPlayer, hwnd);

    // 开始播放
    typedef int (*play_t)(libvlc_media_player_t*);
    int playRet = ((play_t)m_libvlc_media_player_play)(m_mediaPlayer);
    if (playRet != 0) {
        LOG_ERROR("LibVlcRenderer", "启动 RTSP 播放失败：ret=%d", playRet);
        SetLastErrorMessage("启动 RTSP 播放失败，libVLC 返回: " +
            std::to_string(playRet) + ", URL: " + SanitizeUrlForLog(url));
        ((void(*)(libvlc_media_player_t*))m_libvlc_media_player_release)(m_mediaPlayer);
        m_mediaPlayer = nullptr;
        ((void(*)(libvlc_media_t*))m_libvlc_media_release)(m_media);
        m_media = nullptr;
        ((void(*)(libvlc_instance_t*))m_libvlc_release)(m_vlcInstance);
        m_vlcInstance = nullptr;
        m_existingChildWindows.clear();
        return HZCYKJTHardWare_RET_PREVIEW_RENDER_FAILED;
    }

    m_running = true;
    m_renderHwnd = hwnd;
    m_lastError.clear();

    m_lastWidth = -1;
    m_lastHeight = -1;
    ApplyWindowFit(hwnd);
    m_stopLayout = false;
    m_layoutThread = std::make_unique<std::thread>(&LibVlcRtspRenderer::LayoutLoop, this);

    const std::string safeUrl = SanitizeUrlForLog(url);
    LOG_DEBUG("LibVlcRenderer", "RTSP 播放已启动：url=%s，hwnd=%p", safeUrl.c_str(), hwnd);
    return HZCYKJTHardWare_RET_OK;
}

int LibVlcRtspRenderer::Stop() {
    m_stopLayout = true;
    if (m_layoutThread && m_layoutThread->joinable()) {
        m_layoutThread->join();
    }
    m_layoutThread.reset();

    CriticalSectionGuard guard(&m_cs);

    // 停止播放
    if (m_mediaPlayer && m_libvlc_media_player_stop) {
        typedef void (*stop_t)(libvlc_media_player_t*);
        ((stop_t)m_libvlc_media_player_stop)(m_mediaPlayer);
    }

    // 释放 media player
    if (m_mediaPlayer && m_libvlc_media_player_release) {
        typedef void (*release_t)(libvlc_media_player_t*);
        ((release_t)m_libvlc_media_player_release)(m_mediaPlayer);
    }
    m_mediaPlayer = nullptr;

    // 释放 media
    if (m_media && m_libvlc_media_release) {
        typedef void (*release_t)(libvlc_media_t*);
        ((release_t)m_libvlc_media_release)(m_media);
    }
    m_media = nullptr;

    // 释放 VLC 实例
    if (m_vlcInstance && m_libvlc_release) {
        typedef void (*release_t)(libvlc_instance_t*);
        ((release_t)m_libvlc_release)(m_vlcInstance);
    }
    m_vlcInstance = nullptr;

    m_running = false;
    m_renderHwnd = nullptr;
    m_existingChildWindows.clear();
    LOG_DEBUG("LibVlcRenderer", "RTSP 播放已停止");
    return HZCYKJTHardWare_RET_OK;
}

bool LibVlcRtspRenderer::IsRunning() const {
    return m_running;
}

std::string LibVlcRtspRenderer::LastErrorMessage() const {
    return m_lastError;
}

void LibVlcRtspRenderer::LayoutLoop() {
    while (!m_stopLayout) {
        int remainingMs = m_layoutIntervalMs;
        while (!m_stopLayout && remainingMs > 0) {
            const int sleepMs = remainingMs > 25 ? 25 : remainingMs;
            Sleep(static_cast<DWORD>(sleepMs));
            remainingMs -= sleepMs;
        }
        if (!m_stopLayout && m_running) {
            ApplyWindowFit(m_renderHwnd);
        }
    }
}

void LibVlcRtspRenderer::ApplyWindowFit(HWND hwnd) {
    if (!IsWindow(hwnd)) return;

    RECT rc;
    if (!GetClientRect(hwnd, &rc)) return;

    int width = rc.right - rc.left;
    int height = rc.bottom - rc.top;
    if (width <= 0 || height <= 0) return;

    bool sizeChanged = (width != m_lastWidth || height != m_lastHeight);

    if (sizeChanged && m_mediaPlayer && m_libvlc_video_set_scale) {
        typedef void (*set_scale_t)(libvlc_media_player_t*, float);
        ((set_scale_t)m_libvlc_video_set_scale)(m_mediaPlayer, 0.0f);
    }

    if (sizeChanged && m_mediaPlayer && m_libvlc_video_set_crop_geometry) {
        char aspect[32];
        snprintf(aspect, sizeof(aspect), "%d:%d", width, height);
        typedef void (*set_crop_t)(libvlc_media_player_t*, const char*);
        ((set_crop_t)m_libvlc_video_set_crop_geometry)(m_mediaPlayer, aspect);
    } else if (sizeChanged && m_mediaPlayer && m_libvlc_video_set_aspect_ratio) {
        char aspect[32];
        snprintf(aspect, sizeof(aspect), "%d:%d", width, height);
        typedef void (*set_aspect_t)(libvlc_media_player_t*, const char*);
        ((set_aspect_t)m_libvlc_video_set_aspect_ratio)(m_mediaPlayer, aspect);
    }

    struct LayoutContext {
        LibVlcRtspRenderer* self;
        int width;
        int height;
        bool sizeChanged;
    } context{this, width, height, sizeChanged};

    EnumChildWindows(hwnd, [](HWND child, LPARAM lParam) -> BOOL {
        auto* context = reinterpret_cast<LayoutContext*>(lParam);
        if (context->self->IsExistingChildWindow(child)) {
            return TRUE;
        }

        UINT flags = SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_SHOWWINDOW;
        if (!context->sizeChanged) {
            flags |= SWP_NOMOVE | SWP_NOSIZE;
        }
        SetWindowPos(child, HWND_BOTTOM, 0, 0, context->width, context->height, flags);
        return TRUE;
    }, reinterpret_cast<LPARAM>(&context));

    m_lastWidth = width;
    m_lastHeight = height;

    LOG_DEBUG("LibVlcRenderer", "预览窗口已按客户区铺满：hwnd=%p，width=%d，height=%d",
              hwnd, width, height);
}

// 工厂函数
std::unique_ptr<IRtspRenderer> CreateLibVlcRtspRenderer() {
    return std::make_unique<LibVlcRtspRenderer>();
}

} // namespace HZCYKJTHardWare
