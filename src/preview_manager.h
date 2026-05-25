#pragma once
#include "pch.h"
#include "rtsp_renderer.h"

namespace HZCYKJTHardWare {

// Manages camera, fingerprint and iris RTSP previews.
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

} // namespace HZCYKJTHardWare
