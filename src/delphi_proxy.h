#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// DLL -> Delphi HTTP proxy. The exported API layer should not know endpoint
// details or Delphi response parsing rules.
class DelphiProxy {
public:
    explicit DelphiProxy(const std::string& baseUrl);

    bool Ping();

    bool ProcessStart(const std::string& requestId, const std::string& saveDir, const std::string& callbacksJson = "{}");
    bool ProcessEnd();

    bool SwitchTerminal(int terminalIndex);

    bool CaptureFace(const std::string& requestId,
                     const std::string& saveDir,
                     std::string& outSavePath);

    bool CaptureFingerprint(const std::string& requestId,
                            const std::string& saveDir,
                            std::string& outSavePath);

    bool CaptureIrisAsync(const std::string& requestId,
                          const std::string& saveDir,
                          const std::string& callbackUrl);

    bool RequestOcrAsync(const std::string& requestId,
                         const std::string& saveDir,
                         const std::string& callbackUrl);

    bool RequestNfcAsync(const std::string& requestId,
                         const std::string& saveDir,
                         const std::string& callbackUrl);

    bool StartCameraPreview(const std::string& requestId,
                            intptr_t thirdPartyHwnd,
                            const std::string& callbackUrl);

    bool StopCameraPreview(const std::string& requestId);

    bool StartFingerprintPreview(const std::string& requestId,
                                  intptr_t thirdPartyHwnd,
                                  const std::string& callbackUrl);

    bool StopFingerprintPreview(const std::string& requestId);

    bool StartIrisPreview(const std::string& requestId,
                           intptr_t thirdPartyHwnd,
                           const std::string& callbackUrl);

    bool StopIrisPreview(const std::string& requestId);

private:
    std::string baseUrl_;

    bool Get(const std::string& path, std::string& response);
    bool PostJson(const std::string& path,
                  const std::string& body,
                  std::string& response);

    bool IsOkResponse(const std::string& response);
    bool IsAcceptedResponse(const std::string& response);
    bool ExtractSavePath(const std::string& response,
                         std::string& outSavePath);

    std::string BuildUrl(const std::string& path) const;
};

} // namespace HZCYKJTHardWare
