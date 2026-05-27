#include "pch.h"
#include "preview_manager.h"
#include "http_client.h"
#include "terminal_manager.h"
#include "result_parser.h"
#include "hzsjkjt_context.h"
#include "event_dispatcher.h"
#include "logger.h"
#include "json_helper.h"

namespace HZCYKJTHardWare {

static void FillCurrentTerminal(HZCYKJTHardWare_EVENT& event) {
    std::string terminalBaseUrl;
    int terminalIndex = 0;
    {
        auto lock = ReadLock();
        const auto& ctx = HzsjkjtContext::Instance();
        terminalBaseUrl = ctx.current_terminal_base_url;
        terminalIndex = ctx.current_terminal_index;
    }
    event.terminal_base_url = terminalBaseUrl.c_str();
    event.terminal_index = terminalIndex;
    EventDispatcher::Instance().PostEvent(event);
}

namespace { PreviewManager* g_pPreviewMgr = nullptr; }

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
}

PreviewManager& PreviewManager::Instance() {
    if (!g_pPreviewMgr) g_pPreviewMgr = new PreviewManager();
    return *g_pPreviewMgr;
}

PreviewManager::PreviewManager() { InitializeCriticalSection(&m_cs); }
PreviewManager::~PreviewManager() { DeleteCriticalSection(&m_cs); }

int PreviewManager::StartCameraPreview(HWND hwnd) {
    return StartPreview(hwnd, "/resources/face-preview/request",
                        m_cameraRunning, m_cameraRenderer, m_cameraHwnd,
                        HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STARTED,
                        HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED);
}

int PreviewManager::StartFingerprintPreview(HWND hwnd) {
    return StartPreview(hwnd, "/resources/fingerprint-preview/request",
                        m_fingerprintRunning, m_fingerprintRenderer, m_fingerprintHwnd,
                        HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STARTED,
                        HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_FAILED);
}

int PreviewManager::StartIrisPreview(HWND hwnd) {
    return StartPreview(hwnd, "/resources/iris-preview/request",
                        m_irisRunning, m_irisRenderer, m_irisHwnd,
                        HZCYKJTHardWare_EVENT_IRIS_PREVIEW_STARTED,
                        HZCYKJTHardWare_EVENT_IRIS_PREVIEW_FAILED);
}

