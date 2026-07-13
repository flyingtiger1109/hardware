#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// DLL -> Delphi HTTP proxy. The exported API layer should not know endpoint
// details or Delphi response parsing rules.
class DelphiProxy {
public:
    explicit DelphiProxy(const std::string& baseUrl);

    bool Ping();
    bool GetInstanceId(std::string& outInstanceId, int timeoutMs = 1000);

    bool ProcessStart(const std::string& requestId,
                      const std::string& saveDir,
                      const std::string& callbacksJson = "{}");
    bool ProcessEnd();

    bool SwitchTerminal(int terminalIndex);

    bool CaptureFace(const std::string& requestId,
                     const std::string& saveDir,
                     std::string& outSavePath,
                     int timeoutMs);

    bool CaptureFingerprint(const std::string& requestId,
                            const std::string& saveDir,
                            const std::string& saveDirHk,
                            std::string& outSavePath,
                            int timeoutMs);

    bool CaptureIrisAsync(const std::string& requestId,
                          const std::string& saveDir,
                          const std::string& callbackUrl);

    bool RequestOcrAsync(const std::string& requestId,
                         const std::string& saveDir,
                         const std::string& callbackUrl);

    bool RequestNfcAsync(const std::string& requestId,
                         const std::string& saveDir,
                         const std::string& callbackUrl);

    bool GetCameraPreviewUrl(const std::string& requestId, std::string& outPreviewUrl);
    bool GetFingerprintPreviewUrl(const std::string& requestId, std::string& outPreviewUrl);
    bool GetIrisPreviewUrl(const std::string& requestId, std::string& outPreviewUrl);

    // Legacy server-rendered preview endpoints retained for mixed-version deployment.
    bool StartCameraPreview(const std::string& requestId,
                            intptr_t thirdPartyHwnd,
                            const std::string& callbackUrl,
                            int timeoutMs = -1);

    bool StopCameraPreview(const std::string& requestId, int timeoutMs = -1);

    bool StartFingerprintPreview(const std::string& requestId,
                                  intptr_t thirdPartyHwnd,
                                  const std::string& callbackUrl,
                                  int timeoutMs = -1);

    bool StopFingerprintPreview(const std::string& requestId, int timeoutMs = -1);

    bool StartIrisPreview(const std::string& requestId,
                           intptr_t thirdPartyHwnd,
                           const std::string& callbackUrl);

    bool StopIrisPreview(const std::string& requestId);

    bool StartPlatePreview(const std::string& plateCode,
                           const std::string& requestId,
                           intptr_t thirdPartyHwnd,
                           const std::string& callbackUrl,
                           int timeoutMs = -1);

    bool StopPlatePreview(const std::string& plateCode,
                          const std::string& requestId,
                          int timeoutMs = -1);

    bool RequestAuthorize(const std::string& requestId,
                          const std::string& ZJHM,
                          const std::string& ZJLB,
                          const std::string& GJDQDM,
                          const std::string& XM,
                          const std::string& XB,
                          const std::string& CSRQ,
                          const std::string& KADM,
                          const std::string& callbackUrl,
                          int timeoutMs = 0);

private:
    std::string baseUrl_;

    bool Get(const std::string& path,
             std::string& response,
             int timeoutMs = -1,
             bool quiet = false);
    bool PostJson(const std::string& path,
                   const std::string& body,
                   std::string& response,
                   int timeoutMs = -1,
                   bool logRawResponse = true);

    bool IsOkResponse(const std::string& response);
    bool IsAcceptedResponse(const std::string& response);
    bool ExtractSavePath(const std::string& response,
                         std::string& outSavePath);
    bool GetPreviewUrl(const std::string& path,
                       const std::string& requestId,
                       std::string& outPreviewUrl);

    std::string BuildUrl(const std::string& path) const;
};

} // namespace HZCYKJTHardWare
