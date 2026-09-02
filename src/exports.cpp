#include "pch.h"
#include "include/HZCYKJTHardWare.h"
#include "hzsjkjt_context.h"
#include "sdk_runtime.h"
#include "config_manager.h"
#include "logger.h"
#include "network_detector.h"
#include "terminal_manager.h"
#include "terminal_status_checker.h"
#include "http_client.h"
#include "delphi_proxy.h"
#include "callback_server.h"
#include "request_session_manager.h"
#include "event_dispatcher.h"
#include "preview_manager.h"
#include "path_helper.h"
#include "result_parser.h"
#include "image_saver.h"
#include <tlhelp32.h>

// 导出函数通过小型主体辅助函数实现，避免 SEH 包装范围跨越 C++ 对象生命周期

using HZCYKJTHardWare::HzsjkjtContext;
using HZCYKJTHardWare::PlatePreviewCameraConfig;
using HZCYKJTHardWare::PlatePreviewChannel;
using HZCYKJTHardWare::PlatePreviewState;

enum class DeviceCapability {
    PlateCJ, PlateRJ2, PlateRJ3, Face, Fingerprint, Iris, OCR, NfcCard,
    Authorize, TerminalControl, ProcessControl
};

static void PostCaptureEvent(const std::string& requestId,
                             const std::string& resourceType,
                             int eventType, int status,
                             const char* errorCode, const char* message,
                             const char* savePath, const char* rawJson);

static const char* CapabilityName(DeviceCapability capability) {
    switch (capability) {
        case DeviceCapability::PlateCJ: return "PlateCJ";
        case DeviceCapability::PlateRJ2: return "PlateRJ2";
        case DeviceCapability::PlateRJ3: return "PlateRJ3";
        case DeviceCapability::Face: return "Face";
        case DeviceCapability::Fingerprint: return "Fingerprint";
        case DeviceCapability::Iris: return "Iris";
        case DeviceCapability::OCR: return "OCR";
        case DeviceCapability::NfcCard: return "NfcCard";
        case DeviceCapability::Authorize: return "Authorize";
        case DeviceCapability::TerminalControl: return "TerminalControl";
        case DeviceCapability::ProcessControl: return "ProcessControl";
        default: return "Unknown";
    }
}

static const char* CapabilityDisplayName(DeviceCapability capability) {
    switch (capability) {
        case DeviceCapability::PlateCJ: return "车牌CJ";
        case DeviceCapability::PlateRJ2: return "车牌RJ2";
        case DeviceCapability::PlateRJ3: return "车牌RJ3";
        case DeviceCapability::Face: return "人脸";
        case DeviceCapability::Fingerprint: return "指纹";
        case DeviceCapability::Iris: return "虹膜";
        case DeviceCapability::OCR: return "OCR";
        case DeviceCapability::NfcCard: return "NFC卡";
        case DeviceCapability::Authorize: return "授权";
        case DeviceCapability::TerminalControl: return "终端控制";
        case DeviceCapability::ProcessControl: return "流程控制";
        default: return "未知";
    }
}

static bool IsCapabilitySupported(DeviceCapability capability) {
    auto& ctx = HZCYKJTHardWare::HzsjkjtContext::Instance();
    auto lock = HZCYKJTHardWare::ReadLock();
    return ctx.device_mode != 2 || capability == DeviceCapability::PlateRJ2 ||
        capability == DeviceCapability::PlateRJ3;
}

static int RequireCapability(const char* interfaceName,
                             DeviceCapability capability) {
    if (IsCapabilitySupported(capability)) return HZCYKJTHardWare_RET_OK;
    static std::mutex warningMutex;
    static std::map<std::string, std::pair<int64_t, int>> warnings;
    const int64_t now = static_cast<int64_t>(GetTickCount64());
    const std::string key = std::string(interfaceName ? interfaceName : "unknown") +
        "|" + CapabilityName(capability);
    std::lock_guard<std::mutex> lock(warningMutex);
    auto& state = warnings[key];
    if (state.first != 0 && now - state.first < 60000) {
        ++state.second;
        return HZCYKJTHardWare_RET_UNSUPPORTED;
    }
    LOG_WARN("能力检查", "接口=%s，设备模式(DeviceMode)=2，能力=%s，结果=不支持，已抑制次数=%d",
             interfaceName, CapabilityDisplayName(capability), state.second);
    state = {now, 0};
    return HZCYKJTHardWare_RET_UNSUPPORTED;
}

static bool RejectUnsupportedAsync(const char* interfaceName,
                                   DeviceCapability capability,
                                   const char* resourceType,
                                   int failedEventType) {
    if (RequireCapability(interfaceName, capability) == HZCYKJTHardWare_RET_OK)
        return false;
    static std::atomic<unsigned long> sequence{0};
    const std::string requestId = "not-supported-" +
        std::to_string(GetTickCount64()) + "-" +
        std::to_string(++sequence);
    PostCaptureEvent(requestId, resourceType, failedEventType,
        HZCYKJTHardWare_RET_UNSUPPORTED, "not_supported",
        "Current DeviceMode does not support this capability", nullptr, nullptr);
    return true;
}

// BusyGuard 已移除，并发控制由 C# Proxy Scheduler 负责

struct SwitchPendingScope {
    std::atomic<bool>& flag;
    bool acquired;

    explicit SwitchPendingScope(std::atomic<bool>& switchFlag)
        : flag(switchFlag), acquired(false) {
        bool expected = false;
        acquired = flag.compare_exchange_strong(expected, true);
    }

    ~SwitchPendingScope() {
        if (acquired) {
            flag.store(false);
        }
    }
};

// 检查终端切换是否进行中；切换期间拒绝新操作，保证切换优先
static bool IsSwitchPending() {
    return HZCYKJTHardWare::HzsjkjtContext::Instance().switch_pending.load();
}

static int ProxyFailureCode(const HZCYKJTHardWare::DelphiProxy& proxy) {
    return proxy.LastResultCode() == HZCYKJTHardWare_RET_UNSUPPORTED
        ? HZCYKJTHardWare_RET_UNSUPPORTED
        : HZCYKJTHardWare_RET_HTTP_FAILED;
}

static std::string LogValue(const std::string& value, size_t maxLength = 256) {
    std::string result;
    result.reserve(value.size());
    for (char ch : value) {
        unsigned char c = static_cast<unsigned char>(ch);
        if (ch == '\r' || ch == '\n' || ch == '\t' || c < 0x20) {
            result += ' ';
        } else {
            result += ch;
        }
        if (result.size() >= maxLength) {
            result += "...";
            break;
        }
    }
    return result;
}

static std::string LogValue(const char* value, size_t maxLength = 256) {
    return LogValue(std::string(value ? value : ""), maxLength);
}

static void LogLatestPlateFrameOperation(
    int resultCode, const std::string& captureRequestId,
    const std::string& previewRequestId, const char* plateName,
    const std::string& path, size_t bytes,
    const HZCYKJTHardWare::LatestPlateFrameMetadata& metadata,
    ULONGLONG durationMs) {
    const char* result = resultCode == HZCYKJTHardWare_RET_OK
        ? "Success" : "Failed";
    const std::string errorCode = resultCode == HZCYKJTHardWare_RET_OK
        ? "none" : std::to_string(resultCode);
    const std::string source = metadata.source.empty() ? "unknown" : metadata.source;
    const char* captureId = captureRequestId.empty() ? "<无>" : captureRequestId.c_str();
    const char* previewId = previewRequestId.empty() ? "<无>" : previewRequestId.c_str();
    const char* plate = (plateName && plateName[0]) ? plateName : "unknown";

    const char* format =
        "Operation=SaveLatestPlateFrame RequestId=%s CaptureRequestId=%s "
        "PreviewRequestId=%s Plate=%s Result=%s ErrorCode=%s DurationMs=%llu "
        "Path=%s Bytes=%zu Width=%d Height=%d FrameAgeMs=%lld Source=%s";
    if (resultCode == HZCYKJTHardWare_RET_OK) {
        LOG_INFO("接口", format, captureId, captureId, previewId, plate, result,
                 errorCode.c_str(), static_cast<unsigned long long>(durationMs),
                 LogValue(path).c_str(), bytes, metadata.width, metadata.height,
                 metadata.frameAgeMs, LogValue(source).c_str());
    } else {
        LOG_ERROR("接口", format, captureId, captureId, previewId, plate, result,
                  errorCode.c_str(), static_cast<unsigned long long>(durationMs),
                  LogValue(path).c_str(), bytes, metadata.width, metadata.height,
                  metadata.frameAgeMs, LogValue(source).c_str());
    }
}

// 业务排队统一由 C# Proxy Scheduler 管理

class IrisPreviewRestoreWorker {
public:
    static IrisPreviewRestoreWorker& Instance() {
        // 由 ReleaseSdk 显式停止线程。进程退出时不析构该对象，避免在
        // Windows loader lock 中等待后台线程。
        static IrisPreviewRestoreWorker* worker = new IrisPreviewRestoreWorker();
        return *worker;
    }

    void Enqueue(const std::string& delphiServerUrl,
                 const std::string& requestId,
                 HWND hwnd) {
        std::lock_guard<std::mutex> lifecycleLock(lifecycleMutex_);
        {
            std::lock_guard<std::mutex> lock(mutex_);
            if (!thread_.joinable()) {
                stopping_ = false;
                thread_ = std::thread([this]() { Run(); });
            }
            delphiServerUrl_ = delphiServerUrl;
            requestId_ = requestId;
            hwnd_ = hwnd;
            hasPending_ = true;
        }
        LOG_DEBUG("接口", "虹膜预览恢复已进入后台队列");
        cv_.notify_one();
    }

    void Stop() {
        std::lock_guard<std::mutex> lifecycleLock(lifecycleMutex_);
        {
            std::lock_guard<std::mutex> lock(mutex_);
            stopping_ = true;
            hasPending_ = false;
            delphiServerUrl_.clear();
            requestId_.clear();
            hwnd_ = nullptr;
        }
        cv_.notify_all();
        if (thread_.joinable()) {
            thread_.join();
        }
    }

private:
    IrisPreviewRestoreWorker() = default;

    void Run() {
        for (;;) {
            std::string delphiServerUrl;
            std::string requestId;
            HWND hwnd = nullptr;
            {
                std::unique_lock<std::mutex> lock(mutex_);
                cv_.wait(lock, [this]() { return stopping_ || hasPending_; });
                if (stopping_) {
                    return;
                }
                delphiServerUrl = delphiServerUrl_;
                requestId = requestId_;
                hwnd = hwnd_;
                hasPending_ = false;
            }

            if (delphiServerUrl.empty() || requestId.empty() || hwnd == nullptr) {
                LOG_WARN("接口", "虹膜预览恢复请求参数无效，已跳过：request_id=%s", requestId.c_str());
                continue;
            }

            HZCYKJTHardWare::DelphiProxy proxy(delphiServerUrl);
            std::string rtspUrl;
            bool restored = false;
            for (int attempt = 1; attempt <= 50; ++attempt) {
                {
                    std::lock_guard<std::mutex> lock(mutex_);
                    if (hasPending_ || stopping_) {
                        LOG_WARN("接口", "虹膜预览恢复请求被新的切换恢复请求替换：request_id=%s", requestId.c_str());
                        break;
                    }
                }
                if (proxy.GetIrisPreviewUrl(requestId, rtspUrl) &&
                    HZCYKJTHardWare::PreviewManager::Instance().StartIrisPreviewFromUrl(hwnd, rtspUrl) == HZCYKJTHardWare_RET_OK) {
                    LOG_INFO("接口", "切换终端后虹膜预览已恢复");
                    restored = true;
                    break;
                }
                Sleep(200);
            }
            if (!restored) {
                LOG_ERROR("接口", "切换终端后虹膜预览后台恢复失败或已被替换：request_id=%s", requestId.c_str());
            }
        }
    }

    std::mutex lifecycleMutex_;
    std::mutex mutex_;
    std::condition_variable cv_;
    std::thread thread_;
    bool stopping_ = false;
    bool hasPending_ = false;
    std::string delphiServerUrl_;
    std::string requestId_;
    HWND hwnd_ = nullptr;
};

static std::string GenerateSyncRequestId(const char* prefix) {
    static std::atomic<int> seq{0};
    int currentSeq = ++seq;
    char seqBuf[16];
    snprintf(seqBuf, sizeof(seqBuf), "%03d", currentSeq);
    return std::string(prefix) + "_" + HZCYKJTHardWare::PathHelper::GetTimestampString() + "_" + seqBuf;
}

// 导出调用的关联 ID 只存在于当前 DLL 调用线程，不改变任何导出 ABI。
static thread_local std::string g_currentExportRequestId;

