#pragma once
#include "pch.h"
#include "rtsp_renderer.h"

namespace HZCYKJTHardWare {

// 管理相机、指纹和虹膜 RTSP 预览
class PreviewManager {
public:
    struct ActivePreviewSnapshot {
        bool cameraRunning = false;
        HWND cameraHwnd = nullptr;
        bool fingerprintRunning = false;
        HWND fingerprintHwnd = nullptr;
        bool irisRunning = false;
        HWND irisHwnd = nullptr;
    };

    static PreviewManager& Instance();

    int StartCameraPreview(HWND hwnd);
    int StopCameraPreview();

    int StartFingerprintPreview(HWND hwnd);
    int StopFingerprintPreview();

    int StartIrisPreview(HWND hwnd);
    int StopIrisPreview();

    // 第三方预览链路：通过 Delphi 获取 URL，渲染在当前进程内执行
    int StartCameraPreviewFromUrl(HWND hwnd, const std::string& rtspUrl);
    int StartFingerprintPreviewFromUrl(HWND hwnd, const std::string& rtspUrl);
    int StartIrisPreviewFromUrl(HWND hwnd, const std::string& rtspUrl);
    int StopCameraPreviewRenderer(bool clearStoredHwnd = true);
    int StopFingerprintPreviewRenderer(bool clearStoredHwnd = true);
    int StopIrisPreviewRenderer(bool clearStoredHwnd = true);
    void StopAllRenderers();

    void StopAll();

    ActivePreviewSnapshot CaptureActivePreviewSnapshot() const;
    void StopAllForTerminalSwitch();
    int RestorePreviewsForTerminalSwitch(const ActivePreviewSnapshot& snapshot);

    bool IsCameraPreviewRunning() const;
    bool IsFingerprintPreviewRunning() const;
    bool IsIrisPreviewRunning() const;

private:
    PreviewManager();
    ~PreviewManager();
    PreviewManager(const PreviewManager&) = delete;
    PreviewManager& operator=(const PreviewManager&) = delete;

    int StartPreview(HWND hwnd, const std::string& previewPath,
                     std::atomic<bool>& runningFlag,
                     std::unique_ptr<IRtspRenderer>& renderer,
                     HWND& storedHwnd,
                     int successEvent, int failEvent);

    int StopPreview(std::atomic<bool>& runningFlag,
                    std::unique_ptr<IRtspRenderer>& renderer,
                    HWND& storedHwnd,
                    int stoppedEvent,
                    bool clearStoredHwnd,
                    const char* resourceType = nullptr);
    int StartRendererFromUrl(HWND hwnd, const std::string& rtspUrl,
                             std::atomic<bool>& runningFlag,
                             std::unique_ptr<IRtspRenderer>& renderer,
                             HWND& storedHwnd);
    int StopRenderer(std::atomic<bool>& runningFlag,
                     std::unique_ptr<IRtspRenderer>& renderer,
                     HWND& storedHwnd,
                     bool clearStoredHwnd);

    mutable CRITICAL_SECTION m_cs;

    std::atomic<bool> m_cameraRunning{false};
    std::unique_ptr<IRtspRenderer> m_cameraRenderer;
    HWND m_cameraHwnd = nullptr;

    std::atomic<bool> m_fingerprintRunning{false};
    std::unique_ptr<IRtspRenderer> m_fingerprintRenderer;
    HWND m_fingerprintHwnd = nullptr;

    std::atomic<bool> m_irisRunning{false};
    std::unique_ptr<IRtspRenderer> m_irisRenderer;
    HWND m_irisHwnd = nullptr;

};

} // HZCYKJTHardWare 命名空间结束