int PreviewManager::StartPreview(HWND hwnd, const std::string& previewPath,
                                  std::atomic<bool>& runningFlag,
                                  std::unique_ptr<IRtspRenderer>& renderer,
                                  HWND& storedHwnd,
                                  int successEvent, int failEvent) {
    CriticalSectionGuard guard(&m_cs);

    if (!TerminalManager::Instance().IsTerminalSelected()) {
        LOG_ERROR("PreviewMgr", "启动预览失败：当前未选择终端");
        return HZCYKJTHardWare_RET_TERMINAL_NOT_SELECTED;
    }

    if (!IsWindow(hwnd)) {
        LOG_ERROR("PreviewMgr", "启动预览失败：HWND 无效，hwnd=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    if (runningFlag) {
        LOG_WARN("PreviewMgr", "预览已在运行，忽略重复启动：path=%s", previewPath.c_str());
        return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
    }

    char rid[128];
    SYSTEMTIME st;
    GetLocalTime(&st);
    static std::atomic<int> seq{0};
    int currentSeq = ++seq;
    snprintf(rid, sizeof(rid), "HZCYKJTHardWare_PREVIEW_%04d%02d%02d%02d%02d%02d%03d_%03d",
             st.wYear, st.wMonth, st.wDay,
             st.wHour, st.wMinute, st.wSecond, st.wMilliseconds, currentSeq);
    std::string requestId = rid;

    std::string body = "{\"request_id\":\"" + requestId + "\"}";
    std::string url = TerminalManager::Instance().BuildUrl(previewPath);
    LOG_DEBUG("PreviewMgr", "正在请求预览地址：url=%s", url.c_str());

    HttpClient client;
    std::string responseBody;
    int statusCode = 0;

    auto& ctx = HzsjkjtContext::Instance();
    int connectTimeout = 3000;
    int requestTimeout = 5000;
    {
        auto ctxLock = ReadLock();
        connectTimeout = ctx.http_connect_timeout_ms;
        requestTimeout = ctx.http_request_timeout_ms;
    }

    if (!client.PostJson(url, body, connectTimeout, requestTimeout, responseBody, statusCode)) {
        LOG_ERROR("PreviewMgr", "请求终端预览地址失败：url=%s", url.c_str());

        HZCYKJTHardWare_EVENT event;
        memset(&event, 0, sizeof(event));
        event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
        event.event_type = failEvent;
        event.status = HZCYKJTHardWare_RET_HTTP_FAILED;
        event.message = "Preview HTTP request failed";
        FillCurrentTerminal(event);
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    std::string rtspUrl = ResultParser::ExtractPreviewUrl(responseBody);
    if (rtspUrl.empty()) {
        LOG_ERROR("PreviewMgr", "终端预览响应缺少 RTSP 地址：response=%s", responseBody.c_str());

        HZCYKJTHardWare_EVENT event;
        memset(&event, 0, sizeof(event));
        event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
        event.event_type = failEvent;
        event.status = HZCYKJTHardWare_RET_RTSP_URL_EMPTY;
        event.message = "No RTSP URL in preview response";
        FillCurrentTerminal(event);
        return HZCYKJTHardWare_RET_RTSP_URL_EMPTY;
    }

    LOG_DEBUG("PreviewMgr", "已收到RTSP预览地址：url=%s", rtspUrl.c_str());

    renderer = CreateLibVlcRtspRenderer();
    if (!renderer) {
        LOG_ERROR("PreviewMgr", "启动预览失败：创建 RTSP 渲染器失败");

        HZCYKJTHardWare_EVENT event;
        memset(&event, 0, sizeof(event));
        event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
        event.event_type = failEvent;
        event.status = HZCYKJTHardWare_RET_VLC_INIT_FAILED;
        event.message = "Failed to create libVLC renderer";
        FillCurrentTerminal(event);
        return HZCYKJTHardWare_RET_VLC_INIT_FAILED;
    }

    int ret = renderer->Start(rtspUrl, hwnd);
    if (ret != HZCYKJTHardWare_RET_OK) {
        std::string detail = renderer->LastErrorMessage();
        if (detail.empty()) {
            detail = "Failed to start RTSP preview.";
        }
        detail += " RTSP=" + rtspUrl;
        LOG_ERROR("PreviewMgr", "启动 RTSP 预览失败：ret=%d，detail=%s", ret, detail.c_str());

        HZCYKJTHardWare_EVENT event;
        memset(&event, 0, sizeof(event));
        event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
        event.event_type = failEvent;
        event.status = ret;
        event.message = detail.c_str();
        FillCurrentTerminal(event);

        renderer.reset();
        return ret;
    }

    runningFlag = true;
    storedHwnd = hwnd;

    HZCYKJTHardWare_EVENT event;
    memset(&event, 0, sizeof(event));
    event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
    event.event_type = successEvent;
    event.status = HZCYKJTHardWare_RET_OK;

    std::string terminalBaseUrl;
    int terminalIndex = 0;
    {
        auto tlock = ReadLock();
        terminalBaseUrl = HzsjkjtContext::Instance().current_terminal_base_url;
        terminalIndex = HzsjkjtContext::Instance().current_terminal_index;
    }
    event.terminal_base_url = terminalBaseUrl.c_str();
    event.terminal_index = terminalIndex;

    FillCurrentTerminal(event);

    RECT rc;
    int width = 0;
    int height = 0;
    if (GetClientRect(hwnd, &rc)) {
        width = rc.right - rc.left;
        height = rc.bottom - rc.top;
    }
    LOG_INFO("PreviewMgr", "预览已启动：path=%s，hwnd=%p，client=%dx%d",
             previewPath.c_str(), hwnd, width, height);
    return HZCYKJTHardWare_RET_OK;
}

int PreviewManager::StopCameraPreview() {
    return StopPreview(m_cameraRunning, m_cameraRenderer, m_cameraHwnd,
                       HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STOPPED, true);
}

int PreviewManager::StopFingerprintPreview() {
    return StopPreview(m_fingerprintRunning, m_fingerprintRenderer, m_fingerprintHwnd,
                       HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STOPPED, true);
}

int PreviewManager::StopIrisPreview() {
    return StopPreview(m_irisRunning, m_irisRenderer, m_irisHwnd,
                       HZCYKJTHardWare_EVENT_IRIS_PREVIEW_STOPPED, true);
}

int PreviewManager::StopPreview(std::atomic<bool>& runningFlag,
                                 std::unique_ptr<IRtspRenderer>& renderer,
                                 HWND& storedHwnd,
                                 int stoppedEvent,
                                 bool clearStoredHwnd) {
    CriticalSectionGuard guard(&m_cs);

    if (!runningFlag) {
        if (clearStoredHwnd) {
            storedHwnd = nullptr;
        }
        return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
    }

    if (renderer) {
        renderer->Stop();
        renderer.reset();
    }

    runningFlag = false;
    if (clearStoredHwnd) {
        storedHwnd = nullptr;
    }

    HZCYKJTHardWare_EVENT event;
    memset(&event, 0, sizeof(event));
    event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
    event.event_type = stoppedEvent;
    event.status = HZCYKJTHardWare_RET_OK;
    EventDispatcher::Instance().PostEvent(event);

    LOG_INFO("PreviewMgr", "预览已停止：event=%d", stoppedEvent);
    return HZCYKJTHardWare_RET_OK;
}

void PreviewManager::StopAll() {
    StopCameraPreview();
    StopFingerprintPreview();
    StopIrisPreview();
}

PreviewManager::ActivePreviewSnapshot PreviewManager::CaptureActivePreviewSnapshot() const {
    CriticalSectionGuard guard(&m_cs);

    ActivePreviewSnapshot snapshot;
    snapshot.cameraRunning = m_cameraHwnd && IsWindow(m_cameraHwnd);
    snapshot.cameraHwnd = snapshot.cameraRunning ? m_cameraHwnd : nullptr;
    snapshot.fingerprintRunning = m_fingerprintHwnd && IsWindow(m_fingerprintHwnd);
    snapshot.fingerprintHwnd = snapshot.fingerprintRunning ? m_fingerprintHwnd : nullptr;
    snapshot.irisRunning = m_irisHwnd && IsWindow(m_irisHwnd);
    snapshot.irisHwnd = snapshot.irisRunning ? m_irisHwnd : nullptr;
    return snapshot;
}

void PreviewManager::StopAllForTerminalSwitch() {
    StopPreview(m_cameraRunning, m_cameraRenderer, m_cameraHwnd,
                HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STOPPED, false);
    StopPreview(m_fingerprintRunning, m_fingerprintRenderer, m_fingerprintHwnd,
                HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STOPPED, false);
    StopPreview(m_irisRunning, m_irisRenderer, m_irisHwnd,
                HZCYKJTHardWare_EVENT_IRIS_PREVIEW_STOPPED, false);
}

int PreviewManager::RestorePreviewsForTerminalSwitch(const ActivePreviewSnapshot& snapshot) {
    int firstError = HZCYKJTHardWare_RET_OK;

    if (snapshot.cameraRunning) {
        int ret = StartCameraPreview(snapshot.cameraHwnd);
        if (ret != HZCYKJTHardWare_RET_OK && firstError == HZCYKJTHardWare_RET_OK) {
            firstError = ret;
        }
    }

    if (snapshot.fingerprintRunning) {
        int ret = StartFingerprintPreview(snapshot.fingerprintHwnd);
        if (ret != HZCYKJTHardWare_RET_OK && firstError == HZCYKJTHardWare_RET_OK) {
            firstError = ret;
        }
    }

    if (snapshot.irisRunning) {
        int ret = StartIrisPreview(snapshot.irisHwnd);
        if (ret != HZCYKJTHardWare_RET_OK && firstError == HZCYKJTHardWare_RET_OK) {
            firstError = ret;
        }
    }

    return firstError;
}

bool PreviewManager::IsCameraPreviewRunning() const { return m_cameraRunning; }
bool PreviewManager::IsFingerprintPreviewRunning() const { return m_fingerprintRunning; }
bool PreviewManager::IsIrisPreviewRunning() const { return m_irisRunning; }

} // namespace HZCYKJTHardWare