static const char* ExportRequestIdPrefix(const char* operation) {
    if (!operation) return "HZCYKJTHardWare_EXPORT";
    if (std::strcmp(operation, "HZCYKJTHardWare_SwitchTerminal") == 0)
        return "HZCYKJTHardWare_SWITCH";
    if (std::strcmp(operation, "HZCYKJTHardWare_StartProcess") == 0)
        return "HZCYKJTHardWare_PROCESS";
    if (std::strcmp(operation, "HZCYKJTHardWare_EndProcess") == 0)
        return "HZCYKJTHardWare_FLOW_END";
    if (std::strcmp(operation, "HZCYKJTHardWare_StartCameraPreview") == 0 ||
        std::strcmp(operation, "HZCYKJTHardWare_StopCameraPreview") == 0)
        return "HZCYKJTHardWare_PREVIEW";
    if (std::strcmp(operation, "HZCYKJTHardWare_StartFingerprintPreview") == 0 ||
        std::strcmp(operation, "HZCYKJTHardWare_StopFingerprintPreview") == 0)
        return "HZCYKJTHardWare_FP_PREVIEW";
    if (std::strcmp(operation, "HZCYKJTHardWare_StartIrisPreview") == 0 ||
        std::strcmp(operation, "HZCYKJTHardWare_StopIrisPreview") == 0)
        return "HZCYKJTHardWare_IRIS_PREVIEW";
    if (std::strcmp(operation, "HZCYKJTHardWare_StartPlatePreviewCJ") == 0 ||
        std::strcmp(operation, "HZCYKJTHardWare_StopPlatePreviewCJ") == 0)
        return "HZCYKJTHardWare_PLATE_PREVIEW_CJ";
    if (std::strcmp(operation, "HZCYKJTHardWare_StartPlatePreviewRJ2") == 0 ||
        std::strcmp(operation, "HZCYKJTHardWare_StopPlatePreviewRJ2") == 0)
        return "HZCYKJTHardWare_PLATE_PREVIEW_RJ2";
    if (std::strcmp(operation, "HZCYKJTHardWare_StartPlatePreviewRJ3") == 0 ||
        std::strcmp(operation, "HZCYKJTHardWare_StopPlatePreviewRJ3") == 0)
        return "HZCYKJTHardWare_PLATE_PREVIEW_RJ3";
    if (std::strcmp(operation, "HZCYKJTHardWare_SaveLatestPlateFrame") == 0)
        return "HZCYKJTHardWare_PLATE_FRAME";
    if (std::strcmp(operation, "HZCYKJTHardWare_CaptureCameraImage") == 0)
        return "HZCYKJTHardWare_FACE";
    if (std::strcmp(operation, "HZCYKJTHardWare_CaptureFingerprintImage") == 0)
        return "HZCYKJTHardWare_FP";
    if (std::strcmp(operation, "HZCYKJTHardWare_CaptureIrisImage") == 0)
        return "HZCYKJTHardWare_IRIS";
    if (std::strcmp(operation, "HZCYKJTHardWare_RequestOCR") == 0)
        return "HZCYKJTHardWare_OCR";
    if (std::strcmp(operation, "HZCYKJTHardWare_RequestNfcCard") == 0)
        return "HZCYKJTHardWare_NFC";
    if (std::strcmp(operation, "HZCYKJTHardWare_RequestAuthorize") == 0)
        return "HZCYKJTHardWare_AUTH";
    return "HZCYKJTHardWare_EXPORT";
}

static void BeginExportRequestContext(const char* operation) {
    g_currentExportRequestId = GenerateSyncRequestId(ExportRequestIdPrefix(operation));
}

static const std::string& CurrentExportRequestId() {
    return g_currentExportRequestId;
}

static std::string GetOrCreateExportRequestId(const char* prefix) {
    if (g_currentExportRequestId.empty())
        g_currentExportRequestId = GenerateSyncRequestId(prefix);
    return g_currentExportRequestId;
}

static void SetExportRequestId(const std::string& requestId) {
    if (!requestId.empty())
        g_currentExportRequestId = requestId;
}

static void ClearExportRequestContext() {
    g_currentExportRequestId.clear();
}

static std::string GetThirdPartyInputEncoding() {
    auto& ctx = HZCYKJTHardWare::HzsjkjtContext::Instance();
    auto lock = HZCYKJTHardWare::ReadLock();
    return ctx.third_party_input_encoding;
}

static bool NormalizeThirdPartyInput(const char* value,
                                     const char* fieldName,
                                     const std::string& encodingMode,
                                     std::string& result) {
    if (HZCYKJTHardWare::PathHelper::NormalizeExternalTextToUtf8(
            value, encodingMode, result)) {
        return true;
    }

    LOG_ERROR("输入编码",
              "第三方输入编码转换失败：field=%s，mode=%s",
              fieldName ? fieldName : "unknown",
              encodingMode.c_str());
    return false;
}

static std::string ResolveSaveRoot(const char* saveDir) {
    auto& ctx = HZCYKJTHardWare::HzsjkjtContext::Instance();
    std::string root = saveDir ? saveDir : "";
    if (!root.empty()) return root;

    auto lock = HZCYKJTHardWare::ReadLock();
    root = ctx.runtime_save_path;
    if (root.empty()) {
        root = ctx.save_default_root;
    }
    return root;
}

static bool HasFileExtension(const std::string& path) {
    std::string fileName = HZCYKJTHardWare::PathHelper::GetFileName(path);
    size_t dot = fileName.find_last_of('.');
    return dot != std::string::npos && dot > 0 && dot + 1 < fileName.size();
}

static std::string ResolveCaptureTargetPath(const char* requestedPath, bool camera) {
    std::string targetPath = requestedPath ? requestedPath : "";
    if (HasFileExtension(targetPath)) {
        return targetPath;
    }

    auto& ctx = HZCYKJTHardWare::HzsjkjtContext::Instance();
    auto lock = HZCYKJTHardWare::ReadLock();
    targetPath = camera ? ctx.save_camera_default_path : ctx.save_fingerprint_default_path;
    if (targetPath.empty()) {
        targetPath = camera ? ".\\captures\\camera.jpg" : ".\\captures\\fingerprint.jpg";
    }
    return targetPath;
}

static void PostCaptureEvent(const std::string& requestId,
                             const std::string& resourceType,
                             int eventType,
                             int status,
                             const char* errorCode,
                             const char* message,
                             const char* savePath = nullptr,
                             const char* rawJson = nullptr) {
    auto& ctx = HZCYKJTHardWare::HzsjkjtContext::Instance();
    std::string terminalBaseUrl;
    int terminalIndex = 0;
    {
        auto lock = HZCYKJTHardWare::ReadLock();
        terminalBaseUrl = ctx.current_terminal_base_url;
        terminalIndex = ctx.current_terminal_index;
    }

    HZCYKJTHardWare_EVENT event;
    memset(&event, 0, sizeof(event));
    event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
    event.event_type = eventType;
    event.request_id = requestId.c_str();
    event.resource_type = resourceType.c_str();
    event.status = status;
    event.error_code = errorCode;
    event.message = message;
    event.terminal_base_url = terminalBaseUrl.c_str();
    event.terminal_index = terminalIndex;
    event.save_path = savePath;
    event.raw_json = rawJson;
    HZCYKJTHardWare::EventDispatcher::Instance().PostEvent(event);
}

static int GetVersionBody(char* buffer, int bufferSize) {
    static const char* kVersion = "1.0";
    if (!buffer || bufferSize <= 0) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }
    int required = (int)strlen(kVersion) + 1;
    if (bufferSize < required) {
        return HZCYKJTHardWare_RET_BUFFER_TOO_SMALL;
    }
    memcpy(buffer, kVersion, required);
    return HZCYKJTHardWare_RET_OK;
}

static std::string BuildCallbackUrl(const HZCYKJTHardWare::HzsjkjtContext& ctx,
                                    const char* route) {
    return "http://" + ctx.callback_url_host + ":" +
        std::to_string(ctx.callback_server_port) +
        ctx.callback_server_base_path + route;
}

static bool IsAbsoluteWindowsPath(const std::string& path) {
    return (path.size() > 1 && path[1] == ':') ||
        (path.size() > 1 && (path[0] == '\\' || path[0] == '/') &&
         (path[1] == '\\' || path[1] == '/'));
}

static std::string ResolveDelphiExecutablePath(const HZCYKJTHardWare::ConfigManager& cfg,
                                               const std::string& dllDir) {
    std::string executable = cfg.GetDelphiExecutable();
    return IsAbsoluteWindowsPath(executable)
        ? executable
        : HZCYKJTHardWare::PathHelper::Join(dllDir, executable);
}

static std::wstring GetFullWindowsPath(const std::string& path) {
    using namespace HZCYKJTHardWare;

    std::wstring input = PathHelper::Utf8ToWide(path);
    wchar_t fullPath[MAX_PATH] = {0};
    DWORD length = GetFullPathNameW(input.c_str(), MAX_PATH, fullPath, nullptr);
    if (length == 0 || length >= MAX_PATH) {
        return input;
    }
    return std::wstring(fullPath, length);
}

static bool FindProcessIdsForExecutablePath(const std::string& executablePath,
                                            std::vector<DWORD>& processIds) {
    using namespace HZCYKJTHardWare;

    processIds.clear();
    std::wstring executableName = PathHelper::Utf8ToWide(PathHelper::GetFileName(executablePath));
    std::wstring expectedPath = GetFullWindowsPath(executablePath);
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) {
        LOG_WARN("接口", "检查硬件控制程序进程失败：error=%lu", GetLastError());
        return false;
    }

    PROCESSENTRY32W entry = {};
    entry.dwSize = sizeof(entry);
    if (Process32FirstW(snapshot, &entry)) {
        do {
            if (_wcsicmp(entry.szExeFile, executableName.c_str()) == 0) {
                HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, entry.th32ProcessID);
                if (!process) {
                    LOG_WARN("接口", "检查同名进程路径失败：pid=%lu，error=%lu",
                             entry.th32ProcessID, GetLastError());
                    continue;
                }

                wchar_t actualPath[MAX_PATH] = {0};
                DWORD actualPathLength = MAX_PATH;
                if (QueryFullProcessImageNameW(process, 0, actualPath, &actualPathLength) &&
                    _wcsicmp(actualPath, expectedPath.c_str()) == 0) {
                    processIds.push_back(entry.th32ProcessID);
                }
                CloseHandle(process);
            }
        } while (Process32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);
    return true;
}

static bool TerminateDelphiServiceProcesses(const std::string& executablePath,
                                            const std::vector<DWORD>& processIds) {
    for (DWORD processId : processIds) {
        HANDLE process = OpenProcess(PROCESS_TERMINATE | SYNCHRONIZE, FALSE, processId);
        if (!process) {
            DWORD error = GetLastError();
            if (error == ERROR_INVALID_PARAMETER) {
                continue;
            }
            LOG_ERROR("接口", "重启硬件控制程序失败：无法打开待终止进程，path=%s，pid=%lu，error=%lu",
                      executablePath.c_str(), processId, error);
            return false;
        }

        if (!TerminateProcess(process, 1)) {
            DWORD error = GetLastError();
            CloseHandle(process);
            LOG_ERROR("接口", "重启硬件控制程序失败：终止进程失败，path=%s，pid=%lu，error=%lu",
                      executablePath.c_str(), processId, error);
            return false;
        }

        DWORD waitResult = WaitForSingleObject(process, 5000);
        CloseHandle(process);
        if (waitResult != WAIT_OBJECT_0) {
            LOG_ERROR("接口", "重启硬件控制程序失败：等待旧进程退出超时，path=%s，pid=%lu，result=%lu",
                      executablePath.c_str(), processId, waitResult);
            return false;
        }
        LOG_WARN("SDK生命周期", "通信服务不可用，已终止旧硬件控制程序：path=%s，pid=%lu",
                 executablePath.c_str(), processId);
    }
    return true;
}

static bool StartDelphiServiceProcess(const HZCYKJTHardWare::ConfigManager& cfg,
                                      const std::string& dllDir,
                                      std::string& executablePath) {
    using namespace HZCYKJTHardWare;

    executablePath = ResolveDelphiExecutablePath(cfg, dllDir);
    if (!PathHelper::FileExists(executablePath)) {
        LOG_ERROR("接口", "自动启动硬件控制程序失败：可执行文件不存在，path=%s", executablePath.c_str());
        return false;
    }

    std::wstring wExecutable = PathHelper::Utf8ToWide(executablePath);
    std::string workingDir = PathHelper::GetParentDir(executablePath);
    std::wstring wWorkingDir = PathHelper::Utf8ToWide(workingDir);
    std::wstring commandLine = L"\"" + wExecutable + L"\"";
    std::vector<wchar_t> mutableCommandLine(commandLine.begin(), commandLine.end());
    mutableCommandLine.push_back(L'\0');

    STARTUPINFOW startupInfo = {};
    startupInfo.cb = sizeof(startupInfo);
    PROCESS_INFORMATION processInfo = {};
    BOOL created = CreateProcessW(wExecutable.c_str(),
                                  mutableCommandLine.data(),
                                  nullptr,
                                  nullptr,
                                  FALSE,
                                  0,
                                  nullptr,
                                  wWorkingDir.empty() ? nullptr : wWorkingDir.c_str(),
                                  &startupInfo,
                                  &processInfo);
    if (!created) {
        LOG_ERROR("接口", "自动启动硬件控制程序失败：CreateProcessW失败，path=%s，error=%lu",
                  executablePath.c_str(), GetLastError());
        return false;
    }

    LOG_INFO("SDK生命周期", "硬件控制程序启动成功：path=%s，pid=%lu",
             executablePath.c_str(), processInfo.dwProcessId);
    CloseHandle(processInfo.hThread);
    CloseHandle(processInfo.hProcess);
    return true;
}

static bool WaitForDelphiService(HZCYKJTHardWare::DelphiProxy& proxy,
                                 int waitMs,
                                 int intervalMs) {
    ULONGLONG deadline = GetTickCount64() + static_cast<ULONGLONG>(waitMs);
    do {
        if (proxy.Ping()) {
            return true;
        }
        Sleep(static_cast<DWORD>(intervalMs));
    } while (GetTickCount64() < deadline);
    return false;
}

