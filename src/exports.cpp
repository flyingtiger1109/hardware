#include "pch.h"
#include "include/HZCYKJTHardWare.h"
#include "hzsjkjt_context.h"
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

// Export function implementations use small body helpers so SEH wrappers do not
// span C++ object lifetimes.

static std::string GenerateSyncRequestId(const char* prefix) {
    static std::atomic<int> seq{0};
    int currentSeq = ++seq;
    char seqBuf[16];
    snprintf(seqBuf, sizeof(seqBuf), "%03d", currentSeq);
    return std::string(prefix) + "_" + HZCYKJTHardWare::PathHelper::GetTimestampString() + "_" + seqBuf;
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
        LOG_WARN("EXPORT", "检查Delphi程序进程失败：error=%lu", GetLastError());
        return false;
    }

    PROCESSENTRY32W entry = {};
    entry.dwSize = sizeof(entry);
    if (Process32FirstW(snapshot, &entry)) {
        do {
            if (_wcsicmp(entry.szExeFile, executableName.c_str()) == 0) {
                HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, entry.th32ProcessID);
                if (!process) {
                    LOG_WARN("EXPORT", "检查同名Delphi进程路径失败：pid=%lu，error=%lu",
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
            LOG_ERROR("EXPORT", "重启Delphi程序失败：无法打开待终止进程，path=%s，pid=%lu，error=%lu",
                      executablePath.c_str(), processId, error);
            return false;
        }

        if (!TerminateProcess(process, 1)) {
            DWORD error = GetLastError();
            CloseHandle(process);
            LOG_ERROR("EXPORT", "重启Delphi程序失败：终止进程失败，path=%s，pid=%lu，error=%lu",
                      executablePath.c_str(), processId, error);
            return false;
        }

        DWORD waitResult = WaitForSingleObject(process, 5000);
        CloseHandle(process);
        if (waitResult != WAIT_OBJECT_0) {
            LOG_ERROR("EXPORT", "重启Delphi程序失败：等待旧进程退出超时，path=%s，pid=%lu，result=%lu",
                      executablePath.c_str(), processId, waitResult);
            return false;
        }
        LOG_INFO("EXPORT", "通信服务不可用，已终止旧Delphi程序：path=%s，pid=%lu",
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
        LOG_ERROR("EXPORT", "自动启动Delphi程序失败：可执行文件不存在，path=%s", executablePath.c_str());
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
        LOG_ERROR("EXPORT", "自动启动Delphi程序失败：CreateProcessW失败，path=%s，error=%lu",
                  executablePath.c_str(), GetLastError());
        return false;
    }

    LOG_INFO("EXPORT", "已自动启动Delphi程序：path=%s，pid=%lu",
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
        LOG_ERROR("EXPORT", "Delphi程序/ping失败且自动启动未启用：delphi_server=%s",
                  delphiServerUrl.c_str());
        return false;
    }

    std::string executablePath = ResolveDelphiExecutablePath(cfg, dllDir);
    if (!HZCYKJTHardWare::PathHelper::FileExists(executablePath)) {
        LOG_ERROR("EXPORT", "自动启动Delphi程序失败：可执行文件不存在，path=%s", executablePath.c_str());
        return false;
    }

    int intervalMs = cfg.GetDelphiPingIntervalMs();
    std::vector<DWORD> existingProcessIds;
    if (!FindProcessIdsForExecutablePath(executablePath, existingProcessIds)) {
        LOG_ERROR("EXPORT", "自动恢复Delphi程序失败：无法检查同路径进程，path=%s",
                  executablePath.c_str());
        return false;
    }

    if (!existingProcessIds.empty()) {
        LOG_WARN("EXPORT", "Delphi程序已运行但通信服务不可用，正在立即重启同路径进程：path=%s，pid=%lu，delphi_server=%s",
                 executablePath.c_str(), existingProcessIds.front(), delphiServerUrl.c_str());
        if (!TerminateDelphiServiceProcesses(executablePath, existingProcessIds)) {
            return false;
        }
    }

    if (!StartDelphiServiceProcess(cfg, dllDir, executablePath)) {
        return false;
    }

    int waitMs = cfg.GetDelphiStartWaitMs();
    if (WaitForDelphiService(proxy, waitMs, intervalMs)) {
        LOG_INFO("EXPORT", "自动启动后的Delphi通信服务已就绪：delphi_server=%s，path=%s",
                 delphiServerUrl.c_str(), executablePath.c_str());
        return true;
    }

    LOG_ERROR("EXPORT", "启动Delphi程序后等待/ping超时：delphi_server=%s，path=%s，wait_ms=%d",
              delphiServerUrl.c_str(), executablePath.c_str(), waitMs);
    return false;
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
        ctx.current_terminal_index = 0;
        ctx.current_terminal_base_url = delphiServerUrl;
        ctx.selected_lan_ip.clear();
        ctx.selected_subnet_prefix.clear();

        ctx.http_connect_timeout_ms = cfg.GetHttpConnectTimeoutMs();
        ctx.http_request_timeout_ms = cfg.GetHttpRequestTimeoutMs();
        ctx.face_capture_timeout_ms = cfg.GetFaceCaptureTimeoutMs();
        ctx.fingerprint_capture_timeout_ms = cfg.GetFingerprintCaptureTimeoutMs();
        ctx.ocr_timeout_ms = cfg.GetOcrTimeoutMs();

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
    }

    std::string callbackHost = cfg.GetCallbackServerHost();
    if (callbackHost.empty()) {
        callbackHost = "127.0.0.1";
    }

    std::string listenHost = cfg.GetListenAny() ? "0.0.0.0" : callbackHost;
    int callbackPort = cfg.GetCallbackServerPort();
    std::string callbackUrl = "http://" + callbackHost + ":" +
        std::to_string(callbackPort) + cfg.GetCallbackBasePath();

    LOG_DEBUG("EXPORT", "正在启动Delphi回调接收服务：listen=%s:%d，callback_url=%s",
             listenHost.c_str(), callbackPort, callbackUrl.c_str());

    {
        auto lock = WriteLock();
        ctx.callback_server_host = listenHost;
        ctx.callback_server_port = callbackPort;
        ctx.callback_url_host = callbackHost;
    }

    ret = CallbackServer::Instance().Start(listenHost, callbackPort);
    if (ret != HZCYKJTHardWare_RET_OK) {
        LOG_ERROR("EXPORT", "初始化DLL失败：Delphi回调接收服务启动失败，listen=%s:%d", listenHost.c_str(), callbackPort);
        Logger::Instance().Shutdown();
        return HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED;
    }
    {
        auto lock = WriteLock();
        ctx.callback_server_running = true;
    }

    EventDispatcher::Instance().Start();

    LOG_INFO("EXPORT", "初始化DLL：正在检查Delphi通信服务，delphi_server=%s，auto_start=%s",
             delphiServerUrl.c_str(), cfg.GetDelphiAutoStart() ? "true" : "false");
    DelphiProxy proxy(delphiServerUrl);
    if (!EnsureDelphiServiceAvailable(proxy, cfg, ctx.dll_dir, delphiServerUrl)) {
        LOG_ERROR("EXPORT", "初始化DLL失败：Delphi程序/ping不可用，delphi_server=%s", delphiServerUrl.c_str());
        EventDispatcher::Instance().Stop();
        CallbackServer::Instance().Stop();
        {
            auto lock = WriteLock();
            ctx.callback_server_running = false;
            ctx.delphi_server_url.clear();
            ctx.current_terminal_base_url.clear();
        }
        Logger::Instance().Shutdown();
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    {
        auto lock = WriteLock();
        ctx.initialized = true;
    }

    LOG_INFO("EXPORT", "初始化DLL成功：delphi_server=%s，callback_url=%s，dll_dir=%s",
             delphiServerUrl.c_str(), callbackUrl.c_str(), ctx.dll_dir.c_str());

    return HZCYKJTHardWare_RET_OK;
}

static int ReleaseSdkBody() {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：ReleaseSdk()");

    auto& ctx = HzsjkjtContext::Instance();

    {
        auto lock = ReadLock();
        if (!ctx.initialized) {
            return HZCYKJTHardWare_RET_OK;
        }
    }

    PreviewManager::Instance().StopAllRenderers();

    RequestSessionManager::Instance().CancelAll();

    CallbackServer::Instance().Stop();

    EventDispatcher::Instance().Stop();

    {
        auto lock = WriteLock();
        ctx.callback_server_running = false;
        ctx.Reset();
    }

    Logger::Instance().Shutdown();

    return HZCYKJTHardWare_RET_OK;
}

static int SetTerminalBaseUrlBody(const char* baseUrl) {
    using namespace HZCYKJTHardWare;
    if (!baseUrl || !baseUrl[0]) return HZCYKJTHardWare_RET_INVALID_PARAM;
    if (!HzsjkjtContext::Instance().initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    LOG_WARN("EXPORT", "代理模式不支持DLL直连终端URL：base_url=%s，实际终端由Delphi程序管理", baseUrl);
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

    std::string listenHost("0.0.0.0");
    std::string callbackHost = host ? host : "";
    if (callbackHost.empty()) {
        callbackHost = selectedLanIp;
        if (callbackHost.empty()) callbackHost = "127.0.0.1";
    }

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
    LOG_INFO("EXPORT", "第三方调用：SwitchTerminal(%d)", terminalIndex);
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (terminalIndex <= 0) return HZCYKJTHardWare_RET_TERMINAL_INDEX_INVALID;

    // Same terminal — skip
    {
        auto lock = ReadLock();
        if (ctx.current_terminal_index == terminalIndex) {
            LOG_INFO("EXPORT", "终端切换请求跳过：当前已在终端%d", terminalIndex);
            return HZCYKJTHardWare_RET_OK;
        }
    }

    RequestSessionManager::Instance().ExpireAllForTerminalSwitch();

    bool cameraRunning = false;
    bool fingerprintRunning = false;
    bool irisRunning = false;
    std::string cameraRequestId;
    std::string fingerprintRequestId;
    std::string irisRequestId;
    HWND cameraHwnd = nullptr;
    HWND fingerprintHwnd = nullptr;
    HWND irisHwnd = nullptr;
    std::string delphiServerUrl;
    {
        auto lock = ReadLock();
        delphiServerUrl = ctx.delphi_server_url;
        cameraRunning = ctx.camera_preview_running;
        fingerprintRunning = ctx.fingerprint_preview_running;
        irisRunning = ctx.iris_preview_running;
        cameraRequestId = ctx.camera_preview_request_id;
        fingerprintRequestId = ctx.fingerprint_preview_request_id;
        irisRequestId = ctx.iris_preview_request_id;
        cameraHwnd = reinterpret_cast<HWND>(ctx.camera_preview_third_party_hwnd);
        fingerprintHwnd = reinterpret_cast<HWND>(ctx.fingerprint_preview_third_party_hwnd);
        irisHwnd = reinterpret_cast<HWND>(ctx.iris_preview_third_party_hwnd);
    }

    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.SwitchTerminal(terminalIndex)) {
        LOG_ERROR("EXPORT", "终端切换失败：DLL转发Delphi程序失败，terminal_index=%d", terminalIndex);
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    {
        auto lock = WriteLock();
        ctx.current_terminal_index = terminalIndex;
    }

    if (cameraRunning) PreviewManager::Instance().StopCameraPreviewRenderer(false);
    if (fingerprintRunning) PreviewManager::Instance().StopFingerprintPreviewRenderer(false);
    if (irisRunning) PreviewManager::Instance().StopIrisPreviewRenderer(false);

    std::string rtspUrl;
    int restoreRet = HZCYKJTHardWare_RET_OK;
    if (cameraRunning &&
        (!proxy.GetCameraPreviewUrl(cameraRequestId, rtspUrl) ||
         (restoreRet = PreviewManager::Instance().StartCameraPreviewFromUrl(cameraHwnd, rtspUrl)) != HZCYKJTHardWare_RET_OK)) {
        LOG_ERROR("EXPORT", "切换终端后恢复摄像头预览失败：request_id=%s", cameraRequestId.c_str());
        return restoreRet == HZCYKJTHardWare_RET_OK ? HZCYKJTHardWare_RET_HTTP_FAILED : restoreRet;
    }
    if (fingerprintRunning &&
        (!proxy.GetFingerprintPreviewUrl(fingerprintRequestId, rtspUrl) ||
         (restoreRet = PreviewManager::Instance().StartFingerprintPreviewFromUrl(fingerprintHwnd, rtspUrl)) != HZCYKJTHardWare_RET_OK)) {
        LOG_ERROR("EXPORT", "切换终端后恢复指纹预览失败：request_id=%s", fingerprintRequestId.c_str());
        return restoreRet == HZCYKJTHardWare_RET_OK ? HZCYKJTHardWare_RET_HTTP_FAILED : restoreRet;
    }
    if (irisRunning &&
        (!proxy.GetIrisPreviewUrl(irisRequestId, rtspUrl) ||
         (restoreRet = PreviewManager::Instance().StartIrisPreviewFromUrl(irisHwnd, rtspUrl)) != HZCYKJTHardWare_RET_OK)) {
        LOG_ERROR("EXPORT", "切换终端后恢复虹膜预览失败：request_id=%s", irisRequestId.c_str());
        return restoreRet == HZCYKJTHardWare_RET_OK ? HZCYKJTHardWare_RET_HTTP_FAILED : restoreRet;
    }

    LOG_INFO("EXPORT", "终端切换请求已转发Delphi程序：terminal_index=%d", terminalIndex);
    return HZCYKJTHardWare_RET_OK;
}

static int SwitchTerminalByUrlBody(const char* terminalBaseUrl) {
    using namespace HZCYKJTHardWare;
    if (!HzsjkjtContext::Instance().initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!terminalBaseUrl || !terminalBaseUrl[0]) return HZCYKJTHardWare_RET_INVALID_PARAM;
    LOG_WARN("EXPORT", "代理模式不支持DLL按URL切换终端：terminal_base_url=%s，实际终端由Delphi程序管理", terminalBaseUrl);
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
    return proxy.Ping() ? HZCYKJTHardWare_RET_OK : HZCYKJTHardWare_RET_TERMINAL_UNREACHABLE;
}

static int StartProcessBody(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StartProcess(saveDir=%s)", saveDir ? saveDir : "NULL");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string saveRoot = ResolveSaveRoot(saveDir);
    std::string requestId = GenerateSyncRequestId("HZCYKJTHardWare_PROCESS");

    // Create session so callbacks can be matched
    RequestSessionManager::Instance().CreateSession(
        requestId, saveRoot, ctx.ocr_timeout_ms);
    RequestSessionManager::Instance().MarkAccepted(requestId);

    // Build callbacks JSON for async operations
    std::string ocrCallback = BuildCallbackUrl(ctx, "/ocr");
    std::string nfcCallback = BuildCallbackUrl(ctx, "/nfc-card");
    std::string irisCallback = BuildCallbackUrl(ctx, "/iris");
    std::string callbacksJson = "{" +
        std::string("\"callbacks\":{") +
        "\"ocr\":\"" + ocrCallback + "\"," +
        "\"nfc\":\"" + nfcCallback + "\"," +
        "\"iris\":\"" + irisCallback + "\"" +
        "}}";

    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.ProcessStart(requestId, saveRoot, callbacksJson)) {
        LOG_ERROR("EXPORT", "开始流程失败：DLL转发Delphi程序失败，delphi_server=%s", ctx.delphi_server_url.c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    {
        auto lock = WriteLock();
        ctx.process_active = true;
    }

    LOG_INFO("EXPORT", "Delphi程序已受理开始流程：delphi_server=%s", ctx.delphi_server_url.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int EndProcessBody() {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：EndProcess()");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    RequestSessionManager::Instance().CancelAll();
    DelphiProxy proxy(ctx.delphi_server_url);
    bool ok = proxy.ProcessEnd();

    {
        auto lock = WriteLock();
        ctx.process_active = false;
    }

    if (!ok) {
        LOG_ERROR("EXPORT", "结束流程失败：DLL转发Delphi程序失败，delphi_server=%s", ctx.delphi_server_url.c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    LOG_INFO("EXPORT", "Delphi程序已处理结束流程：delphi_server=%s", ctx.delphi_server_url.c_str());
    return HZCYKJTHardWare_RET_OK;
}

// ---- 棰勮 ----

static int StartCameraPreviewBody(void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StartCameraPreview(hwnd=%p)", hwnd);
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!hwnd || !IsWindow(reinterpret_cast<HWND>(hwnd))) {
        LOG_ERROR("EXPORT", "启动摄像头预览失败：第三方HWND无效，hwnd=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    std::string requestId = GenerateSyncRequestId("HZCYKJTHardWare_PREVIEW");
    std::string rtspUrl;
    std::string delphiServerUrl;
    intptr_t thirdPartyHwnd = reinterpret_cast<intptr_t>(hwnd);
    {
        auto lock = WriteLock();
        if (ctx.camera_preview_running) {
            LOG_WARN("EXPORT", "启动摄像头预览失败：预览已运行，request_id=%s",
                     ctx.camera_preview_request_id.c_str());
            return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
        }
        delphiServerUrl = ctx.delphi_server_url;
        ctx.camera_preview_running = true;
        ctx.camera_preview_request_id = requestId;
        ctx.camera_preview_third_party_hwnd = thirdPartyHwnd;
    }

    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.GetCameraPreviewUrl(requestId, rtspUrl)) {
        LOG_ERROR("EXPORT", "启动摄像头预览失败：向Delphi程序获取预览地址失败，request_id=%s，delphi_server=%s",
                  requestId.c_str(), delphiServerUrl.c_str());
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id == requestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    int ret = PreviewManager::Instance().StartCameraPreviewFromUrl(reinterpret_cast<HWND>(hwnd), rtspUrl);
    if (ret != HZCYKJTHardWare_RET_OK) {
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id == requestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
        }
        return ret;
    }

    PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_FACE_IMAGE,
                     HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STARTED,
                     HZCYKJTHardWare_RET_OK, "", "预览已就绪");
    LOG_INFO("EXPORT", "摄像头预览已在第三方进程启动：request_id=%s，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    return HZCYKJTHardWare_RET_OK;
}

static int StopCameraPreviewBody() {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StopCameraPreview()");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId;
    {
        auto lock = ReadLock();
        if (!ctx.camera_preview_running) {
            return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
        }
        requestId = ctx.camera_preview_request_id;
    }

    PreviewManager::Instance().StopCameraPreviewRenderer();

    {
        auto lock = WriteLock();
        if (ctx.camera_preview_request_id == requestId) {
            ctx.camera_preview_running = false;
            ctx.camera_preview_request_id.clear();
            ctx.camera_preview_third_party_hwnd = 0;
        }
    }

    LOG_INFO("EXPORT", "摄像头预览已停止：request_id=%s", requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int StartFingerprintPreviewBody(void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StartFingerprintPreview(hwnd=%p)", hwnd);
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!hwnd || !IsWindow(reinterpret_cast<HWND>(hwnd))) {
        LOG_ERROR("EXPORT", "启动指纹预览失败：第三方HWND无效，hwnd=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    std::string requestId = GenerateSyncRequestId("HZCYKJTHardWare_FP_PREVIEW");
    std::string rtspUrl;
    std::string delphiServerUrl;
    intptr_t thirdPartyHwnd = reinterpret_cast<intptr_t>(hwnd);
    {
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_running) {
            LOG_WARN("EXPORT", "启动指纹预览失败：预览已运行，request_id=%s",
                     ctx.fingerprint_preview_request_id.c_str());
            return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
        }
        delphiServerUrl = ctx.delphi_server_url;
        ctx.fingerprint_preview_running = true;
        ctx.fingerprint_preview_request_id = requestId;
        ctx.fingerprint_preview_third_party_hwnd = thirdPartyHwnd;
    }

    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.GetFingerprintPreviewUrl(requestId, rtspUrl)) {
        LOG_ERROR("EXPORT", "启动指纹预览失败：向Delphi程序获取预览地址失败，request_id=%s", requestId.c_str());
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_request_id == requestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
        }
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    int ret = PreviewManager::Instance().StartFingerprintPreviewFromUrl(reinterpret_cast<HWND>(hwnd), rtspUrl);
    if (ret != HZCYKJTHardWare_RET_OK) {
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_request_id == requestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
        }
        return ret;
    }

    PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE,
                     HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STARTED,
                     HZCYKJTHardWare_RET_OK, "", "预览已就绪");
    LOG_INFO("EXPORT", "指纹预览已在第三方进程启动：request_id=%s，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    return HZCYKJTHardWare_RET_OK;
}

static int StopFingerprintPreviewBody() {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StopFingerprintPreview()");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId;
    {
        auto lock = ReadLock();
        if (!ctx.fingerprint_preview_running) {
            return HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING;
        }
        requestId = ctx.fingerprint_preview_request_id;
    }

    PreviewManager::Instance().StopFingerprintPreviewRenderer();

    {
        auto lock = WriteLock();
        if (ctx.fingerprint_preview_request_id == requestId) {
            ctx.fingerprint_preview_running = false;
            ctx.fingerprint_preview_request_id.clear();
            ctx.fingerprint_preview_third_party_hwnd = 0;
        }
    }

    LOG_INFO("EXPORT", "指纹预览已停止：request_id=%s", requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int StartIrisPreviewBody(void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StartIrisPreview(hwnd=%p)", hwnd);
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    if (!hwnd || !IsWindow(reinterpret_cast<HWND>(hwnd))) {
        LOG_ERROR("EXPORT", "启动虹膜预览失败：第三方HWND无效，hwnd=%p", hwnd);
        return HZCYKJTHardWare_RET_INVALID_HWND;
    }

    std::string requestId = GenerateSyncRequestId("HZCYKJTHardWare_IRIS_PREVIEW");
    std::string rtspUrl;
    std::string delphiServerUrl;
    intptr_t thirdPartyHwnd = reinterpret_cast<intptr_t>(hwnd);
    {
        auto lock = WriteLock();
        if (ctx.iris_preview_running) {
            LOG_WARN("EXPORT", "启动虹膜预览失败：预览已运行，request_id=%s",
                     ctx.iris_preview_request_id.c_str());
            return HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING;
        }
        delphiServerUrl = ctx.delphi_server_url;
        ctx.iris_preview_running = true;
        ctx.iris_preview_request_id = requestId;
        ctx.iris_preview_third_party_hwnd = thirdPartyHwnd;
    }

    DelphiProxy proxy(delphiServerUrl);
    if (!proxy.GetIrisPreviewUrl(requestId, rtspUrl)) {
        LOG_ERROR("EXPORT", "启动虹膜预览失败：向Delphi程序获取预览地址失败，request_id=%s", requestId.c_str());
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
    LOG_INFO("EXPORT", "虹膜预览已在第三方进程启动：request_id=%s，third_party_hwnd=%p",
             requestId.c_str(), hwnd);
    return HZCYKJTHardWare_RET_OK;
}

static int StopIrisPreviewBody() {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StopIrisPreview()");
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

    PreviewManager::Instance().StopIrisPreviewRenderer();

    {
        auto lock = WriteLock();
        if (ctx.iris_preview_request_id == requestId) {
            ctx.iris_preview_running = false;
            ctx.iris_preview_request_id.clear();
            ctx.iris_preview_third_party_hwnd = 0;
        }
    }

    LOG_INFO("EXPORT", "虹膜预览已停止：request_id=%s", requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int StartPlatePreviewBody(void* hwnd) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StartPlatePreview(hwnd=%p)", hwnd);
    if (!HzsjkjtContext::Instance().initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    LOG_WARN("EXPORT", "代理模式暂不支持车牌预览：未定义Delphi HTTP端点，已拒绝调用");
    return HZCYKJTHardWare_RET_UNSUPPORTED;
}

static int StopPlatePreviewBody() {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：StopPlatePreview()");
    if (!HzsjkjtContext::Instance().initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;
    LOG_WARN("EXPORT", "代理模式暂不支持停止车牌预览：未定义Delphi HTTP端点，已拒绝调用");
    return HZCYKJTHardWare_RET_UNSUPPORTED;
}

// ---- 鎶撴媿 ----

static int CaptureCameraImageBody(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：CaptureCameraImage(saveDir=%s)", saveDir ? saveDir : "NULL");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId = GenerateSyncRequestId("HZCYKJTHardWare_FACE");
    std::string saveRoot = ResolveCaptureTargetPath(saveDir, true);
    std::string savePath;
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.CaptureFace(requestId, saveRoot, savePath)) {
        LOG_ERROR("EXPORT", "人脸抓拍失败：DLL转发Delphi程序失败，request_id=%s，delphi_server=%s",
                  requestId.c_str(), ctx.delphi_server_url.c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    LOG_INFO("EXPORT", "人脸抓拍成功：request_id=%s，save_path=%s", requestId.c_str(), savePath.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int CaptureFingerprintImageBody(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：CaptureFingerprintImage(saveDir=%s)", saveDir ? saveDir : "NULL");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string requestId = GenerateSyncRequestId("HZCYKJTHardWare_FP");
    std::string saveRoot = ResolveCaptureTargetPath(saveDir, false);
    std::string savePath;
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.CaptureFingerprint(requestId, saveRoot, savePath)) {
        LOG_ERROR("EXPORT", "指纹抓拍失败：DLL转发Delphi程序失败，request_id=%s，delphi_server=%s",
                  requestId.c_str(), ctx.delphi_server_url.c_str());
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    LOG_INFO("EXPORT", "指纹抓拍成功：request_id=%s，save_path=%s", requestId.c_str(), savePath.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int CaptureIrisImageBody(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：CaptureIrisImage(saveDir=%s)", saveDir ? saveDir : "NULL");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    int timeoutMs = ctx.face_capture_timeout_ms;
    std::string saveRoot = ResolveSaveRoot(saveDir);
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, saveRoot, timeoutMs);

    std::string callbackUrl = BuildCallbackUrl(ctx, "/iris");
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.CaptureIrisAsync(requestId, saveRoot, callbackUrl)) {
        LOG_ERROR("EXPORT", "虹膜抓拍提交失败：DLL转发Delphi程序失败，request_id=%s，delphi_server=%s",
                  requestId.c_str(), ctx.delphi_server_url.c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_IRIS_IMAGE, HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED,
                         HZCYKJTHardWare_RET_HTTP_FAILED, "", "虹膜抓拍请求发送失败",
                         nullptr, nullptr);
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    RequestSessionManager::Instance().MarkAccepted(requestId);
    LOG_INFO("EXPORT", "Delphi程序已受理虹膜抓拍请求：request_id=%s，callback_url=%s", requestId.c_str(), callbackUrl.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int RequestOCRBody(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：RequestOCR(saveDir=%s)", saveDir ? saveDir : "NULL");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    std::string saveRoot = ResolveSaveRoot(saveDir);
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, saveRoot, ctx.ocr_timeout_ms);

    std::string callbackUrl = BuildCallbackUrl(ctx, "/ocr");
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.RequestOcrAsync(requestId, saveRoot, callbackUrl)) {
        LOG_ERROR("EXPORT", "OCR请求提交失败：DLL转发Delphi程序失败，request_id=%s，delphi_server=%s",
                  requestId.c_str(), ctx.delphi_server_url.c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT, HZCYKJTHardWare_EVENT_OCR_FAILED,
                         HZCYKJTHardWare_RET_HTTP_FAILED, "", "OCR识别请求发送失败",
                         nullptr, nullptr);
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    RequestSessionManager::Instance().MarkAccepted(requestId);
    LOG_INFO("EXPORT", "Delphi程序已受理OCR请求：request_id=%s，callback_url=%s", requestId.c_str(), callbackUrl.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int RequestNfcCardBody(const char* saveDir) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：RequestNfcCard(saveDir=%s)", saveDir ? saveDir : "NULL");
    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    int timeoutMs = ctx.ocr_timeout_ms;
    std::string saveRoot = ResolveSaveRoot(saveDir);
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_NFC_CARD, saveRoot, timeoutMs);

    std::string callbackUrl = BuildCallbackUrl(ctx, "/nfc-card");
    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.RequestNfcAsync(requestId, saveRoot, callbackUrl)) {
        LOG_ERROR("NFC", "IC卡识别请求提交失败：DLL转发Delphi程序失败，request_id=%s，delphi_server=%s",
                  requestId.c_str(), ctx.delphi_server_url.c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_NFC_CARD, HZCYKJTHardWare_EVENT_NFC_CARD_FAILED,
                         HZCYKJTHardWare_RET_HTTP_FAILED, "", "IC卡识别请求发送失败",
                         nullptr, nullptr);
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    RequestSessionManager::Instance().MarkAccepted(requestId);
    LOG_INFO("NFC", "Delphi程序已受理IC卡识别请求：request_id=%s，callback_url=%s",
             requestId.c_str(), callbackUrl.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int RequestAuthorizeBody(const char* ZJHM, const char* ZJLB,
                                const char* GJDQDM, const char* XM,
                                const char* XB, const char* CSRQ,
                                const char* KADM) {
    using namespace HZCYKJTHardWare;
    LOG_INFO("EXPORT", "第三方调用：RequestAuthorize(ZJHM=%s, XM=%s, XB=%s)",
             ZJHM ? ZJHM : "NULL", XM ? XM : "NULL", XB ? XB : "NULL");

    auto& ctx = HzsjkjtContext::Instance();
    if (!ctx.initialized) return HZCYKJTHardWare_RET_NOT_INITIALIZED;

    int timeoutMs = ctx.ocr_timeout_ms;
    std::string requestId = RequestSessionManager::Instance().CreateSession(
        HZCYKJTHardWare_RESOURCE_AUTHORIZATION, "", timeoutMs);

    std::string callbackUrl = BuildCallbackUrl(ctx, "/authorize");

    DelphiProxy proxy(ctx.delphi_server_url);
    if (!proxy.RequestAuthorize(requestId,
                                ZJHM ? ZJHM : "",
                                ZJLB ? ZJLB : "",
                                GJDQDM ? GJDQDM : "",
                                XM ? XM : "",
                                XB ? XB : "",
                                CSRQ ? CSRQ : "",
                                KADM ? KADM : "",
                                callbackUrl)) {
        LOG_ERROR("EXPORT", "授权请求提交失败：DLL转发Delphi程序失败，request_id=%s", requestId.c_str());
        PostCaptureEvent(requestId, HZCYKJTHardWare_RESOURCE_AUTHORIZATION,
                         HZCYKJTHardWare_EVENT_AUTHORIZE_FAILED,
                         HZCYKJTHardWare_RET_HTTP_FAILED, "",
                         "Authorization request HTTP failed",
                         nullptr, nullptr);
        return HZCYKJTHardWare_RET_HTTP_FAILED;
    }

    RequestSessionManager::Instance().MarkAccepted(requestId);
    LOG_INFO("EXPORT", "Delphi程序已受理授权请求：request_id=%s", requestId.c_str());
    return HZCYKJTHardWare_RET_OK;
}

static int RegisterEventCallbackBody(THZCYKJTHardWareEventCallback callback) {
    using namespace HZCYKJTHardWare;
    if (!callback) return HZCYKJTHardWare_RET_INVALID_PARAM;
    EventDispatcher::Instance().SetCallback(callback);
    return HZCYKJTHardWare_RET_OK;
}

// ============================================================================
// Exported functions. Keep SEH wrappers outside C++ object lifetimes.
// ============================================================================

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_InitSdk(void) {
    __try { return InitSdkBody() == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) {
        OutputDebugStringA("=== HZCYKJTHardWare_InitSdk CRASH ===");
        return 0;
    }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_ReleaseSdk(void) {
    __try { return ReleaseSdkBody() == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_SwitchTerminal(int terminalIndex) {
    __try { return SwitchTerminalBody(terminalIndex) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartProcess(const char* saveDir) {
    __try { return StartProcessBody(saveDir) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_EndProcess(void) {
    __try { return EndProcessBody() == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartCameraPreview(void* hwnd) {
    __try { return StartCameraPreviewBody(hwnd) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopCameraPreview(void) {
    __try { return StopCameraPreviewBody() == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartFingerprintPreview(void* hwnd) {
    __try { return StartFingerprintPreviewBody(hwnd) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopFingerprintPreview(void) {
    __try { return StopFingerprintPreviewBody() == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartIrisPreview(void* hwnd) {
    __try { return StartIrisPreviewBody(hwnd) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopIrisPreview(void) {
    __try { return StopIrisPreviewBody() == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreview(void* hwnd) {
    __try { return StartPlatePreviewBody(hwnd) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreview(void) {
    __try { return StopPlatePreviewBody() == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureCameraImage(const char* saveDir) {
    __try { return CaptureCameraImageBody(saveDir) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureFingerprintImage(const char* saveDir) {
    __try { return CaptureFingerprintImageBody(saveDir) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureIrisImage(const char* saveDir) {
    __try { return CaptureIrisImageBody(saveDir) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestOCR(const char* saveDir) {
    __try { return RequestOCRBody(saveDir) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestNfcCard(const char* saveDir) {
    __try { return RequestNfcCardBody(saveDir) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestAuthorize(
    const char* ZJHM, const char* ZJLB, const char* GJDQDM,
    const char* XM, const char* XB, const char* CSRQ, const char* KADM)
{
    __try { return RequestAuthorizeBody(ZJHM, ZJLB, GJDQDM, XM, XB, CSRQ, KADM) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

extern "C" __declspec(dllexport) int __stdcall HZCYKJTHardWare_RegisterEventCallback(
    THZCYKJTHardWareEventCallback callback)
{
    __try { return RegisterEventCallbackBody(callback) == HZCYKJTHardWare_RET_OK ? 1 : 0; }
    __except(EXCEPTION_EXECUTE_HANDLER) { return 0; }
}