static bool EnsureDelphiServiceAvailable(HZCYKJTHardWare::DelphiProxy& proxy,
                                         const HZCYKJTHardWare::ConfigManager& cfg,
                                         const std::string& dllDir,
                                         const std::string& delphiServerUrl) {
    if (proxy.Ping()) {
        return true;
    }
    if (!cfg.GetDelphiAutoStart()) {
        LOG_ERROR("SDK生命周期", "硬件控制程序未运行且自动启动未启用：服务地址=%s",
                  HZCYKJTHardWare::SanitizeUrlForLog(delphiServerUrl).c_str());
        return false;
    }

    LOG_INFO("SDK生命周期", "硬件控制程序未运行，准备自动启动：服务地址=%s",
             HZCYKJTHardWare::SanitizeUrlForLog(delphiServerUrl).c_str());

    std::string executablePath = ResolveDelphiExecutablePath(cfg, dllDir);
    if (!HZCYKJTHardWare::PathHelper::FileExists(executablePath)) {
        LOG_ERROR("SDK生命周期", "自动启动硬件控制程序失败：可执行文件不存在，path=%s", executablePath.c_str());
        return false;
    }

    int intervalMs = cfg.GetDelphiPingIntervalMs();
    std::vector<DWORD> existingProcessIds;
    if (!FindProcessIdsForExecutablePath(executablePath, existingProcessIds)) {
        LOG_ERROR("SDK生命周期", "自动恢复硬件控制程序失败：无法检查同路径进程，path=%s",
                  executablePath.c_str());
        return false;
    }

    if (!existingProcessIds.empty()) {
        LOG_WARN("SDK生命周期", "硬件控制程序已运行但通信服务不可用，正在立即重启同路径进程：path=%s，pid=%lu，服务地址=%s",
                 executablePath.c_str(), existingProcessIds.front(),
                 HZCYKJTHardWare::SanitizeUrlForLog(delphiServerUrl).c_str());
        if (!TerminateDelphiServiceProcesses(executablePath, existingProcessIds)) {
            return false;
        }
    }

    if (!StartDelphiServiceProcess(cfg, dllDir, executablePath)) {
        return false;
    }

    int waitMs = cfg.GetDelphiStartWaitMs();
    if (WaitForDelphiService(proxy, waitMs, intervalMs)) {
        LOG_INFO("SDK生命周期", "硬件控制程序通信服务已就绪");
        return true;
    }

    LOG_ERROR("SDK生命周期", "启动硬件控制程序后等待ping超时：服务地址=%s，path=%s，wait_ms=%d",
              HZCYKJTHardWare::SanitizeUrlForLog(delphiServerUrl).c_str(), executablePath.c_str(), waitMs);
    return false;
}

static const char* PlatePreviewCode(PlatePreviewChannel channel) {
    switch (channel) {
        case PlatePreviewChannel::RJ2: return "rj2";
        case PlatePreviewChannel::RJ3: return "rj3";
        case PlatePreviewChannel::CJ:
        default: return "cj";
    }
}

static const char* PlatePreviewDisplayName(PlatePreviewChannel channel) {
    switch (channel) {
        case PlatePreviewChannel::RJ2: return "RJ2";
        case PlatePreviewChannel::RJ3: return "RJ3";
        case PlatePreviewChannel::CJ:
        default: return "CJ";
    }
}

static PlatePreviewState& GetPlatePreviewState(HzsjkjtContext& ctx,
                                               PlatePreviewChannel channel) {
    switch (channel) {
        case PlatePreviewChannel::RJ2: return ctx.plate_preview_rj2;
        case PlatePreviewChannel::RJ3: return ctx.plate_preview_rj3;
        case PlatePreviewChannel::CJ:
        default: return ctx.plate_preview_cj;
    }
}

static bool TryGetPlatePreviewChannel(int cameraType,
                                      PlatePreviewChannel& channel) {
    switch (cameraType) {
        case HZCYKJTHardWare_PLATE_CAMERA_CJ:
            channel = PlatePreviewChannel::CJ;
            return true;
        case HZCYKJTHardWare_PLATE_CAMERA_RJ2:
            channel = PlatePreviewChannel::RJ2;
            return true;
        case HZCYKJTHardWare_PLATE_CAMERA_RJ3:
            channel = PlatePreviewChannel::RJ3;
            return true;
        default:
            return false;
    }
}

struct ExternalPreviewLeaseSnapshot {
    bool cameraActive = false;
    bool fingerprintActive = false;
    bool plateCJActive = false;
    bool plateRJ2Active = false;
    bool plateRJ3Active = false;
    std::string cameraRequestId;
    std::string fingerprintRequestId;
    std::string plateCJRequestId;
    std::string plateRJ2RequestId;
    std::string plateRJ3RequestId;
    intptr_t cameraHwnd = 0;
    intptr_t fingerprintHwnd = 0;
    intptr_t plateCJHwnd = 0;
    intptr_t plateRJ2Hwnd = 0;
    intptr_t plateRJ3Hwnd = 0;
    std::string callbackUrl;
};

static ExternalPreviewLeaseSnapshot CaptureExternalPreviewLeases() {
    using namespace HZCYKJTHardWare;

    ExternalPreviewLeaseSnapshot snapshot;
    std::string invalidCameraRequestId;
    std::string invalidFingerprintRequestId;
    std::string invalidPlateCJRequestId;
    std::string invalidPlateRJ2RequestId;
    std::string invalidPlateRJ3RequestId;
    {
        auto lock = ReadLock();
        auto& ctx = HzsjkjtContext::Instance();
        if (!ctx.initialized || ctx.switch_pending.load()) {
            return snapshot;
        }

        snapshot.callbackUrl = BuildCallbackUrl(ctx, "/preview-ready");
        if (ctx.camera_preview_running) {
            HWND hwnd = reinterpret_cast<HWND>(ctx.camera_preview_third_party_hwnd);
            if (hwnd && IsWindow(hwnd)) {
                snapshot.cameraActive = true;
                snapshot.cameraRequestId = ctx.camera_preview_request_id;
                snapshot.cameraHwnd = ctx.camera_preview_third_party_hwnd;
            } else {
                invalidCameraRequestId = ctx.camera_preview_request_id;
            }
        }
        if (ctx.fingerprint_preview_running) {
            HWND hwnd = reinterpret_cast<HWND>(ctx.fingerprint_preview_third_party_hwnd);
            if (hwnd && IsWindow(hwnd)) {
                snapshot.fingerprintActive = true;
                snapshot.fingerprintRequestId = ctx.fingerprint_preview_request_id;
                snapshot.fingerprintHwnd = ctx.fingerprint_preview_third_party_hwnd;
            } else {
                invalidFingerprintRequestId = ctx.fingerprint_preview_request_id;
            }
        }
        auto capturePlate = [](const PlatePreviewState& state,
                               bool& active,
                               std::string& requestId,
                               intptr_t& targetHwnd,
                               std::string& invalidRequestId) {
            if (!state.running) return;
            HWND hwnd = reinterpret_cast<HWND>(state.third_party_hwnd);
            if (hwnd && IsWindow(hwnd)) {
                active = true;
                requestId = state.request_id;
                targetHwnd = state.third_party_hwnd;
            } else {
                invalidRequestId = state.request_id;
            }
        };
        capturePlate(ctx.plate_preview_cj, snapshot.plateCJActive,
            snapshot.plateCJRequestId, snapshot.plateCJHwnd, invalidPlateCJRequestId);
        capturePlate(ctx.plate_preview_rj2, snapshot.plateRJ2Active,
            snapshot.plateRJ2RequestId, snapshot.plateRJ2Hwnd, invalidPlateRJ2RequestId);
        capturePlate(ctx.plate_preview_rj3, snapshot.plateRJ3Active,
            snapshot.plateRJ3RequestId, snapshot.plateRJ3Hwnd, invalidPlateRJ3RequestId);
    }

    if (!invalidCameraRequestId.empty() || !invalidFingerprintRequestId.empty() ||
        !invalidPlateCJRequestId.empty() || !invalidPlateRJ2RequestId.empty() ||
        !invalidPlateRJ3RequestId.empty()) {
        auto lock = WriteLock();
        auto& ctx = HzsjkjtContext::Instance();
        if (!invalidCameraRequestId.empty() &&
            ctx.camera_preview_request_id == invalidCameraRequestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
            LOG_WARN("预览租约", "摄像头预览宿主HWND已失效，已清理DLL租约：request_id=%s",
                     invalidCameraRequestId.c_str());
        }
        if (!invalidFingerprintRequestId.empty() &&
            ctx.fingerprint_preview_request_id == invalidFingerprintRequestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
            LOG_WARN("预览租约", "指纹预览宿主HWND已失效，已清理DLL租约：request_id=%s",
                     invalidFingerprintRequestId.c_str());
        }
        auto clearInvalidPlate = [](PlatePreviewState& state,
                                    const std::string& invalidRequestId,
                                    const char* plateCode) {
            if (invalidRequestId.empty() || state.request_id != invalidRequestId) return;
            state.running = false;
            state.request_id.clear();
            state.third_party_hwnd = 0;
            LOG_WARN("预览租约", "车牌%s预览宿主HWND已失效，已清理DLL租约：request_id=%s",
                     plateCode, invalidRequestId.c_str());
        };
        clearInvalidPlate(ctx.plate_preview_cj, invalidPlateCJRequestId, "CJ");
        clearInvalidPlate(ctx.plate_preview_rj2, invalidPlateRJ2RequestId, "RJ2");
        clearInvalidPlate(ctx.plate_preview_rj3, invalidPlateRJ3RequestId, "RJ3");
    }
    return snapshot;
}

class ExternalPreviewLeaseMonitor {
public:
    static ExternalPreviewLeaseMonitor& Instance() {
        // 实例保留至进程结束。CRT 清理期间持有 Windows 加载器锁时等待工作线程可能死锁，
        // 因此由 ReleaseSdk 显式停止线程。
        static ExternalPreviewLeaseMonitor* monitor = new ExternalPreviewLeaseMonitor();
        return *monitor;
    }

    void Start(const std::string& proxyUrl,
               const std::string& initialInstanceId,
               int checkIntervalMs) {
        Stop();
        {
            std::lock_guard<std::mutex> lock(mutex_);
            proxyUrl_ = proxyUrl;
            initialInstanceId_ = initialInstanceId;
            intervalMs_ = checkIntervalMs < 250 ? 250 : checkIntervalMs;
            stopping_ = false;
            stateChanged_ = true;
            running_ = true;
            thread_ = std::thread([this]() { Run(); });
        }
        cv_.notify_one();
    }

    void NotifyStateChanged() {
        {
            std::lock_guard<std::mutex> lock(mutex_);
            if (!running_) return;
            stateChanged_ = true;
        }
        cv_.notify_one();
    }

    void Stop() {
        std::thread worker;
        {
            std::lock_guard<std::mutex> lock(mutex_);
            if (!running_) return;
            stopping_ = true;
            stateChanged_ = true;
            worker = std::move(thread_);
        }
        cv_.notify_one();
        if (worker.joinable()) worker.join();
        {
            std::lock_guard<std::mutex> lock(mutex_);
            running_ = false;
            stopping_ = false;
            stateChanged_ = false;
            proxyUrl_.clear();
            initialInstanceId_.clear();
        }
    }

private:
    static constexpr int kMonitorRequestTimeoutMs = 750;

    ExternalPreviewLeaseMonitor() = default;
    ~ExternalPreviewLeaseMonitor() = default;
    ExternalPreviewLeaseMonitor(const ExternalPreviewLeaseMonitor&) = delete;
    ExternalPreviewLeaseMonitor& operator=(const ExternalPreviewLeaseMonitor&) = delete;

    void Run() {
        std::string proxyUrl;
        std::string lastInstanceId;
        int intervalMs = 500;
        {
            std::lock_guard<std::mutex> lock(mutex_);
            proxyUrl = proxyUrl_;
            lastInstanceId = initialInstanceId_;
            intervalMs = intervalMs_;
        }

        bool proxyWasUnavailable = false;
        bool recoveryPending = false;
        for (;;) {
            {
                std::unique_lock<std::mutex> lock(mutex_);
                cv_.wait_for(lock, std::chrono::milliseconds(intervalMs), [this]() {
                    return stopping_ || stateChanged_;
                });
                if (stopping_) return;
                stateChanged_ = false;
            }

            ExternalPreviewLeaseSnapshot snapshot = CaptureExternalPreviewLeases();
            if (!snapshot.cameraActive && !snapshot.fingerprintActive &&
                !snapshot.plateCJActive && !snapshot.plateRJ2Active &&
                !snapshot.plateRJ3Active) {
                recoveryPending = false;
                continue;
            }

            HZCYKJTHardWare::DelphiProxy proxy(proxyUrl);
            std::string currentInstanceId;
            if (!proxy.GetInstanceId(currentInstanceId, kMonitorRequestTimeoutMs)) {
                if (!proxyWasUnavailable) {
                    LOG_WARN("预览租约", "C# Proxy实例暂不可用，保留外部预览租约等待恢复");
                }
                proxyWasUnavailable = true;
                continue;
            }

            if (lastInstanceId.empty()) {
                lastInstanceId = currentInstanceId;
                proxyWasUnavailable = false;
                continue;
            }

            if (!proxyWasUnavailable && !recoveryPending &&
                currentInstanceId == lastInstanceId) {
                continue;
            }

            LOG_DEBUG("预览租约", "检测到C# Proxy实例恢复或变更，正在重建外部预览：old=%s，new=%s",
                     lastInstanceId.c_str(), currentInstanceId.c_str());
            bool restored = true;
            if (snapshot.cameraActive) {
                restored = proxy.StartCameraPreview(snapshot.cameraRequestId,
                    snapshot.cameraHwnd, snapshot.callbackUrl,
                    kMonitorRequestTimeoutMs) && restored;
            }
            if (snapshot.fingerprintActive) {
                restored = proxy.StartFingerprintPreview(snapshot.fingerprintRequestId,
                    snapshot.fingerprintHwnd, snapshot.callbackUrl,
                    kMonitorRequestTimeoutMs) && restored;
            }
            if (snapshot.plateCJActive) {
                restored = proxy.StartPlatePreview("cj", snapshot.plateCJRequestId,
                    snapshot.plateCJHwnd, snapshot.callbackUrl,
                    kMonitorRequestTimeoutMs) && restored;
            }
            if (snapshot.plateRJ2Active) {
                restored = proxy.StartPlatePreview("rj2", snapshot.plateRJ2RequestId,
                    snapshot.plateRJ2Hwnd, snapshot.callbackUrl,
                    kMonitorRequestTimeoutMs) && restored;
            }
            if (snapshot.plateRJ3Active) {
                restored = proxy.StartPlatePreview("rj3", snapshot.plateRJ3RequestId,
                    snapshot.plateRJ3Hwnd, snapshot.callbackUrl,
                    kMonitorRequestTimeoutMs) && restored;
            }

            proxyWasUnavailable = false;
            recoveryPending = !restored;
            if (restored) {
                lastInstanceId = currentInstanceId;
                LOG_DEBUG("预览租约", "C# Proxy重启后的外部预览重建请求已受理");
            } else {
                LOG_WARN("预览租约", "外部预览重建请求未全部受理，将继续重试");
            }
        }
    }

    std::mutex mutex_;
    std::condition_variable cv_;
    std::thread thread_;
    bool running_ = false;
    bool stopping_ = false;
    bool stateChanged_ = false;
    int intervalMs_ = 500;
    std::string proxyUrl_;
    std::string initialInstanceId_;
};

static void StopProxyExternalPreviewsOnRelease() {
    using namespace HZCYKJTHardWare;

    std::string proxyUrl;
    std::string cameraRequestId;
    std::string fingerprintRequestId;
    std::string plateCJRequestId;
    std::string plateRJ2RequestId;
    std::string plateRJ3RequestId;
    {
        auto lock = ReadLock();
        auto& ctx = HzsjkjtContext::Instance();
        proxyUrl = ctx.delphi_server_url;
        if (ctx.camera_preview_running)
            cameraRequestId = ctx.camera_preview_request_id;
        if (ctx.fingerprint_preview_running)
            fingerprintRequestId = ctx.fingerprint_preview_request_id;
        if (ctx.plate_preview_cj.running)
            plateCJRequestId = ctx.plate_preview_cj.request_id;
        if (ctx.plate_preview_rj2.running)
            plateRJ2RequestId = ctx.plate_preview_rj2.request_id;
        if (ctx.plate_preview_rj3.running)
            plateRJ3RequestId = ctx.plate_preview_rj3.request_id;
    }

    if (proxyUrl.empty()) return;
    DelphiProxy proxy(proxyUrl);
    if (!cameraRequestId.empty() &&
        !proxy.StopCameraPreview(cameraRequestId, 750)) {
        LOG_WARN("预览租约", "ReleaseSdk停止Proxy摄像头预览失败，继续执行本地释放：request_id=%s",
                 cameraRequestId.c_str());
    }
    if (!fingerprintRequestId.empty() &&
        !proxy.StopFingerprintPreview(fingerprintRequestId, 750)) {
        LOG_WARN("预览租约", "ReleaseSdk停止Proxy指纹预览失败，继续执行本地释放：request_id=%s",
                 fingerprintRequestId.c_str());
    }
    auto stopPlate = [&proxy](const char* plateCode,
                              const std::string& requestId) {
        if (requestId.empty() || proxy.StopPlatePreview(plateCode, requestId, 750)) return;
        LOG_WARN("预览租约", "ReleaseSdk停止Proxy车牌%s预览失败，继续执行本地释放：request_id=%s",
                 plateCode, requestId.c_str());
    };
    stopPlate("cj", plateCJRequestId);
    stopPlate("rj2", plateRJ2RequestId);
    stopPlate("rj3", plateRJ3RequestId);
}

static int InitSdkBody() {
    using namespace HZCYKJTHardWare;

    auto& ctx = HzsjkjtContext::Instance();

    {
        auto lock = ReadLock();
        if (ctx.initialized) {
            return HZCYKJTHardWare_RET_OK;
        }
    }

    // 初始化日志
    std::string logDir = PathHelper::Join(ctx.dll_dir, "HZCYKJTHardWareDLL_Logs");
    bool logOk = Logger::Instance().Init(logDir);
    if (!logOk) {
        logDir = "HZCYKJTHardWareDLL_Logs";
        logOk = Logger::Instance().Init(logDir);
    }
    if (!logOk) {
        wchar_t tempPath[MAX_PATH] = {0};
        GetTempPathW(MAX_PATH, tempPath);
        std::wstring ws(tempPath);
        if (!ws.empty() && ws.back() != L'\\') ws += L'\\';
        ws += L"HZCYKJTHardWareDLL_Logs";
        logDir = PathHelper::WideToUtf8(ws);
        logOk = Logger::Instance().Init(logDir);
    }

    ConfigManager cfg;
    int ret = cfg.Load(ctx.dll_dir);
    if (ret == HZCYKJTHardWare_RET_CONFIG_INVALID) {
        Logger::Instance().Shutdown();
        return HZCYKJTHardWare_RET_CONFIG_INVALID;
    }

    Logger::Instance().ConfigureRetention(
        cfg.GetLogRetentionDays(),
        cfg.GetLogMaxTotalSizeMb(),
        cfg.GetLogDiskWarningFreeMb(),
        cfg.GetLogFlushIntervalMs(),
        cfg.GetLogFlushBatchSize());

    std::string cfgLogDir = cfg.GetLogDir();
    if (!cfgLogDir.empty() && cfgLogDir != "HZCYKJTHardWareDLL_Logs") {
        bool absoluteLogDir =
            cfgLogDir[0] == '\\' ||
            cfgLogDir[0] == '/' ||
            (cfgLogDir.size() > 1 && cfgLogDir[1] == ':');
        if (!absoluteLogDir) {
            cfgLogDir = PathHelper::Join(ctx.dll_dir, cfgLogDir);
        }
        if (!Logger::Instance().Init(cfgLogDir)) {
            Logger::Instance().Init(logDir); // 切换失败，回退到原路径
        }
    }

    std::string cfgLogLevel = cfg.GetLogLevel();
    if (cfgLogLevel == "debug") {
        Logger::Instance().SetLevel(LogLevel::Debug);
    } else if (cfgLogLevel == "warn") {
        Logger::Instance().SetLevel(LogLevel::Warn);
    } else if (cfgLogLevel == "error") {
        Logger::Instance().SetLevel(LogLevel::Error);
    }

    std::string delphiServerUrl = cfg.GetDelphiServerUrl();

    {
        auto lock = WriteLock();
        ctx.delphi_server_url = delphiServerUrl;
        ctx.device_mode = cfg.GetDeviceMode();
        ctx.current_terminal_index = 0;
        ctx.current_terminal_base_url = delphiServerUrl;
        ctx.selected_lan_ip.clear();
        ctx.selected_subnet_prefix.clear();

        ctx.http_connect_timeout_ms = cfg.GetHttpConnectTimeoutMs();
        ctx.http_request_timeout_ms = cfg.GetHttpRequestTimeoutMs();
        ctx.face_capture_timeout_ms = cfg.GetFaceCaptureTimeoutMs();
        ctx.fingerprint_capture_timeout_ms = cfg.GetFingerprintCaptureTimeoutMs();
        ctx.ocr_timeout_ms = cfg.GetOcrTimeoutMs();
        ctx.authorize_timeout_ms = cfg.GetAuthorizeTimeoutMs();
        ctx.third_party_input_encoding = cfg.GetThirdPartyInputEncoding();

        ctx.save_default_root = cfg.GetSaveDefaultRoot();
        ctx.save_camera_default_path = cfg.GetCameraDefaultPath();
        ctx.save_fingerprint_default_path = cfg.GetFingerprintDefaultPath();
        ctx.save_create_date_folder = cfg.GetCreateDateFolder();
        ctx.save_create_request_folder = cfg.GetCreateRequestFolder();
        ctx.callback_server_base_path = cfg.GetCallbackBasePath();
        ctx.rtsp_network_caching_ms = cfg.GetRtspNetworkCachingMs();
        ctx.rtsp_live_caching_ms = cfg.GetRtspLiveCachingMs();
        ctx.rtsp_transport = cfg.GetRtspTransport();
        ctx.preview_check_hwnd_interval_ms = cfg.GetCheckHwndIntervalMs();
        auto loadPlateConfig = [&cfg](PlatePreviewState& state,
                                      PlatePreviewChannel channel) {
            const PlatePreviewCameraConfig& config = cfg.GetPlatePreviewConfig(channel);
            state.enabled = config.enabled;
            state.rtsp_url = cfg.BuildPlatePreviewUrl(channel);
            state.stream_channel = config.stream_channel;
        };
        loadPlateConfig(ctx.plate_preview_cj, PlatePreviewChannel::CJ);
        loadPlateConfig(ctx.plate_preview_rj2, PlatePreviewChannel::RJ2);
        loadPlateConfig(ctx.plate_preview_rj3, PlatePreviewChannel::RJ3);
    }

    LOG_INFO("配置管理", "配置加载成功：设备模式(DeviceMode)=%d，能力列表=[%s]，输入编码=%s",
             cfg.GetDeviceMode(), cfg.GetDeviceMode() == 2 ? "车牌RJ2，车牌RJ3" : "全部",
             cfg.GetThirdPartyInputEncoding().c_str());

    auto warnInvalidPlateConfig = [&cfg](PlatePreviewChannel channel) {
        const PlatePreviewCameraConfig& config = cfg.GetPlatePreviewConfig(channel);
        if (config.enabled && cfg.BuildPlatePreviewUrl(channel).empty()) {
            LOG_WARN("配置管理", "车牌%s预览已启用但相机host为空，启动接口将返回RTSP_URL_EMPTY",
                     PlatePreviewDisplayName(channel));
        }
    };
    warnInvalidPlateConfig(PlatePreviewChannel::CJ);
    warnInvalidPlateConfig(PlatePreviewChannel::RJ2);
    warnInvalidPlateConfig(PlatePreviewChannel::RJ3);

    std::string callbackHost = cfg.GetCallbackServerHost();
    if (callbackHost.empty()) {
        callbackHost = "127.0.0.1";
    }

    std::string listenHost = cfg.GetListenAny() ? "0.0.0.0" : callbackHost;
    int callbackPort = cfg.GetCallbackServerPort();
    std::string callbackUrl = "http://" + callbackHost + ":" +
        std::to_string(callbackPort) + cfg.GetCallbackBasePath();

    LOG_DEBUG("接口", "正在启动硬件控制程序回调接收服务：listen=%s:%d，回调地址=%s",
             listenHost.c_str(), callbackPort, SanitizeUrlForLog(callbackUrl).c_str());

    {
        auto lock = WriteLock();
        ctx.callback_server_host = listenHost;
        ctx.callback_server_port = callbackPort;
        ctx.callback_url_host = callbackHost;
    }

    ret = CallbackServer::Instance().Start(listenHost, callbackPort);
    if (ret != HZCYKJTHardWare_RET_OK) {
        LOG_ERROR("接口", "初始化DLL失败：硬件控制程序回调接收服务启动失败，listen=%s:%d", listenHost.c_str(), callbackPort);
        Logger::Instance().Shutdown();
        return HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED;
    }
    {
        auto lock = WriteLock();
        ctx.callback_server_running = true;
    }

    EventDispatcher::Instance().Start();

    LOG_DEBUG("SDK生命周期", "初始化DLL：正在检查硬件控制程序通信服务，服务地址=%s，自动启动=%s",
             SanitizeUrlForLog(delphiServerUrl).c_str(), cfg.GetDelphiAutoStart() ? "true" : "false");
    {
        auto lock = WriteLock();
        delete ctx.http_client;
        ctx.http_client = new HttpClient();
    }
    DelphiProxy proxy(delphiServerUrl);
    if (!EnsureDelphiServiceAvailable(proxy, cfg, ctx.dll_dir, delphiServerUrl)) {
        LOG_DEBUG("SDK生命周期", "初始化DLL失败：硬件控制程序通信服务不可用，服务地址=%s",
                  SanitizeUrlForLog(delphiServerUrl).c_str());
        EventDispatcher::Instance().Stop();
        CallbackServer::Instance().Stop();
        {
            auto lock = WriteLock();
            ctx.callback_server_running = false;
            ctx.delphi_server_url.clear();
            ctx.current_terminal_base_url.clear();
            delete ctx.http_client;
            ctx.http_client = nullptr;
        }
        Logger::Instance().Shutdown();
        return ProxyFailureCode(proxy);
    }

    {
        auto lock = WriteLock();
        ctx.initialized = true;
    }

    std::string proxyInstanceId;
    if (!proxy.GetInstanceId(proxyInstanceId, 1000)) {
        LOG_WARN("预览租约", "C# Proxy未返回实例标识，当前版本仍可使用，但Proxy重启自动恢复暂不可用");
    }
    ExternalPreviewLeaseMonitor::Instance().Start(delphiServerUrl,
        proxyInstanceId, cfg.GetCheckHwndIntervalMs());

    LOG_INFO("SDK生命周期", "初始化DLL成功");

    return HZCYKJTHardWare_RET_OK;
}

static int ReleaseSdkBody(bool& canResumeRunning) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "释放SDK");

    auto& ctx = HzsjkjtContext::Instance();
    canResumeRunning = true;

    {
        auto lock = ReadLock();
        if (!ctx.initialized) {
            return HZCYKJTHardWare_RET_OK;
        }
    }

    ctx.switch_pending.store(true);

    // 拆除其余运行时资源前，拒绝从阻塞或重入的第三方回调中释放。
    // 回调返回后调用方可重试。进入 Stop 前将操作标记为不可逆，
    // 避免异常情况下错误恢复已部分停止的运行时。
    canResumeRunning = false;
    if (!EventDispatcher::Instance().Stop(1000)) {
        canResumeRunning = true;
        ctx.switch_pending.store(false);
        LOG_ERROR("接口", "释放SDK失败：第三方事件回调线程未能在1000ms内退出");
        return HZCYKJTHardWare_RET_FAILED;
    }
    // 该 Worker 会通过 DelphiProxy 使用 ctx.http_client，必须在删除
    // 共享 HTTP 客户端前停止并等待在途恢复请求。
    IrisPreviewRestoreWorker::Instance().Stop();
    ExternalPreviewLeaseMonitor::Instance().Stop();
    StopProxyExternalPreviewsOnRelease();
    EventDispatcher::Instance().SetCallback(nullptr);

    PreviewManager::Instance().StopAllRenderers();

    RequestSessionManager::Instance().CancelAll();

    const bool callbackStopped = CallbackServer::Instance().Stop(5000);
    if (!callbackStopped) {
        LOG_ERROR("接口", "释放SDK失败：回调接收线程未能在5000ms内退出，SDK进入故障状态，需重启宿主进程");
    }

    {
        auto lock = WriteLock();
        ctx.callback_server_running = false;
        delete ctx.http_client;
        ctx.http_client = nullptr;
        ctx.Reset();
    }

    Logger::Instance().Shutdown();

    return callbackStopped
        ? HZCYKJTHardWare_RET_OK
        : HZCYKJTHardWare_RET_FAILED;
}

static int SetTerminalBaseUrlBody(const char* baseUrl) {
    using namespace HZCYKJTHardWare;
    if (!baseUrl || !baseUrl[0]) return HZCYKJTHardWare_RET_INVALID_PARAM;
    if (!HzsjkjtContext::Instance().initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    LOG_WARN("接口", "代理模式不支持DLL直连终端URL：地址=%s，实际终端由硬件控制程序管理",
             SanitizeUrlForLog(baseUrl ? baseUrl : "").c_str());
    return HZCYKJTHardWare_RET_UNSUPPORTED;
}

static int SetCallbackServerBody(const char* host, int port) {
    using namespace HZCYKJTHardWare;
    auto& ctx = HzsjkjtContext::Instance();
    if (port <= 0 || port > 65535) return HZCYKJTHardWare_RET_INVALID_PARAM;

    bool wasRunning = false;
    std::string selectedLanIp;
    {
        auto lock = ReadLock();
        if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
        wasRunning = ctx.callback_server_running;
        selectedLanIp = ctx.selected_lan_ip;
    }

    if (wasRunning) {
        CallbackServer::Instance().Stop();
        auto lock = WriteLock();
        ctx.callback_server_running = false;
    }

    std::string callbackHost = host ? host : "";
    if (callbackHost.empty()) {
        callbackHost = selectedLanIp;
        if (callbackHost.empty()) callbackHost = "127.0.0.1";
    }
    const std::string listenHost = callbackHost;

    int ret = CallbackServer::Instance().Start(listenHost, port);
    if (ret != HZCYKJTHardWare_RET_OK) return ret;

    {
        auto lock = WriteLock();
        ctx.callback_server_host = listenHost;
        ctx.callback_server_port = port;
        ctx.callback_url_host = callbackHost;
        ctx.callback_server_running = true;
    }
    return HZCYKJTHardWare_RET_OK;
}

static int SetSavePathBody(const char* savePath) {
    using namespace HZCYKJTHardWare;
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    auto lock = WriteLock();
    ctx.runtime_save_path = savePath ? savePath : "";
    return HZCYKJTHardWare_RET_OK;
}

static int SwitchTerminalBody(int terminalIndex) {
    using namespace HZCYKJTHardWare;
    const std::string requestId = CurrentExportRequestId();
    LOG_DEBUG("接口", "切换终端：terminal_index=%d，request_id=%s",
              terminalIndex, requestId.c_str());
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (terminalIndex <= 0) return HZCYKJTHardWare_RET_TERMINAL_INDEX_INVALID;

    // 目标与当前终端相同，跳过切换
    {
        auto lock = ReadLock();
        if (ctx.current_terminal_index == terminalIndex) {
            LOG_DEBUG("接口", "终端切换请求跳过：当前已在终端%d", terminalIndex);
            return HZCYKJTHardWare_RET_OK;
        }
    }

    SwitchPendingScope switchScope(ctx.switch_pending);
    if (!switchScope.acquired) {
        LOG_WARN("接口", "终端切换请求被拒绝：已有切换正在执行，terminal_index=%d", terminalIndex);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }

    RequestSessionManager::Instance().ExpireAllForTerminalSwitch();

    bool irisRunning = false;
    std::string irisRequestId;
    HWND irisHwnd = nullptr;
    std::string delphiServerUrl;
    {
        auto lock = ReadLock();
        delphiServerUrl = ctx.delphi_server_url;
        irisRunning = ctx.iris_preview_running;
        irisRequestId = ctx.iris_preview_request_id;
        irisHwnd = reinterpret_cast<HWND>(ctx.iris_preview_third_party_hwnd);
    }

    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.SwitchTerminal(terminalIndex, requestId)) {
        LOG_ERROR("接口", "终端切换失败：DLL转发硬件控制程序失败，terminal_index=%d，request_id=%s",
                  terminalIndex, requestId.c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    {
        auto lock = WriteLock();
        ctx.current_terminal_index = terminalIndex;
    }

    if (irisRunning) {
        PreviewManager::Instance().StopIrisPreviewRenderer(false);
        IrisPreviewRestoreWorker::Instance().Enqueue(delphiServerUrl, irisRequestId, irisHwnd);
        LOG_DEBUG("接口", "终端切换已受理，虹膜预览恢复转入后台队列");
    }

    LOG_INFO("接口", "终端切换成功：Operation=SwitchTerminal RequestId=%s TerminalIndex=%d Result=Success",
             requestId.c_str(), terminalIndex);
    return HZCYKJTHardWare_RET_OK;
}

static int SwitchTerminalByUrlBody(const char* terminalBaseUrl) {
    using namespace HZCYKJTHardWare;
    if (!HzsjkjtContext::Instance().initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!terminalBaseUrl || !terminalBaseUrl[0]) return HZCYKJTHardWare_RET_INVALID_PARAM;
    LOG_WARN("接口", "代理模式不支持DLL按URL切换终端：terminal_地址=%s，实际终端由硬件控制程序管理",
             SanitizeUrlForLog(terminalBaseUrl ? terminalBaseUrl : "").c_str());
    return HZCYKJTHardWare_RET_UNSUPPORTED;
}

static int GetDetectedNetworkInfoBody(char* buffer, int bufferSize) {
    using namespace HZCYKJTHardWare;
    if (!buffer || bufferSize <= 0) return HZCYKJTHardWare_RET_INVALID_PARAM;

    auto& ctx = HzsjkjtContext::Instance();
    int callbackPort = CallbackServer::Instance().GetPort();

    std::string json = "{\n";
    json += "  \"selected_ip\": \"" + ctx.selected_lan_ip + "\",\n";
    json += "  \"selected_subnet_prefix\": \"" + ctx.selected_subnet_prefix + "\",\n";
    json += "  \"delphi_server_url\": \"" + ctx.delphi_server_url + "\",\n";
    json += "  \"callback_host\": \"" + ctx.callback_url_host + "\",\n";
    json += "  \"callback_port\": " + std::to_string(callbackPort) + "\n";
    json += "}";

    if ((int)json.size() >= bufferSize) return HZCYKJTHardWare_RET_BUFFER_TOO_SMALL;
    memcpy(buffer, json.c_str(), json.size() + 1);
    return HZCYKJTHardWare_RET_OK;
}

static int CheckTerminalStatusBody() {
    using namespace HZCYKJTHardWare;
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    DelphiProxy proxy(ctx.delphi_server_url);
    if (proxy.Ping()) {
        LOG_DEBUG("SDK生命周期", "硬件控制程序连通性检查成功");
        return HZCYKJTHardWare_RET_OK;
    }
    LOG_WARN_RATE_LIMITED("SDKLifecycle|proxy_ping", "SDK生命周期",
                          "硬件控制程序通信服务异常：检查/ping失败");
    return HZCYKJTHardWare_RET_TERMINAL_UNREACHABLE;
}

static int StartProcessBody(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "开始流程");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string inputEncoding = GetThirdPartyInputEncoding();
    std::string normalizedSaveDir;
    if (!NormalizeThirdPartyInput(
            saveDir, "saveDir", inputEncoding, normalizedSaveDir)) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    std::string saveRoot = ResolveSaveRoot(normalizedSaveDir.c_str());
    std::string requestId = GetOrCreateExportRequestId("HZCYKJTHardWare_PROCESS");

    // 为异步操作构建回调 JSON
    std::string ocrCallback = BuildCallbackUrl(ctx, "/ocr");
    std::string nfcCallback = BuildCallbackUrl(ctx, "/nfc-card");
    std::string irisCallback = BuildCallbackUrl(ctx, "/iris");
    std::string callbacksJson = "{" +
        std::string("\"callbacks\":{") +
        "\"ocr\":\"" + ocrCallback + "\"," +
        "\"nfc\":\"" + nfcCallback + "\"," +
        "\"iris\":\"" + irisCallback + "\"" +
        "}}";

    if (IsSwitchPending()) {
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }

    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.ProcessStart(requestId, saveRoot, callbacksJson)) {
        const int failureCode = ProxyFailureCode(proxy);
        LOG_ERROR("接口", "业务流程启动失败：Operation=StartProcess RequestId=%s Result=Failed ErrorCode=%d，服务地址=%s",
                  requestId.c_str(), failureCode,
                  SanitizeUrlForLog(ctx.delphi_server_url).c_str());
        return failureCode;
    }

    LOG_INFO("接口", "流程已受理：Operation=StartProcess RequestId=%s Result=Accepted",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int EndProcessBody() {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "结束流程");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string requestId =
        GetOrCreateExportRequestId("HZCYKJTHardWare_FLOW_END");
    DelphiProxy proxy(ctx.delphi_server_url);
    bool ok = proxy.ProcessEnd(requestId);

    if (!ok) {
        const int failureCode = ProxyFailureCode(proxy);
        LOG_ERROR("接口", "业务流程结束失败：Operation=EndProcess RequestId=%s Result=Failed ErrorCode=%d，服务地址=%s",
                  requestId.c_str(), failureCode,
                  SanitizeUrlForLog(ctx.delphi_server_url).c_str());
        return failureCode;
    }

    LOG_INFO("接口", "业务流程已结束：Operation=EndProcess RequestId=%s Result=Success",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

// ---- 预览 ----

static int StartCameraPreviewBody(void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "开始摄像头预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!hwnd || !IsWindow(reinterpret_cast<HWND>(hwnd))) {
        LOG_ERROR("接口", "启动摄像头预览失败：第三方HWND无效，HWND=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    std::string requestId = GetOrCreateExportRequestId("HZCYKJTHardWare_PREVIEW");
    LOG_DEBUG("接口", "摄像头预览请求已创建：request_id=%s，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    std::string delphiServerUrl;
    std::string callbackUrl;
    intptr_t thirdPartyHwnd = reinterpret_cast<intptr_t>(hwnd);
    {
        auto lock = WriteLock();
        if (ctx.camera_preview_running) {
            LOG_WARN("接口", "启动摄像头预览失败：预览已运行，request_id=%s",
                     ctx.camera_preview_request_id.c_str());
            return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
        }
        delphiServerUrl = ctx.delphi_server_url;
        callbackUrl = BuildCallbackUrl(ctx, "/preview-ready");
        ctx.camera_preview_running = true;
        ctx.camera_preview_request_id = requestId;
        ctx.camera_preview_third_party_hwnd = thirdPartyHwnd;
    }

    if (IsSwitchPending()) {
        LOG_WARN("接口", "摄像头预览启动被终端切换拦截：request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id == requestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        LOG_WARN("接口", "摄像头预览启动被终端切换拦截（锁后）：request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id == requestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.StartCameraPreview(requestId, thirdPartyHwnd, callbackUrl)) {
        LOG_ERROR("接口", "启动摄像头预览失败：向硬件控制程序下发外部渲染请求失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(delphiServerUrl).c_str());
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id == requestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
        }
        return ProxyFailureCode(proxy);
    }

    ExternalPreviewLeaseMonitor::Instance().NotifyStateChanged();
    LOG_INFO("接口", "摄像头预览请求已受理：Operation=StartCameraPreview RequestId=%s Result=Accepted，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    return HZCYKJTHardWare_RET_OK;
}

static int StopCameraPreviewBody() {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "停止摄像头预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId;
    std::string delphiServerUrl;
    {
        auto lock = ReadLock();
        if (!ctx.camera_preview_running) {
            return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
        }
        requestId = ctx.camera_preview_request_id;
        delphiServerUrl = ctx.delphi_server_url;
    }
    SetExportRequestId(requestId);

    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.StopCameraPreview(requestId)) {
        LOG_ERROR("接口", "停止摄像头预览失败：向硬件控制程序下发停止请求失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(delphiServerUrl).c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    {
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id == requestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
        }
    }

    ExternalPreviewLeaseMonitor::Instance().NotifyStateChanged();
    LOG_INFO("接口", "摄像头预览已停止：Operation=StopCameraPreview RequestId=%s Result=Success",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int StartFingerprintPreviewBody(void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "开始指纹预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!hwnd || !IsWindow(reinterpret_cast<HWND>(hwnd))) {
        LOG_ERROR("接口", "启动指纹预览失败：第三方HWND无效，HWND=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    std::string requestId = GetOrCreateExportRequestId("HZCYKJTHardWare_FP_PREVIEW");
    LOG_DEBUG("接口", "指纹预览请求已创建：request_id=%s，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    std::string delphiServerUrl;
    std::string callbackUrl;
    intptr_t thirdPartyHwnd = reinterpret_cast<intptr_t>(hwnd);
    {
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_running) {
            LOG_WARN("接口", "启动指纹预览失败：预览已运行，request_id=%s",
                     ctx.fingerprint_preview_request_id.c_str());
            return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
        }
        delphiServerUrl = ctx.delphi_server_url;
        callbackUrl = BuildCallbackUrl(ctx, "/preview-ready");
        ctx.fingerprint_preview_running = true;
        ctx.fingerprint_preview_request_id = requestId;
        ctx.fingerprint_preview_third_party_hwnd = thirdPartyHwnd;
    }

    if (IsSwitchPending()) {
        LOG_WARN("接口", "指纹预览启动被终端切换拦截：request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_request_id == requestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        LOG_WARN("接口", "指纹预览启动被终端切换拦截（锁后）：request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_request_id == requestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.StartFingerprintPreview(requestId, thirdPartyHwnd, callbackUrl)) {
        LOG_ERROR("接口", "启动指纹预览失败：向硬件控制程序下发外部渲染请求失败，request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_request_id == requestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    ExternalPreviewLeaseMonitor::Instance().NotifyStateChanged();
    LOG_INFO("接口", "指纹预览请求已受理：Operation=StartFingerprintPreview RequestId=%s Result=Accepted，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    return HZCYKJTHardWare_RET_OK;
}

static int StopFingerprintPreviewBody() {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "停止指纹预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId;
    std::string delphiServerUrl;
    {
        auto lock = ReadLock();
        if (!ctx.fingerprint_preview_running) {
            return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
        }
        requestId = ctx.fingerprint_preview_request_id;
        delphiServerUrl = ctx.delphi_server_url;
    }
    SetExportRequestId(requestId);

    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.StopFingerprintPreview(requestId)) {
        LOG_ERROR("接口", "停止指纹预览失败：向硬件控制程序下发停止请求失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(delphiServerUrl).c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    {
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_request_id == requestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
        }
    }

    ExternalPreviewLeaseMonitor::Instance().NotifyStateChanged();
    LOG_INFO("接口", "指纹预览已停止：Operation=StopFingerprintPreview RequestId=%s Result=Success",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int StartIrisPreviewBody(void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "开始虹膜预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!hwnd || !IsWindow(reinterpret_cast<HWND>(hwnd))) {
        LOG_ERROR("接口", "启动虹膜预览失败：第三方HWND无效，HWND=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    std::string requestId = GetOrCreateExportRequestId("HZCYKJTHardWare_IRIS_PREVIEW");
    LOG_DEBUG("接口", "虹膜预览请求已创建：request_id=%s，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    std::string rtspUrl;
    std::string delphiServerUrl;
    intptr_t thirdPartyHwnd = reinterpret_cast<intptr_t>(hwnd);
    {
        auto lock = WriteLock();
        if (ctx.iris_preview_running) {
            LOG_WARN("接口", "启动虹膜预览失败：预览已运行，request_id=%s",
                     ctx.iris_preview_request_id.c_str());
            return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
        }
        delphiServerUrl = ctx.delphi_server_url;
        ctx.iris_preview_running = true;
        ctx.iris_preview_request_id = requestId;
        ctx.iris_preview_third_party_hwnd = thirdPartyHwnd;
    }

    if (IsSwitchPending()) {
        LOG_WARN("接口", "虹膜预览启动被终端切换拦截：request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.iris_preview_request_id == requestId) {
            ctx.iris_preview_running = false;
            ctx.iris_preview_request_id.clear();
            ctx.iris_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        LOG_WARN("接口", "虹膜预览启动被终端切换拦截（锁后）：request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.iris_preview_request_id == requestId) {
            ctx.iris_preview_running = false;
            ctx.iris_preview_request_id.clear();
            ctx.iris_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.GetIrisPreviewUrl(requestId, rtspUrl)) {
        LOG_ERROR("接口", "启动虹膜预览失败：向硬件控制程序获取预览地址失败，request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.iris_preview_request_id == requestId) {
            ctx.iris_preview_running = false;
            ctx.iris_preview_request_id.clear();
            ctx.iris_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    int ret = PreviewManager::Instance().StartIrisPreviewFromUrl(reinterpret_cast<HWND>(hwnd), rtspUrl);
    if (ret != HZCYKJTHardWare_RET_OK) {
        LOG_ERROR("接口", "启动虹膜预览失败：本地渲染器启动失败，request_id=%s，third_party_hwnd=%p，返回值=%d",
                  requestId.c_str(), hwnd, ret);
        auto lock = WriteLock();
        if (ctx.iris_preview_request_id == requestId) {
            ctx.iris_preview_running = false;
            ctx.iris_preview_request_id.clear();
            ctx.iris_preview_third_party_hwnd = 0;
        }
        return ret;
    }

    PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE,
                     HZCYKJTHardWare_EVENT_IRIS_PREVIEW_STARTED,
                     HZCYKJTHardWare_RET_OK, "", "预览已就绪");
    LOG_INFO("接口", "虹膜预览已启动：Operation=StartIrisPreview RequestId=%s Result=Success，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    return HZCYKJTHardWare_RET_OK;
}

static int StopIrisPreviewBody() {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "停止虹膜预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId;
    {
        auto lock = ReadLock();
        if (!ctx.iris_preview_running) {
            return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
        }
        requestId = ctx.iris_preview_request_id;
    }
    SetExportRequestId(requestId);

    PreviewManager::Instance().StopIrisPreviewRenderer();

    {
        auto lock = WriteLock();
        if (ctx.iris_preview_request_id == requestId) {
            ctx.iris_preview_running = false;
            ctx.iris_preview_request_id.clear();
            ctx.iris_preview_third_party_hwnd = 0;
        }
    }

    LOG_INFO("接口", "虹膜预览已停止：Operation=StopIrisPreview RequestId=%s Result=Success",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int StartPlatePreviewBody(PlatePreviewChannel channel, void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "开始车牌预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!hwnd || !IsWindow(reinterpret_cast<HWND>(hwnd))) {
        LOG_ERROR("接口", "启动车牌预览失败：第三方HWND无效，HWND=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    const char* plateCode = PlatePreviewCode(channel);
    const char* plateName = PlatePreviewDisplayName(channel);
    const std::string requestPrefix =
        std::string("HZCYKJTHardWare_PLATE_PREVIEW_") + plateName;
    std::string requestId = GetOrCreateExportRequestId(requestPrefix.c_str());
    LOG_DEBUG("接口", "车牌%s预览请求已创建：request_id=%s，third_party_hwnd=%p",
             plateName, requestId.c_str(), hwnd);
    std::string proxyUrl;
    std::string callbackUrl;
    int streamChannel = 101;
    intptr_t thirdPartyHwnd = reinterpret_cast<intptr_t>(hwnd);
    {
        auto lock = WriteLock();
        PlatePreviewState& plateState = GetPlatePreviewState(ctx, channel);
        if (plateState.running) {
            LOG_WARN("接口", "启动车牌预览失败：预览已运行，request_id=%s",
                     plateState.request_id.c_str());
            return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
        }
        if (!plateState.enabled) {
            LOG_WARN("接口", "启动车牌%s预览失败：配置preview.plate.%s.enabled=false，request_id=%s",
                     plateName, plateCode, requestId.c_str());
            return HZCYKJTHardWare_RET_UNSUPPORTED;
        }
        if (plateState.rtsp_url.empty()) {
            LOG_ERROR("接口", "启动车牌%s预览失败：车牌相机RTSP配置不完整，request_id=%s",
                      plateName, requestId.c_str());
            return HZCYKJTHardWare_RET_RTSP_URL_EMPTY;
        }

        proxyUrl = ctx.delphi_server_url;
        callbackUrl = BuildCallbackUrl(ctx, "/preview-ready");
        streamChannel = plateState.stream_channel;
        plateState.running = true;
        plateState.request_id = requestId;
        plateState.third_party_hwnd = thirdPartyHwnd;
    }

    DelphiProxy proxy(proxyUrl);
    if (!proxy.StartPlatePreview(plateCode, requestId, thirdPartyHwnd, callbackUrl)) {
        LOG_ERROR("接口", "启动车牌预览失败：向C# Proxy下发外部渲染请求失败，request_id=%s，stream_channel=%d",
                  requestId.c_str(), streamChannel);
        auto lock = WriteLock();
        PlatePreviewState& plateState = GetPlatePreviewState(ctx, channel);
        if (plateState.request_id == requestId) {
            plateState.running = false;
            plateState.request_id.clear();
            plateState.third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    ExternalPreviewLeaseMonitor::Instance().NotifyStateChanged();
    LOG_INFO("接口", "车牌%s预览已受理：Operation=StartPlatePreview%s RequestId=%s Result=Accepted，码流通道=%d，third_party_hwnd=%p",
             plateName, plateName, requestId.c_str(), streamChannel, hwnd);
    return HZCYKJTHardWare_RET_OK;
}

static int StopPlatePreviewBody(PlatePreviewChannel channel) {
    using namespace HZCYKJTHardWare;
    LOG_DEBUG("接口", "停止车牌预览");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId;
    std::string proxyUrl;
    const char* plateCode = PlatePreviewCode(channel);
    const char* plateName = PlatePreviewDisplayName(channel);
    {
        auto lock = ReadLock();
        const PlatePreviewState& plateState = GetPlatePreviewState(ctx, channel);
        if (!plateState.running) {
            return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
        }
        requestId = plateState.request_id;
        proxyUrl = ctx.delphi_server_url;
    }
    SetExportRequestId(requestId);

    DelphiProxy proxy(proxyUrl);
    if (!proxy.StopPlatePreview(plateCode, requestId)) {
        LOG_ERROR("接口", "停止车牌预览失败：向C# Proxy下发停止请求失败，request_id=%s",
                  requestId.c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    {
        auto lock = WriteLock();
        PlatePreviewState& plateState = GetPlatePreviewState(ctx, channel);
        if (plateState.request_id == requestId) {
            plateState.running = false;
            plateState.request_id.clear();
            plateState.third_party_hwnd = 0;
        }
    }

    ExternalPreviewLeaseMonitor::Instance().NotifyStateChanged();
    PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_PLATE_IMAGE,
        HZCYKJTHardWare_EVENT_PLATE_PREVIEW_STOPPED,
        HZCYKJTHardWare_RET_OK, "", "plate preview stopped");
    LOG_INFO("接口", "车牌%s预览已停止：Operation=StopPlatePreview%s RequestId=%s Result=Success",
             plateName, plateName, requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int SaveLatestPlateFrameBody(const char* savePath, int cameraType) {
    using namespace HZCYKJTHardWare;

    const ULONGLONG startedAt = GetTickCount64();
    const std::string captureRequestId = GenerateSyncRequestId(
        "HZCYKJTHardWare_PLATE_CAPTURE");
    std::string normalizedSavePath;
    std::string requestId;
    const char* plateCode = "unknown";
    const char* plateName = "unknown";
    LatestPlateFrameMetadata frameMetadata;
    auto finish = [&](int resultCode, size_t bytes) -> int {
        LogLatestPlateFrameOperation(resultCode, captureRequestId, requestId,
            plateName, normalizedSavePath, bytes, frameMetadata,
            GetTickCount64() - startedAt);
        return resultCode;
    };

    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized)
        return finish(HZCYKJTHardWare_RET_NOT_INITIALIZED, 0);
    if (!savePath || !savePath[0]) {
        LOG_ERROR("接口", "保存最新车牌帧失败：保存路径为空");
        return finish(HZCYKJTHardWare_RET_INVALID_PARAM, 0);
    }

    PlatePreviewChannel channel;
    if (!TryGetPlatePreviewChannel(cameraType, channel)) {
        LOG_ERROR("接口", "保存最新车牌帧失败：镜头类型无效，camera_type=%d",
                  cameraType);
        return finish(HZCYKJTHardWare_FRAME_INVALID_CAMERA, 0);
    }

    plateCode = PlatePreviewCode(channel);
    plateName = PlatePreviewDisplayName(channel);

    DeviceCapability capability = DeviceCapability::PlateCJ;
    switch (channel) {
        case PlatePreviewChannel::RJ2:
            capability = DeviceCapability::PlateRJ2;
            break;
        case PlatePreviewChannel::RJ3:
            capability = DeviceCapability::PlateRJ3;
            break;
        case PlatePreviewChannel::CJ:
        default:
            capability = DeviceCapability::PlateCJ;
            break;
    }
    const int capabilityResult = RequireCapability(__FUNCTION__, capability);
    if (capabilityResult != HZCYKJTHardWare_RET_OK)
        return finish(capabilityResult, 0);

    if (IsSwitchPending()) {
        LOG_WARN("接口", "保存最新车牌帧被终端切换拦截：camera_type=%d", cameraType);
        return finish(HZCYKJTHardWare_RET_DEVICE_BUSY, 0);
    }

    if (!PathHelper::NormalizeExternalTextToUtf8(
            savePath, GetThirdPartyInputEncoding(), normalizedSavePath) ||
        normalizedSavePath.empty()) {
        LOG_ERROR("接口", "保存最新车牌帧失败：保存路径编码无效");
        return finish(HZCYKJTHardWare_RET_INVALID_PARAM, 0);
    }

    std::string proxyUrl;
    {
        auto lock = ReadLock();
        const PlatePreviewState& plateState = GetPlatePreviewState(ctx, channel);
        if (!plateState.running || plateState.request_id.empty()) {
            LOG_WARN("接口", "保存最新车牌帧失败：车牌%s预览未运行",
                     plateName);
            return finish(HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING, 0);
        }
        requestId = plateState.request_id;
        proxyUrl = ctx.delphi_server_url;
    }
    SetExportRequestId(requestId);

    if (proxyUrl.empty()) {
        LOG_ERROR("接口", "保存最新车牌帧失败：C# Proxy地址为空，车牌%s，request_id=%s",
                  plateName, requestId.c_str());
        return finish(HZCYKJTHardWare_RET_HTTP_FAILED, 0);
    }

    DelphiProxy proxy(proxyUrl);
    std::vector<unsigned char> jpegData;
    if (!proxy.GetLatestPlateFrame(plateCode, requestId, jpegData, 5000,
                                   captureRequestId, &frameMetadata)) {
        const int proxyCode = proxy.LastResultCode();
        LOG_ERROR("接口", "保存最新车牌帧失败：车牌%s，request_id=%s，返回码=%d",
                  plateName, requestId.c_str(), proxyCode);
        const int resultCode = proxyCode == HZCYKJTHardWare_RET_OK
            ? HZCYKJTHardWare_RET_HTTP_FAILED : proxyCode;
        return finish(resultCode, jpegData.size());
    }

    const int saveResult = ImageSaver::SaveJpegFileAtomic(
        normalizedSavePath, jpegData);
    if (saveResult != HZCYKJTHardWare_RET_OK) {
        LOG_ERROR("接口", "保存最新车牌帧失败：车牌%s，path=%s，request_id=%s，返回码=%d",
                  plateName, normalizedSavePath.c_str(), requestId.c_str(), saveResult);
        return finish(saveResult, jpegData.size());
    }

    return finish(HZCYKJTHardWare_RET_OK, jpegData.size());
}

static int SaveLatestPlateFrameSafeBody(const char* savePath, int cameraType) {
    try {
        return SaveLatestPlateFrameBody(savePath, cameraType);
    } catch (...) {
        return HZCYKJTHardWare_RET_FAILED;
    }
}

// ---- 采集 ----

static int CaptureCameraImageDirect(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "人脸抓拍");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string inputEncoding = GetThirdPartyInputEncoding();
    std::string normalizedSaveDir;
    if (!NormalizeThirdPartyInput(
            saveDir, "saveDir", inputEncoding, normalizedSaveDir)) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    std::string requestId = GetOrCreateExportRequestId("HZCYKJTHardWare_FACE");
    std::string saveRoot = ResolveCaptureTargetPath(normalizedSaveDir.c_str(), true);
    std::string savePath;

    if (IsSwitchPending()) {
        LOG_WARN("接口", "人脸抓拍被终端切换拦截：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.CaptureFace(requestId, saveRoot, savePath,
                           ctx.face_capture_timeout_ms)) {
        LOG_ERROR("接口", "人脸抓拍失败：DLL转发硬件控制程序失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(ctx.delphi_server_url).c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    // 终端切换抢断：如果HTTP执行期间发生了切换，丢弃结果
    if (IsSwitchPending()) {
        LOG_WARN("接口", "人脸抓拍结果因终端切换丢弃：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }

    LOG_INFO("接口", "人脸抓拍成功：Operation=CaptureFace RequestId=%s Result=Success",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int CaptureCameraImageBody(const char* saveDir) {
    if (IsSwitchPending()) {
        LOG_WARN("接口", "人脸抓拍被终端切换拦截");
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    return CaptureCameraImageDirect(saveDir);
}

static int CaptureFingerprintImageDirect(const char* saveDir, const char* saveDirHk) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "指纹抓拍");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string inputEncoding = GetThirdPartyInputEncoding();
    std::string normalizedSaveDir;
    std::string normalizedSaveDirHk;
    if (!NormalizeThirdPartyInput(
            saveDir, "saveDir", inputEncoding, normalizedSaveDir) ||
        !NormalizeThirdPartyInput(
            saveDirHk, "saveDirHk", inputEncoding, normalizedSaveDirHk)) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    std::string requestId = GetOrCreateExportRequestId("HZCYKJTHardWare_FP");
    std::string saveRoot = ResolveCaptureTargetPath(normalizedSaveDir.c_str(), false);
    std::string savePath;

    if (IsSwitchPending()) {
        LOG_WARN("接口", "指纹抓拍被终端切换拦截：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(ctx.delphi_server_url);
    bool ok;
    if (!normalizedSaveDirHk.empty()) {
        std::string saveDirHkRoot = normalizedSaveDirHk;
        ok = proxy.CaptureFingerprint(requestId, saveRoot, saveDirHkRoot, savePath,
                                      ctx.fingerprint_capture_timeout_ms);
    } else {
        ok = proxy.CaptureFingerprint(requestId, saveRoot, "", savePath,
                                      ctx.fingerprint_capture_timeout_ms);
    }
    if (!ok) {
        LOG_ERROR("接口", "指纹抓拍失败：DLL转发硬件控制程序失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(ctx.delphi_server_url).c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    if (IsSwitchPending()) {
        LOG_WARN("接口", "指纹抓拍结果因终端切换丢弃：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }

    LOG_INFO("接口", "指纹抓拍成功：Operation=CaptureFingerprint RequestId=%s Result=Success",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int CaptureIrisImageDirect(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "虹膜抓拍");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string inputEncoding = GetThirdPartyInputEncoding();
    std::string normalizedSaveDir;
    if (!NormalizeThirdPartyInput(
            saveDir, "saveDir", inputEncoding, normalizedSaveDir)) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    int timeoutMs = ctx.face_capture_timeout_ms;
    std::string saveRoot = ResolveSaveRoot(normalizedSaveDir.c_str());
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, saveRoot, timeoutMs,
        CurrentExportRequestId());

    std::string callbackUrl = BuildCallbackUrl(ctx, "/iris");

    if (IsSwitchPending()) {
        LOG_WARN("接口", "虹膜抓拍被终端切换拦截：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        LOG_WARN("接口", "虹膜抓拍被终端切换拦截（锁后）：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.CaptureIrisAsync(requestId, saveRoot, callbackUrl)) {
        const int failureCode = ProxyFailureCode(proxy);
        LOG_ERROR("接口", "虹膜抓拍提交失败：DLL转发硬件控制程序失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(ctx.delphi_server_url).c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                         failureCode,
                         failureCode == HZCYKJTHardWare_RET_UNSUPPORTED ? "not_supported" : "",
                         "虹膜抓拍请求发送失败",
                         nullptr, nullptr);
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return failureCode;
    }

    if (IsSwitchPending()) {
        LOG_WARN("接口", "虹膜抓拍受理结果因终端切换丢弃：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (!RequestSessionManager::Instance().MarkAccepted(requestId)) {
        LOG_WARN("接口", "虹膜抓拍受理结果已过期：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    LOG_INFO("接口", "虹膜抓拍已受理：Operation=CaptureIris RequestId=%s Result=Accepted",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int CaptureIrisImageBody(const char* saveDir) {
    if (IsSwitchPending()) {
        LOG_WARN("接口", "虹膜抓拍被终端切换拦截");
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    return CaptureIrisImageDirect(saveDir);
}

static int RequestOCRDirect(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "OCR识别");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string inputEncoding = GetThirdPartyInputEncoding();
    std::string normalizedSaveDir;
    if (!NormalizeThirdPartyInput(
            saveDir, "saveDir", inputEncoding, normalizedSaveDir)) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    std::string saveRoot = ResolveSaveRoot(normalizedSaveDir.c_str());
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, saveRoot, ctx.ocr_timeout_ms,
        CurrentExportRequestId());

    std::string callbackUrl = BuildCallbackUrl(ctx, "/ocr");

    if (IsSwitchPending()) {
        LOG_WARN("接口", "OCR请求被终端切换拦截：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        LOG_WARN("接口", "OCR请求被终端切换拦截（锁后）：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.RequestOcrAsync(requestId, saveRoot, callbackUrl)) {
        const int failureCode = ProxyFailureCode(proxy);
        LOG_ERROR("接口", "OCR请求提交失败：DLL转发硬件控制程序失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(ctx.delphi_server_url).c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, HZCYKJTHardWare_EVENT_OCR_FAILED,
                         failureCode,
                         failureCode == HZCYKJTHardWare_RET_UNSUPPORTED ? "not_supported" : "",
                         "OCR识别请求发送失败",
                         nullptr, nullptr);
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return failureCode;
    }

    if (IsSwitchPending()) {
        LOG_WARN("接口", "OCR请求受理结果因终端切换丢弃：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (!RequestSessionManager::Instance().MarkAccepted(requestId)) {
        LOG_WARN("接口", "OCR请求受理结果已过期：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    LOG_INFO("接口", "OCR请求已受理：Operation=RequestOCR RequestId=%s Result=Accepted",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int RequestOCRBody(const char* saveDir) {
    if (IsSwitchPending()) {
        LOG_WARN("接口", "OCR请求被终端切换拦截");
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    return RequestOCRDirect(saveDir);
}

static int RequestNfcCardDirect(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "IC卡识别");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string inputEncoding = GetThirdPartyInputEncoding();
    std::string normalizedSaveDir;
    if (!NormalizeThirdPartyInput(
            saveDir, "saveDir", inputEncoding, normalizedSaveDir)) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    int timeoutMs = ctx.ocr_timeout_ms;
    std::string saveRoot = ResolveSaveRoot(normalizedSaveDir.c_str());
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_NFC_CARD, saveRoot, timeoutMs,
        CurrentExportRequestId());

    std::string callbackUrl = BuildCallbackUrl(ctx, "/nfc-card");

    if (IsSwitchPending()) {
        LOG_WARN("接口", "NFC请求被终端切换拦截：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        LOG_WARN("接口", "NFC请求被终端切换拦截（锁后）：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.RequestNfcAsync(requestId, saveRoot, callbackUrl)) {
        const int failureCode = ProxyFailureCode(proxy);
        LOG_ERROR("NFC", "IC卡识别请求提交失败：DLL转发硬件控制程序失败，request_id=%s，服务地址=%s",
                  requestId.c_str(), SanitizeUrlForLog(ctx.delphi_server_url).c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                         failureCode,
                         failureCode == HZCYKJTHardWare_RET_UNSUPPORTED ? "not_supported" : "",
                         "IC卡识别请求发送失败",
                         nullptr, nullptr);
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return failureCode;
    }

    if (IsSwitchPending()) {
        LOG_WARN("NFC", "NFC请求受理结果因终端切换丢弃：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (!RequestSessionManager::Instance().MarkAccepted(requestId)) {
        LOG_WARN("NFC", "NFC请求受理结果已过期：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    LOG_INFO("NFC", "IC卡识别已受理：Operation=RequestNfcCard RequestId=%s Result=Accepted",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int RequestNfcCardBody(const char* saveDir) {
    if (IsSwitchPending()) {
        LOG_WARN("NFC", "NFC请求被终端切换拦截");
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    return RequestNfcCardDirect(saveDir);
}

static int RequestAuthorizeDirect(const char* ZJHM, const char* ZJLB,
                                  const char* GJDQDM, const char* XM,
                                  const char* XB, const char* CSRQ,
                                  const char* KADM) {
    using namespace HZCYKJTHardWare;
    //LOG_INFO("接口", "授权请求");

    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    const std::string inputEncoding = GetThirdPartyInputEncoding();
    std::string authZJHM;
    std::string authZJLB;
    std::string authGJDQDM;
    std::string authXM;
    std::string authXB;
    std::string authCSRQ;
    std::string authKADM;
    if (!NormalizeThirdPartyInput(ZJHM, "ZJHM", inputEncoding, authZJHM) ||
        !NormalizeThirdPartyInput(ZJLB, "ZJLB", inputEncoding, authZJLB) ||
        !NormalizeThirdPartyInput(GJDQDM, "GJDQDM", inputEncoding, authGJDQDM) ||
        !NormalizeThirdPartyInput(XM, "XM", inputEncoding, authXM) ||
        !NormalizeThirdPartyInput(XB, "XB", inputEncoding, authXB) ||
        !NormalizeThirdPartyInput(CSRQ, "CSRQ", inputEncoding, authCSRQ) ||
        !NormalizeThirdPartyInput(KADM, "KADM", inputEncoding, authKADM)) {
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    int timeoutMs = ctx.authorize_timeout_ms;
    int httpTimeoutMs = 5000;  // Proxy HTTP 请求仅等待快速受理，不覆盖完整签名时长
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_AUTHORIZATION, "", timeoutMs,
        CurrentExportRequestId());

    std::string callbackUrl = BuildCallbackUrl(ctx, "/authorize");
    LOG_INFO("授权", "收到授权请求：Operation=Authorize RequestId=%s，证件号码=%s，证件类别=%s，国家地区代码=%s，姓名=%s，性别=%s，出生日期=%s，口岸代码=%s",
             requestId.c_str(),
             LogValue(authZJHM).c_str(), LogValue(authZJLB).c_str(),
             LogValue(authGJDQDM).c_str(), LogValue(authXM).c_str(),
             LogValue(authXB).c_str(), LogValue(authCSRQ).c_str(),
             LogValue(authKADM).c_str());
    LOG_DEBUG("授权", "授权请求通信上下文：请求ID=%s，回调地址=%s",
              requestId.c_str(), LogValue(callbackUrl).c_str());

    if (IsSwitchPending()) {
        LOG_WARN("接口", "授权请求被终端切换拦截：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (IsSwitchPending()) {
        LOG_WARN("接口", "授权请求被终端切换拦截（锁后）：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.RequestAuthorize(requestId,
                                authZJHM,
                                authZJLB,
                                authGJDQDM,
                                authXM,
                                authXB,
                                authCSRQ,
                                authKADM,
                                callbackUrl,
                                httpTimeoutMs)) {
        const int failureCode = ProxyFailureCode(proxy);
        LOG_ERROR("授权", "EXE授权请求受理失败：请求ID=%s，错误码=%d",
                  requestId.c_str(), failureCode);
        LOG_DEBUG("授权", "授权请求通信失败：请求ID=%s，EXE地址=%s",
                  requestId.c_str(), LogValue(ctx.delphi_server_url).c_str());
        LOG_ERROR("接口", "授权请求提交失败：DLL转发硬件控制程序失败，request_id=%s", requestId.c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_AUTHORIZATION,
                         HZCYKJTHardWare_EVENT_AUTHORIZE_FAILED,
                         failureCode,
                         failureCode == HZCYKJTHardWare_RET_UNSUPPORTED ? "not_supported" : "",
                         "授权请求发送失败",
                         nullptr, nullptr);
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return failureCode;
    }

    if (IsSwitchPending()) {
        LOG_WARN("接口", "授权请求受理结果因终端切换丢弃：request_id=%s", requestId.c_str());
        RequestSessionManager::Instance().MarkCompleted(requestId);
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    if (!RequestSessionManager::Instance().MarkAccepted(requestId)) {
        LOG_WARN("接口", "授权请求受理结果已过期：request_id=%s", requestId.c_str());
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    LOG_INFO("授权", "授权请求已受理：Operation=Authorize RequestId=%s Result=Accepted",
             requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int RequestAuthorizeBody(const char* ZJHM, const char* ZJLB,
                                const char* GJDQDM, const char* XM,
                                const char* XB, const char* CSRQ,
                                const char* KADM) {
    if (IsSwitchPending()) {
        LOG_WARN("接口", "授权请求被终端切换拦截");
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    return RequestAuthorizeDirect(ZJHM, ZJLB, GJDQDM, XM, XB, CSRQ, KADM);
}

static int RegisterEventCallbackBody(THZCYKJTHardWareEventCallback callback) {
    using namespace HZCYKJTHardWare;
    if (!callback) return HZCYKJTHardWare_RET_INVALID_PARAM;
    EventDispatcher::Instance().SetCallback(callback);
    return HZCYKJTHardWare_RET_OK;
}

static void LogActiveCallDrainTimeout(int waitMs) {
    const std::string activeCallDetails =
        HZCYKJTHardWare::SdkRuntime::Instance().DescribeActiveCalls();
    LOG_ERROR("接口", "释放SDK等待在途调用超时：active=%d，wait_ms=%d",
              HZCYKJTHardWare::SdkRuntime::Instance().ActiveCalls(),
              waitMs);
    LOG_ERROR("SDK", "ReleaseSdk active call details: %s",
              activeCallDetails.c_str());
}

// ============================================================================
// 导出函数；SEH 包装范围不得跨越 C++ 对象生命周期
// ============================================================================

static void LogExportBoundary(const char* operation, const char* phase,
                              const char* result, int resultCode,
                              ULONGLONG durationMs,
                              const char* requestId) {
    try {
        using namespace HZCYKJTHardWare;
        LogContext context;
        context.operation = operation ? operation : "unknown";
        context.requestId = requestId ? requestId : "";
        context.result = result ? result : "未知";
        if (resultCode != HZCYKJTHardWare_RET_OK)
            context.errorCode = std::to_string(resultCode);
        context.durationMs = static_cast<long long>(durationMs);
        const std::string fields = FormatLogContext(context);
        Logger::Instance().Debug("接口", operation ? operation : "unknown",
                                 "导出调用边界：阶段=%s %s",
                                 phase ? phase : "边界", fields.c_str());
    } catch (...) {
        // 日志属于旁路能力，不能因格式化或落盘异常改变 DLL 导出行为。
    }
}

#define HZCY_GUARDED_EXPORT(bodyCall)                                      \
    if (!HZCYKJTHardWare::SdkRuntime::Instance().TryEnterCall(__FUNCTION__)) return 0; \
    BeginExportRequestContext(__FUNCTION__);                               \
    const ULONGLONG guardedStartedAt = GetTickCount64();                   \
    LogExportBoundary(__FUNCTION__, "入口", "开始",                       \
                      HZCYKJTHardWare_RET_OK, 0,                             \
                      CurrentExportRequestId().c_str());                    \
    int guardedCallResult = HZCYKJTHardWare_RET_FAILED;                    \
    int guardedResult = 0;                                                 \
    __try { guardedCallResult = (bodyCall);                                \
            guardedResult = (guardedCallResult == HZCYKJTHardWare_RET_OK) ? 1 : 0; } \
    __except(EXCEPTION_EXECUTE_HANDLER) { guardedCallResult = HZCYKJTHardWare_RET_FAILED; guardedResult = 0; } \
    HZCYKJTHardWare::SdkRuntime::Instance().LeaveCall();                   \
    LogExportBoundary(__FUNCTION__, "出口", guardedResult ? "成功" : "失败", \
                      guardedCallResult, GetTickCount64() - guardedStartedAt, \
                      CurrentExportRequestId().c_str());                    \
    ClearExportRequestContext();                                            \
    return guardedResult

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_InitSdk(void) {
    bool shouldInitialize = false;
    if (!HZCYKJTHardWare::SdkRuntime::Instance().BeginInitialize(
            shouldInitialize, 5000)) {
        return 0;
    }
    if (!shouldInitialize) return 1;

    int result = HZCYKJTHardWare_RET_FAILED;
    __try { result = InitSdkBody(); }
    __except(EXCEPTION_EXECUTE_HANDLER) {
        OutputDebugStringA("=== HZCYKJTHardWare_InitSdk CRASH ===");
        result = HZCYKJTHardWare_RET_FAILED;
    }
    const bool success = (result == HZCYKJTHardWare_RET_OK);
    HZCYKJTHardWare::SdkRuntime::Instance().CompleteInitialize(success);
    return success ? 1 : 0;
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_ReleaseSdk(void) {
    const int activeCallDrainTimeoutMs = 20000;
    bool shouldRelease = false;
    if (!HZCYKJTHardWare::SdkRuntime::Instance().BeginRelease(
            shouldRelease, 5000)) {
        return 0;
    }
    if (!shouldRelease) return 1;

    if (!HZCYKJTHardWare::SdkRuntime::Instance().WaitForActiveCalls(activeCallDrainTimeoutMs)) {
        LogActiveCallDrainTimeout(activeCallDrainTimeoutMs);
        HZCYKJTHardWare::SdkRuntime::Instance().CompleteRelease(false, true);
        return 0;
    }

    int result = HZCYKJTHardWare_RET_FAILED;
    bool canResumeRunning = true;
    __try { result = ReleaseSdkBody(canResumeRunning); }
    __except(EXCEPTION_EXECUTE_HANDLER) { result = HZCYKJTHardWare_RET_FAILED; }
    const bool success = (result == HZCYKJTHardWare_RET_OK);
    HZCYKJTHardWare::SdkRuntime::Instance().CompleteRelease(
        success, canResumeRunning);
    return success ? 1 : 0;
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_SwitchTerminal(int terminalIndex) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::TerminalControl) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(SwitchTerminalBody(terminalIndex));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartProcess(const char* saveDir) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::ProcessControl) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StartProcessBody(saveDir));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_EndProcess(void) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::ProcessControl) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(EndProcessBody());
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartCameraPreview(void* hwnd) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Face) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StartCameraPreviewBody(hwnd));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopCameraPreview(void) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Face) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StopCameraPreviewBody());
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartFingerprintPreview(void* hwnd) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Fingerprint) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StartFingerprintPreviewBody(hwnd));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopFingerprintPreview(void) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Fingerprint) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StopFingerprintPreviewBody());
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartIrisPreview(void* hwnd) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Iris) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StartIrisPreviewBody(hwnd));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopIrisPreview(void) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Iris) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StopIrisPreviewBody());
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreviewCJ(void* hwnd) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::PlateCJ) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StartPlatePreviewBody(PlatePreviewChannel::CJ, hwnd));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreviewCJ(void) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::PlateCJ) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(StopPlatePreviewBody(PlatePreviewChannel::CJ));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreviewRJ2(void* hwnd) {
    HZCY_GUARDED_EXPORT(StartPlatePreviewBody(PlatePreviewChannel::RJ2, hwnd));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreviewRJ2(void) {
    HZCY_GUARDED_EXPORT(StopPlatePreviewBody(PlatePreviewChannel::RJ2));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreviewRJ3(void* hwnd) {
    HZCY_GUARDED_EXPORT(StartPlatePreviewBody(PlatePreviewChannel::RJ3, hwnd));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreviewRJ3(void) {
    HZCY_GUARDED_EXPORT(StopPlatePreviewBody(PlatePreviewChannel::RJ3));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_SaveLatestPlateFrame(
    const char* savePath, int cameraType) {
    if (!HZCYKJTHardWare::SdkRuntime::Instance().TryEnterCall(__FUNCTION__))
        return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    int result = HZCYKJTHardWare_RET_FAILED;
    __try { result = SaveLatestPlateFrameSafeBody(savePath, cameraType); }
    __except(EXCEPTION_EXECUTE_HANDLER) { result = HZCYKJTHardWare_RET_FAILED; }
    HZCYKJTHardWare::SdkRuntime::Instance().LeaveCall();
    return result;
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureCameraImage(const char* saveDir) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Face) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(CaptureCameraImageBody(saveDir));
}

static int CaptureFingerprintImageBody(const char* saveDir, const char* saveDirHk) {
    if (IsSwitchPending()) {
        LOG_WARN("接口", "指纹抓拍被终端切换拦截");
        return HZCYKJTHardWare_RET_DEVICE_BUSY;
    }
    return CaptureFingerprintImageDirect(saveDir, saveDirHk);
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureFingerprintImage(const char* saveDir, const char* saveDirHk) {
    if (RequireCapability(__FUNCTION__, DeviceCapability::Fingerprint) != HZCYKJTHardWare_RET_OK) return 0;
    HZCY_GUARDED_EXPORT(CaptureFingerprintImageBody(saveDir, saveDirHk));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureIrisImage(const char* saveDir) {
    if (RejectUnsupportedAsync(__FUNCTION__, DeviceCapability::Iris,
            HZCYKJTHardWare_RESOURCE_IRIS_IMAGE,
            HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED)) return 0;
    HZCY_GUARDED_EXPORT(CaptureIrisImageBody(saveDir));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestOCR(const char* saveDir) {
    if (RejectUnsupportedAsync(__FUNCTION__, DeviceCapability::OCR,
            HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT,
            HZCYKJTHardWare_EVENT_OCR_FAILED)) return 0;
    HZCY_GUARDED_EXPORT(RequestOCRBody(saveDir));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestNfcCard(const char* saveDir) {
    if (RejectUnsupportedAsync(__FUNCTION__, DeviceCapability::NfcCard,
            HZCYKJTHardWare_RESOURCE_NFC_CARD,
            HZCYKJTHardWare_EVENT_NFC_CARD_FAILED)) return 0;
    HZCY_GUARDED_EXPORT(RequestNfcCardBody(saveDir));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestAuthorize(
    const char* ZJHM, const char* ZJLB, const char* GJDQDM,
    const char* XM, const char* XB, const char* CSRQ, const char* KADM)
{
    if (RejectUnsupportedAsync(__FUNCTION__, DeviceCapability::Authorize,
            HZCYKJTHardWare_RESOURCE_AUTHORIZATION,
            HZCYKJTHardWare_EVENT_AUTHORIZE_FAILED)) return 0;
    HZCY_GUARDED_EXPORT(RequestAuthorizeBody(ZJHM, ZJLB, GJDQDM, XM, XB, CSRQ, KADM));
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RegisterEventCallback(
    THZCYKJTHardWareEventCallback callback)
{
    // 保留 InitSdk 前允许注册回调的既有行为，同时避免与 InitSdk/ReleaseSdk 发生竞争。
    if (!HZCYKJTHardWare::SdkRuntime::Instance().TryEnterCallbackRegistration(__FUNCTION__)) return 0;
    int guardedResult = 0;
    __try { guardedResult = (RegisterEventCallbackBody(callback) == HZCYKJTHardWare_RET_OK) ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { guardedResult = 0; }
    HZCYKJTHardWare::SdkRuntime::Instance().LeaveCall();
    return guardedResult;
}

#undef HZCY_GUARDED_EXPORT
