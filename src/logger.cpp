#include "pch.h"
#include "logger.h"
#include "json_helper.h"
#include "path_helper.h"
#include <algorithm>
#include <cctype>
#include <cstdarg>
#include <cstring>
#include <filesystem>
#include <io.h>
#include <share.h>
#include <vector>

namespace HZCYKJTHardWare {

namespace {

const char* LevelToStringValue(LogLevel level) {
    switch (level) {
        case LogLevel::Debug: return "调试";
        case LogLevel::Info: return "信息";
        case LogLevel::Warn: return "警告";
        case LogLevel::Error: return "错误";
        default: return "信息";
    }
}

int LevelToNumber(LogLevel level) {
    return static_cast<int>(level);
}

bool ContainsText(const std::string& text, const char* value) {
    return value && text.find(value) != std::string::npos;
}

struct OperationModuleEntry {
    const char* operation;
    const char* module;
};

// 导出边界已经提供了确定的 Operation。优先使用此表，避免依赖日志正文中的关键词推断模块。
// 表项必须与 HZCYKJTHardWare.def 保持一致；等价校验脚本会核对两者的完整集合。
const OperationModuleEntry kOperationModuleEntries[] = {
    {"HZCYKJTHardWare_InitSdk", "SDK生命周期"},
    {"HZCYKJTHardWare_ReleaseSdk", "SDK生命周期"},
    {"HZCYKJTHardWare_SwitchTerminal", "终端切换"},
    {"HZCYKJTHardWare_StartProcess", "流程控制"},
    {"HZCYKJTHardWare_EndProcess", "流程控制"},
    {"HZCYKJTHardWare_StartCameraPreview", "预览"},
    {"HZCYKJTHardWare_StopCameraPreview", "预览"},
    {"HZCYKJTHardWare_StartFingerprintPreview", "预览"},
    {"HZCYKJTHardWare_StopFingerprintPreview", "预览"},
    {"HZCYKJTHardWare_StartIrisPreview", "预览"},
    {"HZCYKJTHardWare_StopIrisPreview", "预览"},
    {"HZCYKJTHardWare_StartPlatePreviewCJ", "预览"},
    {"HZCYKJTHardWare_StopPlatePreviewCJ", "预览"},
    {"HZCYKJTHardWare_StartPlatePreviewRJ2", "预览"},
    {"HZCYKJTHardWare_StopPlatePreviewRJ2", "预览"},
    {"HZCYKJTHardWare_StartPlatePreviewRJ3", "预览"},
    {"HZCYKJTHardWare_StopPlatePreviewRJ3", "预览"},
    {"HZCYKJTHardWare_SaveLatestPlateFrame", "车牌抓帧"},
    {"HZCYKJTHardWare_CaptureCameraImage", "人脸抓拍"},
    {"HZCYKJTHardWare_CaptureFingerprintImage", "指纹抓拍"},
    {"HZCYKJTHardWare_CaptureIrisImage", "虹膜抓拍"},
    {"HZCYKJTHardWare_RequestOCR", "证件识别"},
    {"HZCYKJTHardWare_RequestNfcCard", "NFC读卡"},
    {"HZCYKJTHardWare_RequestAuthorize", "授权"},
    {"HZCYKJTHardWare_RegisterEventCallback", "终端回调"}
};

const char* ModuleFromOperation(const char* operation) {
    if (!operation || !*operation) return nullptr;
    for (const auto& entry : kOperationModuleEntries) {
        if (std::strcmp(entry.operation, operation) == 0)
            return entry.module;
    }
    return nullptr;
}

std::string ModuleFromMessage(const std::string& message) {
    if (ContainsText(message, "预览") || ContainsText(message, "preview") ||
        ContainsText(message, "MJPEG")) {
        return "预览";
    }
    if (ContainsText(message, "车牌帧") || ContainsText(message, "最新车牌")) {
        return "车牌抓帧";
    }
    if (ContainsText(message, "人脸抓拍") || ContainsText(message, "摄像头抓拍") ||
        ContainsText(message, "/capture/face") || ContainsText(message, "face_capture")) {
        return "人脸抓拍";
    }
    if (ContainsText(message, "指纹抓拍") || ContainsText(message, "/capture/fingerprint") ||
        ContainsText(message, "fingerprint_capture")) return "指纹抓拍";
    if (ContainsText(message, "虹膜抓拍") || ContainsText(message, "/capture/iris") ||
        ContainsText(message, "iris_capture")) return "虹膜抓拍";
    if (ContainsText(message, "OCR") || ContainsText(message, "/ocr") ||
        ContainsText(message, "证件")) {
        return "证件识别";
    }
    if (ContainsText(message, "NFC") || ContainsText(message, "/nfc") ||
        ContainsText(message, "IC卡")) {
        return "NFC读卡";
    }
    if (ContainsText(message, "授权") || ContainsText(message, "/authorize")) return "授权";
    if (ContainsText(message, "/ping") || ContainsText(message, "ping") ||
        ContainsText(message, "连通性检查")) {
        return "健康检查";
    }
    if (ContainsText(message, "终端切换") || ContainsText(message, "切换终端") ||
        ContainsText(message, "/terminal/switch")) {
        return "终端切换";
    }
    if (ContainsText(message, "流程") || ContainsText(message, "/process")) return "流程控制";
    if (ContainsText(message, "初始化DLL") || ContainsText(message, "释放SDK")) {
        return "SDK生命周期";
    }
    if (ContainsText(message, "回调")) return "终端回调";
    if (ContainsText(message, "自动启动") || ContainsText(message, "重启硬件") ||
        ContainsText(message, "进程")) {
        return "流程控制";
    }
    if (ContainsText(message, "硬件控制程序") || ContainsText(message, "通信服务")) {
        return "终端通信";
    }
    return "未识别接口";
}

std::string NormalizeModule(const char* module, const char* function,
                            const char* message) {
    const std::string name = module ? module : "";
    const std::string text = message ? message : "";

    if (name == "预览" || name == "预览请求" || name == "预览管理" ||
        name == "预览窗口" || name == "预览租约" || name == "PreviewMgr") {
        return "预览";
    }
    if (name == "授权") return "授权";
    if (name == "NFC" || name == "NFC读卡") return "NFC读卡";
    if (name == "配置管理") return "配置管理";
    if (name == "日志管理" || name == "日志") return "日志管理";
    if (name == "回调服务" || name == "服务监听" || name == "服务") {
        return "服务监听";
    }
    if (name == "TerminalMgr") {
        return ContainsText(text, "切换") ? "终端切换" : "终端通信";
    }
    if (name == "事件分发" || name == "终端回调") {
        const std::string operation = ModuleFromMessage(text);
        return operation == "未识别接口" ? "终端回调" : operation;
    }
    if (name == "HTTP请求" || name == "代理服务") {
        const std::string operation = ModuleFromMessage(text);
        if (operation != "未识别接口") return operation;
        return "终端通信";
    }
    if (name == "接口" || name.empty()) {
        const char* operationModule = ModuleFromOperation(function);
        if (operationModule) return operationModule;
        return ModuleFromMessage(text);
    }
    if (name == "能力检查") return "设备能力";
    if (name == "SDK" || name == "SDK生命周期") return "SDK生命周期";
    return name;
}

std::string SanitizeScalar(const std::string& value, size_t maxLength = 256) {
    if (value.empty()) return "";
    if (maxLength == 0) maxLength = 256;

    std::string result;
    result.reserve((std::min)(value.size(), maxLength) + 3);
    for (char ch : value) {
        const unsigned char byte = static_cast<unsigned char>(ch);
        result += (ch == '\r' || ch == '\n' || ch == '\t' || byte < 0x20)
            ? ' ' : ch;
        if (result.size() >= maxLength) {
            if (value.size() > maxLength) result += "...";
            break;
        }
    }
    return result;
}

std::string ToLowerAscii(std::string value) {
    for (char& ch : value) {
        ch = static_cast<char>(std::tolower(static_cast<unsigned char>(ch)));
    }
    return value;
}

std::string RateLimitCategory(const std::string& key) {
    const std::string lower = ToLowerAscii(key);
    if (ContainsText(lower, "mjpeg") &&
        (ContainsText(lower, "render") || ContainsText(lower, "target"))) {
        return "MJPEG绘制失败";
    }
    if (ContainsText(lower, "mjpeg") && ContainsText(lower, "decode")) {
        return "MJPEG解码失败";
    }
    if (ContainsText(lower, "mjpeg")) return "MJPEG流故障";
    if (ContainsText(lower, "callback")) return "回调投递失败";
    if (ContainsText(lower, "ping") || ContainsText(lower, "connect") ||
        ContainsText(lower, "network") || ContainsText(lower, "timeout") ||
        ContainsText(lower, "12029")) {
        return "连接失败";
    }
    if (ContainsText(lower, "preview")) return "预览失败";
    if (ContainsText(lower, "queue")) return "任务队列失败";
    return "重复故障";
}

bool IsSensitivePayloadKey(const std::string& key) {
    const std::string lower = ToLowerAscii(key);
    return lower == "image_base64" || lower == "imagedata" ||
        lower == "image_data" || lower == "undistorted_image_base64" ||
        lower == "raw_json" || lower == "raw_body" || lower == "body" ||
        lower == "binary" || lower == "frame" || lower == "video" ||
        lower.find("password") != std::string::npos ||
        lower.find("passwd") != std::string::npos ||
        lower.find("token") != std::string::npos ||
        lower.find("secret") != std::string::npos ||
        lower.find("credential") != std::string::npos;
}

bool LooksLikeJsonObject(const std::string& value) {
    size_t first = 0;
    while (first < value.size() &&
           (value[first] == ' ' || value[first] == '\t' ||
            value[first] == '\r' || value[first] == '\n')) {
        ++first;
    }
    size_t last = value.size();
    while (last > first &&
           (value[last - 1] == ' ' || value[last - 1] == '\t' ||
            value[last - 1] == '\r' || value[last - 1] == '\n')) {
        --last;
    }
    if (last <= first || value[first] != '{' || value[last - 1] != '}') {
        return false;
    }

    int depth = 0;
    bool inString = false;
    bool escaped = false;
    for (size_t i = first; i < last; ++i) {
        const char ch = value[i];
        if (inString) {
            if (escaped) {
                escaped = false;
            } else if (ch == '\\') {
                escaped = true;
            } else if (ch == '"') {
                inString = false;
            }
            continue;
        }
        if (ch == '"') {
            inString = true;
        } else if (ch == '{') {
            ++depth;
        } else if (ch == '}') {
            if (--depth < 0) return false;
        }
    }
    return !inString && !escaped && depth == 0;
}

void WriteUtf8BomIfEmpty(FILE* file) {
    if (!file) return;

    const int descriptor = _fileno(file);
    if (descriptor < 0 || _filelengthi64(descriptor) != 0) return;

    static const unsigned char utf8Bom[] = {0xEF, 0xBB, 0xBF};
    fwrite(utf8Bom, 1, sizeof(utf8Bom), file);
}

std::string FormatTimestamp() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char timeBuf[32] = {0};
    snprintf(timeBuf, sizeof(timeBuf),
             "%04d-%02d-%02d %02d:%02d:%02d.%03d",
             st.wYear, st.wMonth, st.wDay,
             st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    return timeBuf;
}

std::string FormatV(const char* fmt, va_list args) {
    if (!fmt) return "";

    va_list probe;
    va_copy(probe, args);
    const int length = _vscprintf(fmt, probe);
    va_end(probe);
    if (length <= 0) return length == 0 ? "" : "[日志格式化失败]";

    std::vector<char> buffer(static_cast<size_t>(length) + 1, '\0');
    va_list copy;
    va_copy(copy, args);
    vsnprintf(buffer.data(), buffer.size(), fmt, copy);
    va_end(copy);
    return std::string(buffer.data(), static_cast<size_t>(length));
}

void AppendContextField(std::string& output, const char* name,
                        const std::string& value) {
    if (value.empty()) return;
    if (!output.empty()) output += ' ';
    output += name;
    output += '=';
    output += SanitizeScalar(value);
}

void AppendJsonScalar(std::string& output, const std::string& json,
                      const char* key) {
    if (!JsonHelper::HasKey(json, key) || IsSensitivePayloadKey(key)) {
        return;
    }
    const std::string value = JsonHelper::GetString(json, key);
    if (value.empty()) return;
    if (!output.empty()) output += ' ';
    output += key;
    output += '=';
    output += SanitizeScalar(value);
}

} // 匿名命名空间结束

std::string CanonicalOperationName(const std::string& operation) {
    if (operation.empty()) return operation;

    std::string value = operation;
    const std::string exportPrefix = "HZCYKJTHardWare_";
    if (value.compare(0, exportPrefix.size(), exportPrefix) == 0) {
        value.erase(0, exportPrefix.size());
    }

    if (value == "CaptureCameraImage") return "CaptureFace";
    if (value == "CaptureFingerprintImage") return "CaptureFingerprint";
    if (value == "CaptureIrisImage") return "CaptureIris";
    if (value == "RequestAuthorize") return "Authorize";
    return value;
}

static std::string CanonicalResultName(const std::string& result) {
    if (result == "成功") return "Success";
    if (result == "失败") return "Failed";
    if (result == "已受理") return "Accepted";
    if (result == "已恢复") return "Recovered";
    if (result == "已停止") return "Stopped";
    if (result == "忽略" || result == "已忽略") return "Ignored";
    if (result == "已发送") return "Delivered";
    if (result == "已取消") return "Cancelled";
    if (result == "重试") return "Retrying";
    if (result == "收到") return "Received";
    if (result == "开始") return "Started";
    return result;
}

std::string FormatLogContext(const LogContext& context) {
    std::string result;
    AppendContextField(result, "Operation", CanonicalOperationName(context.operation));
    AppendContextField(result, "TerminalIndex", context.terminalIndex);
    AppendContextField(result, "Device", context.device);
    AppendContextField(result, "RequestId", context.requestId);
    AppendContextField(result, "Result", CanonicalResultName(context.result));
    AppendContextField(result, "ErrorCode", context.errorCode);
    if (context.durationMs >= 0) {
        AppendContextField(result, "DurationMs", std::to_string(context.durationMs));
    }
    if (context.queueWaitMs >= 0) {
        AppendContextField(result, "QueueWaitMs", std::to_string(context.queueWaitMs));
    }
    if (context.attempt >= 0) {
        AppendContextField(result, "Attempt", std::to_string(context.attempt));
    }
    if (context.routeEpoch >= 0) {
        AppendContextField(result, "RouteEpoch", std::to_string(context.routeEpoch));
    }
    return result;
}

std::string SanitizeLargePayloadForLog(const std::string& payload,
                                       const std::string& requestId) {
    const std::string externalRequestId = requestId.empty()
        ? (LooksLikeJsonObject(payload) ? JsonHelper::GetString(payload, "request_id") : "")
        : requestId;

    std::string result = "payload=<omitted chars=" +
        std::to_string(payload.size()) + " estimated_bytes=" +
        std::to_string(payload.size()) + ">";
    if (!externalRequestId.empty()) {
        result += " RequestId=" + SanitizeScalar(externalRequestId);
    }

    if (!LooksLikeJsonObject(payload)) {
        return result;
    }

    static const char* safeKeys[] = {
        "status", "error_code", "code", "accepted", "result",
        "resource_type", "message", "save_path", "mrz", "card_text"
    };
    for (const char* key : safeKeys) {
        AppendJsonScalar(result, payload, key);
    }
    return result;
}

std::string SanitizeUrlForLog(const std::string& url) {
    if (url.empty()) return "";

    std::string result = url;
    const size_t schemeEnd = result.find("://");
    const size_t authorityStart = schemeEnd == std::string::npos
        ? 0 : schemeEnd + 3;
    if (schemeEnd != std::string::npos) {
        const size_t authorityEnd = result.find_first_of("/?#", authorityStart);
        const size_t authorityLimit = authorityEnd == std::string::npos
            ? result.size() : authorityEnd;
        const size_t at = result.rfind('@', authorityLimit);
        if (at != std::string::npos && at > authorityStart) {
            result.replace(authorityStart, at - authorityStart, "***:***");
        }
    }

    const size_t queryStart = result.find('?', authorityStart);
    if (queryStart != std::string::npos) {
        const size_t fragmentStart = result.find('#', queryStart + 1);
        const size_t queryEnd = fragmentStart == std::string::npos
            ? result.size() : fragmentStart;
        const std::string query = result.substr(
            queryStart + 1, queryEnd - queryStart - 1);
        std::string sanitizedQuery;
        size_t segmentStart = 0;
        while (segmentStart <= query.size()) {
            size_t segmentEnd = query.find('&', segmentStart);
            if (segmentEnd == std::string::npos) {
                segmentEnd = query.size();
            }
            std::string segment = query.substr(segmentStart,
                                               segmentEnd - segmentStart);
            const size_t equal = segment.find('=');
            if (equal != std::string::npos) {
                const std::string key = ToLowerAscii(segment.substr(0, equal));
                if (key.find("password") != std::string::npos ||
                    key.find("passwd") != std::string::npos ||
                    key.find("token") != std::string::npos ||
                    key.find("secret") != std::string::npos ||
                    key.find("credential") != std::string::npos ||
                    key == "key" || key == "auth") {
                    segment = segment.substr(0, equal + 1) + "***";
                }
            }
            if (!sanitizedQuery.empty()) sanitizedQuery += '&';
            sanitizedQuery += segment;
            if (segmentEnd == query.size()) break;
            segmentStart = segmentEnd + 1;
        }
        result.replace(queryStart + 1, queryEnd - queryStart - 1,
                       sanitizedQuery);
    }
    return SanitizeScalar(result, 512);
}

Logger::Logger() {
    InitializeCriticalSection(&m_cs);
}

Logger::~Logger() {
    Shutdown();
    DeleteCriticalSection(&m_cs);
}

Logger& Logger::Instance() {
    static Logger instance;
    return instance;
}

const char* Logger::LevelToString(LogLevel level) const {
    return LevelToStringValue(level);
}

std::string Logger::GetLogFilePath(const std::string& date, int rollIndex) const {
    std::string trimmedDir = m_logDir;
    while (!trimmedDir.empty() &&
           (trimmedDir.back() == '\\' || trimmedDir.back() == '/')) {
        trimmedDir.pop_back();
    }
    std::string dirName = PathHelper::GetFileName(trimmedDir);
    if (dirName.empty()) dirName = "HZCYKJTHardWareDLL_Logs";

    std::string fileName = dirName + "_" + date;
    if (rollIndex > 0) {
        char suffix[16] = {0};
        snprintf(suffix, sizeof(suffix), "_%03d", rollIndex);
        fileName += suffix;
    }
    return PathHelper::Join(m_logDir, fileName + ".log");
}

bool Logger::OpenLogFileLocked(const std::string& date, int preferredRollIndex) {
    if (date.empty()) return false;
    if (m_file && m_currentLogDate == date && preferredRollIndex < 0) {
        return true;
    }

    CloseLogFileLocked();
    m_currentLogDate = date;
    m_rollIndex = preferredRollIndex < 0 ? 0 : preferredRollIndex;

    namespace fs = std::filesystem;
    std::error_code ec;
    const fs::path directory(PathHelper::Utf8ToWide(m_logDir));
    if (!fs::is_directory(directory, ec)) {
        ec.clear();
        CreateDirectoryW(directory.wstring().c_str(), nullptr);
    }

    int index = m_rollIndex;
    if (preferredRollIndex < 0) {
        index = 0;
        const fs::path basePath(PathHelper::Utf8ToWide(GetLogFilePath(date, 0)));
        if (fs::exists(basePath, ec) &&
            fs::file_size(basePath, ec) >= m_maxFileSizeBytes) {
            ec.clear();
            index = 1;
        }
        ec.clear();
    }

    std::string logPath;
    for (int attempts = 0; attempts < 10000; ++attempts, ++index) {
        logPath = GetLogFilePath(date, index);
        const fs::path candidate(PathHelper::Utf8ToWide(logPath));
        if (!fs::exists(candidate, ec)) {
            ec.clear();
            break;
        }
        const uintmax_t size = fs::file_size(candidate, ec);
        ec.clear();
        if (size < m_maxFileSizeBytes) break;
    }

    const std::wstring wLogPath = PathHelper::Utf8ToWide(logPath);
    m_file = _wfsopen(wLogPath.c_str(), L"a", _SH_DENYNO);
    if (!m_file) {
        m_currentLogPath.clear();
        m_currentFileSize = 0;
        return false;
    }

    setvbuf(m_file, nullptr, _IOFBF, 64 * 1024);
    WriteUtf8BomIfEmpty(m_file);
    m_currentLogPath = logPath;
    m_rollIndex = index;
    const int descriptor = _fileno(m_file);
    const __int64 fileLength = descriptor >= 0 ? _filelengthi64(descriptor) : 0;
    m_currentFileSize = fileLength > 0 ? static_cast<uint64_t>(fileLength) : 0;
    m_currentFileLength.store(m_currentFileSize);
    m_pendingLines = 0;
    m_lastFlushTick = GetTickCount64();
    return true;
}

bool Logger::RotateLogFileLocked(const std::string& date) {
    const int nextIndex = m_rollIndex + 1;
    if (m_file) {
        FlushLocked();
        fclose(m_file);
        m_file = nullptr;
    }
    m_currentLogPath.clear();
    m_currentLogDate.clear();
    return OpenLogFileLocked(date, nextIndex);
}

void Logger::CloseLogFileLocked() {
    if (m_file) {
        FlushLocked();
        fclose(m_file);
        m_file = nullptr;
    }
    m_currentLogPath.clear();
    m_currentLogDate.clear();
    m_rollIndex = 0;
    m_currentFileSize = 0;
    m_currentFileLength.store(0);
}

bool Logger::Init(const std::string& logDir) {
    Shutdown();

    EnterCriticalSection(&m_cs);
    m_logDir = logDir.empty() ? "HZCYKJTHardWareDLL_Logs" : logDir;
    m_lastCleanupDate.clear();
    m_rateLimitBuckets.clear();
    CleanupOldLogsLocked(true);

    const bool fileReady = OpenLogFileLocked(PathHelper::GetDateString());
    CheckDiskSpaceLocked();
    if (!fileReady) {
        const std::string line = "[" + FormatTimestamp() +
            "] [日志管理][错误] 日志目录不可写，已切换应急输出\n";
        WriteEmergencyLine(line, "log directory unavailable");
        OutputDebugStringW(PathHelper::Utf8ToWide(line).c_str());
    }
    LeaveCriticalSection(&m_cs);

    try {
        {
            std::lock_guard<std::mutex> lock(m_queueMutex);
            m_accepting = true;
            m_stopRequested = false;
        }
        m_worker = std::thread(&Logger::WorkerLoop, this);
    } catch (...) {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        m_accepting = false;
        m_stopRequested = true;
        const std::string line = "[" + FormatTimestamp() +
            "] [日志管理][错误] 异步日志线程启动失败\n";
        WriteEmergencyLine(line, "worker thread start failed");
        return fileReady;
    }
    m_queueCondition.notify_all();
    return fileReady;
}

void Logger::Shutdown() {
    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        m_accepting = false;
        m_stopRequested = true;
    }
    m_queueCondition.notify_all();

    if (m_worker.joinable() &&
        std::this_thread::get_id() != m_worker.get_id()) {
        m_worker.join();
    }

    EnterCriticalSection(&m_cs);
    CloseLogFileLocked();
    m_pendingLines = 0;
    LeaveCriticalSection(&m_cs);
}

void Logger::ConfigureRetention(int retentionDays, int maxTotalSizeMb,
                                int diskWarningFreeMb, int flushIntervalMs,
                                int flushBatchSize) {
    EnterCriticalSection(&m_cs);
    m_retentionDays = std::clamp(retentionDays, 1, 3650);
    m_maxTotalSizeBytes = static_cast<uint64_t>(
        std::clamp(maxTotalSizeMb, 16, 102400)) * 1024ULL * 1024ULL;
    m_diskWarningFreeBytes = static_cast<uint64_t>(
        std::clamp(diskWarningFreeMb, 0, 102400)) * 1024ULL * 1024ULL;
    m_flushIntervalMs = std::clamp(flushIntervalMs, 50, 10000);
    m_flushBatchSize = std::clamp(flushBatchSize, 1, 10000);
    m_lastCleanupDate.clear();
    CleanupOldLogsLocked(true);
    CheckDiskSpaceLocked();
    LeaveCriticalSection(&m_cs);
}

void Logger::CleanupOldLogsLocked(bool force) {
    namespace fs = std::filesystem;
    if (m_logDir.empty()) return;

    const std::string today = PathHelper::GetDateString();
    if (!force && today == m_lastCleanupDate) return;
    m_lastCleanupDate = today;

    std::error_code ec;
    const fs::path directory(PathHelper::Utf8ToWide(m_logDir));
    if (!fs::is_directory(directory, ec)) return;

    struct LogFileInfo {
        fs::path path;
        fs::file_time_type writeTime;
        uint64_t size = 0;
        bool current = false;
    };

    std::string trimmedDir = m_logDir;
    while (!trimmedDir.empty() &&
           (trimmedDir.back() == '\\' || trimmedDir.back() == '/')) {
        trimmedDir.pop_back();
    }
    std::string dirName = PathHelper::GetFileName(trimmedDir);
    if (dirName.empty()) dirName = "HZCYKJTHardWareDLL_Logs";
    const std::wstring filePrefix = PathHelper::Utf8ToWide(dirName + "_");
    const fs::path currentPath = m_currentLogPath.empty()
        ? fs::path() : fs::path(PathHelper::Utf8ToWide(m_currentLogPath));

    auto CollectFiles = [&]() {
        std::vector<LogFileInfo> files;
        fs::directory_iterator end;
        for (fs::directory_iterator it(directory, ec); !ec && it != end;
             it.increment(ec)) {
            const auto& entry = *it;
            const std::wstring fileName = entry.path().filename().wstring();
            if (!entry.is_regular_file(ec) ||
                entry.path().extension() != L".log" ||
                fileName.rfind(filePrefix, 0) != 0) {
                ec.clear();
                continue;
            }

            const auto writeTime = entry.last_write_time(ec);
            if (ec) {
                ec.clear();
                continue;
            }
            const auto size = entry.file_size(ec);
            if (ec) {
                ec.clear();
                continue;
            }
            const bool current = !m_currentLogPath.empty() &&
                entry.path().lexically_normal() == currentPath.lexically_normal();
            files.push_back({entry.path(), writeTime,
                             static_cast<uint64_t>(size), current});
        }
        return files;
    };

    auto files = CollectFiles();
    std::sort(files.begin(), files.end(),
        [](const LogFileInfo& left, const LogFileInfo& right) {
            return left.writeTime < right.writeTime;
        });

    const auto now = fs::file_time_type::clock::now();
    const auto keepDuration = std::chrono::hours(
        static_cast<long long>(m_retentionDays) * 24LL);
    for (const auto& file : files) {
        if (file.current || now - file.writeTime <= keepDuration) continue;
        fs::remove(file.path, ec);
        ec.clear();
    }

    files = CollectFiles();
    std::sort(files.begin(), files.end(),
        [](const LogFileInfo& left, const LogFileInfo& right) {
            return left.writeTime < right.writeTime;
        });

    uint64_t totalSize = 0;
    for (const auto& file : files) totalSize += file.size;
    for (const auto& file : files) {
        if (totalSize <= m_maxTotalSizeBytes) break;
        if (file.current) continue;
        if (fs::remove(file.path, ec)) {
            totalSize = file.size <= totalSize ? totalSize - file.size : 0;
        }
        ec.clear();
    }
}

void Logger::CheckDiskSpaceLocked() {
    namespace fs = std::filesystem;
    if (m_logDir.empty() || m_diskWarningFreeBytes == 0) {
        m_lowDiskMode.store(false);
        return;
    }

    std::error_code ec;
    const auto info = fs::space(
        fs::path(PathHelper::Utf8ToWide(m_logDir)), ec);
    if (ec) return;

    const bool lowDisk = info.available < m_diskWarningFreeBytes;
    m_lowDiskMode.store(lowDisk);
    if (!lowDisk) return;

    const std::string warning =
        "[" + FormatTimestamp() + "] [日志管理][警告] 日志盘剩余空间不足："
        "可用空间=" + std::to_string(info.available / 1024ULL / 1024ULL) +
        "MB，预警阈值=" +
        std::to_string(m_diskWarningFreeBytes / 1024ULL / 1024ULL) + "MB\n";
    if (m_file) {
        if (fputs(warning.c_str(), m_file) == EOF) {
            RecordWriterFailure("low disk warning write failed");
        } else {
            m_currentFileSize += warning.size();
            m_currentFileLength.store(m_currentFileSize);
            ++m_pendingLines;
            FlushLocked();
        }
    } else {
        WriteEmergencyLine(warning, "low disk");
    }
    OutputDebugStringW(PathHelper::Utf8ToWide(warning).c_str());
}

bool Logger::FlushLocked() {
    if (!m_file) return true;
    if (fflush(m_file) != 0) {
        RecordWriterFailure("fflush failed");
        return false;
    }
    m_pendingLines = 0;
    m_lastFlushTick = GetTickCount64();
    const int descriptor = _fileno(m_file);
    const __int64 length = descriptor >= 0 ? _filelengthi64(descriptor) : -1;
    if (length >= 0) {
        m_currentFileSize = static_cast<uint64_t>(length);
        m_currentFileLength.store(m_currentFileSize);
    }
    m_lastSuccessfulFlushTick.store(m_lastFlushTick);
    return true;
}

bool Logger::WriteLineLocked(const std::string& line, LogLevel level) {
    if (!m_file) return false;
    const uint64_t lineBytes = static_cast<uint64_t>(line.size());
    if (m_maxFileSizeBytes > 0 &&
        m_currentFileSize > 3 &&
        m_currentFileSize + lineBytes > m_maxFileSizeBytes) {
        if (!RotateLogFileLocked(m_currentLogDate)) {
            return false;
        }
    }
    if (fputs(line.c_str(), m_file) == EOF) {
        return false;
    }
    m_currentFileSize += lineBytes;
    m_currentFileLength.store(m_currentFileSize);
    ++m_pendingLines;
    (void)level;
    return true;
}

void Logger::WriteEntry(const LogEntry& entry) {
    if (entry.flush) {
        bool completed = false;
        EnterCriticalSection(&m_cs);
        completed = FlushLocked();
        LeaveCriticalSection(&m_cs);
        {
            std::lock_guard<std::mutex> lock(entry.flush->mutex);
            entry.flush->completed = completed;
        }
        entry.flush->condition.notify_all();
        return;
    }

    EnterCriticalSection(&m_cs);
    bool written = false;
    if (OpenLogFileLocked(entry.date)) {
        const uint64_t dropped = m_droppedSinceNotice.exchange(0);
        if (dropped > 0) {
            const std::string warning =
                "[" + FormatTimestamp() + "] [日志管理][警告] 日志队列已满，"
                "已丢弃 " + std::to_string(dropped) + " 条普通日志\n";
            if (!WriteLineLocked(warning, LogLevel::Warn)) {
                RecordWriterFailure("dropped-log warning write failed");
            }
        }

        written = WriteLineLocked(entry.line, entry.level);
        if (!written) {
            RecordWriterFailure("log line write failed");
        } else {
            const ULONGLONG elapsed = GetTickCount64() - m_lastFlushTick;
            if (entry.level == LogLevel::Error ||
                m_pendingLines >= m_flushBatchSize ||
                elapsed >= static_cast<ULONGLONG>(m_flushIntervalMs)) {
                FlushLocked();
            }
        }
    } else {
        RecordWriterFailure("log file open failed");
    }
    LeaveCriticalSection(&m_cs);

    if (!written && entry.level == LogLevel::Error) {
        WriteEmergencyLine(entry.line, "error log main file unavailable");
    }
}

void Logger::WorkerLoop() {
    for (;;) {
        LogEntry entry;
        bool hasEntry = false;
        {
            std::unique_lock<std::mutex> lock(m_queueMutex);
            m_queueCondition.wait(lock, [this]() {
                return m_stopRequested ||
                    !m_errorQueue.empty() || !m_normalQueue.empty();
            });

            if (!m_errorQueue.empty()) {
                entry = std::move(m_errorQueue.front());
                m_errorQueue.pop_front();
                hasEntry = true;
            } else if (!m_normalQueue.empty()) {
                entry = std::move(m_normalQueue.front());
                m_normalQueue.pop_front();
                hasEntry = true;
            } else if (m_stopRequested) {
                break;
            }
        }

        if (!hasEntry) continue;
        try {
            WriteEntry(entry);
        } catch (...) {
            RecordWriterFailure("unhandled logger worker exception");
            if (entry.flush) {
                std::lock_guard<std::mutex> lock(entry.flush->mutex);
                entry.flush->completed = false;
                entry.flush->condition.notify_all();
            } else if (entry.level == LogLevel::Error) {
                WriteEmergencyLine(entry.line, "logger worker exception");
            }
        }
    }
}

bool Logger::Enqueue(LogEntry entry) {
    const bool isError = entry.level == LogLevel::Error;
    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        if (!m_accepting) return false;

        const size_t total = m_normalQueue.size() + m_errorQueue.size();
        if (isError) {
            if (m_errorQueue.size() >= kReservedErrorQueueLength) return false;
            m_errorQueue.push_back(std::move(entry));
        } else {
            if (total >= kMaxQueueLength - kReservedErrorQueueLength) {
                return false;
            }
            m_normalQueue.push_back(std::move(entry));
        }
    }
    m_queueCondition.notify_one();
    return true;
}

bool Logger::WriteEmergencyLine(const std::string& line, const char* reason) {
    std::wstring tempPath;
    wchar_t buffer[MAX_PATH] = {0};
    const DWORD length = GetTempPathW(MAX_PATH, buffer);
    if (length > 0 && length < MAX_PATH) {
        tempPath.assign(buffer, length);
    }
    if (tempPath.empty()) {
        tempPath = PathHelper::Utf8ToWide(m_logDir);
        if (!tempPath.empty() && tempPath.back() != L'\\') tempPath += L'\\';
    }
    if (tempPath.empty()) {
        OutputDebugStringW(PathHelper::Utf8ToWide(line).c_str());
        return false;
    }

    const std::string pathUtf8 = PathHelper::WideToUtf8(tempPath);
    const std::string fileName = "HZCYKJTHardWareDLL_Emergency_" +
        PathHelper::GetDateString() + ".log";
    const std::wstring path = PathHelper::Utf8ToWide(
        PathHelper::Join(pathUtf8, fileName));
    FILE* file = _wfsopen(path.c_str(), L"a", _SH_DENYNO);
    if (!file) {
        OutputDebugStringW(PathHelper::Utf8ToWide(line).c_str());
        return false;
    }

    WriteUtf8BomIfEmpty(file);
    std::string emergencyLine;
    if (reason && reason[0]) {
        emergencyLine = "[" + FormatTimestamp() +
            "] [日志管理][错误] 应急写入原因=" +
            SanitizeScalar(reason) + "\n";
    }
    emergencyLine += line;
    const bool ok = fputs(emergencyLine.c_str(), file) != EOF &&
        fflush(file) == 0;
    fclose(file);
    if (!ok) {
        OutputDebugStringW(PathHelper::Utf8ToWide(line).c_str());
    }
    return ok;
}

void Logger::RecordWriterFailure(const char* reason) {
    const std::string message = reason && reason[0] ? reason : "unknown";
    m_writeFailureCount.fetch_add(1);
    std::string windowSummary;
    bool emitCurrent = false;
    {
        std::lock_guard<std::mutex> lock(m_failureMutex);
        m_lastWriteError = message;
        const ULONGLONG now = GetTickCount64();
        const std::string nowText = FormatTimestamp();
        if (m_writerFailureBucket.windowStart == 0 ||
            now - m_writerFailureBucket.windowStart >= kRateLimitWindowMs) {
            if (m_writerFailureBucket.count > 0) {
                windowSummary = "重复故障汇总：类别=日志写入失败" +
                    std::string("，次数=") +
                    std::to_string(m_writerFailureBucket.count) +
                    "，首次=" + m_writerFailureBucket.firstTime +
                    "，最近=" + m_writerFailureBucket.lastTime +
                    "，最近错误=" + SanitizeScalar(m_writerFailureBucket.lastError);
            }
            m_writerFailureBucket.windowStart = now;
            m_writerFailureBucket.lastSeen = now;
            m_writerFailureBucket.count = 1;
            m_writerFailureBucket.firstTime = nowText;
            m_writerFailureBucket.lastTime = nowText;
            m_writerFailureBucket.lastError = message;
            emitCurrent = true;
        } else {
            ++m_writerFailureBucket.count;
            m_writerFailureBucket.lastSeen = now;
            m_writerFailureBucket.lastTime = nowText;
            m_writerFailureBucket.lastError = message;
        }
    }

    const std::string debugLine = "[" + FormatTimestamp() +
        "] [日志管理][错误] " + message + "\n";
    OutputDebugStringW(PathHelper::Utf8ToWide(debugLine).c_str());

    if (!windowSummary.empty() && emitCurrent) {
        WriteEmergencyLine("[" + FormatTimestamp() +
            "] [日志管理][错误] " + windowSummary +
            "，本次错误=" + SanitizeScalar(message) + "\n",
            "writer failure window");
    } else if (emitCurrent) {
        WriteEmergencyLine("[" + FormatTimestamp() +
            "] [日志管理][错误] 日志主写入器失败：原因=" +
            SanitizeScalar(message) + "\n", "writer failure");
    }
}

bool Logger::IsLevelEnabled(LogLevel level) const {
    if (level == LogLevel::Debug && m_lowDiskMode.load()) return false;
    return LevelToNumber(level) >= m_minLevel.load();
}

bool Logger::CheckRateLimitLocked(const std::string& key,
                                  const std::string& message,
                                  std::string& windowSummary) {
    const ULONGLONG now = GetTickCount64();
    const std::string normalizedKey = key.empty() ? "unknown" : SanitizeScalar(key);
    const std::string nowText = FormatTimestamp();
    auto& bucket = m_rateLimitBuckets[normalizedKey];
    if (bucket.windowStart == 0 ||
        now - bucket.windowStart >= kRateLimitWindowMs) {
        if (bucket.count > 0) {
            windowSummary = "重复故障汇总：类别=" + RateLimitCategory(normalizedKey) +
                "，次数=" + std::to_string(bucket.count) +
                "，首次=" + bucket.firstTime +
                "，最近=" + bucket.lastTime +
                "，最近错误=" + SanitizeScalar(bucket.lastError);
        }
        bucket.windowStart = now;
        bucket.lastSeen = now;
        bucket.count = 1;
        bucket.firstTime = nowText;
        bucket.lastTime = nowText;
        bucket.lastError = message;
        if (m_rateLimitBuckets.size() > 1024) {
            auto oldest = m_rateLimitBuckets.begin();
            for (auto it = m_rateLimitBuckets.begin();
                 it != m_rateLimitBuckets.end(); ++it) {
                if (it->second.lastSeen < oldest->second.lastSeen) oldest = it;
            }
            if (oldest != m_rateLimitBuckets.end() &&
                oldest->first != normalizedKey) {
                m_rateLimitBuckets.erase(oldest);
            }
        }
        return true;
    }

    ++bucket.count;
    bucket.lastSeen = now;
    bucket.lastTime = nowText;
    bucket.lastError = message;
    return false;
}

void Logger::Log(LogLevel level, const char* module, const char* function,
                 const char* fmt, ...) {
    if (!IsLevelEnabled(level)) return;

    va_list args;
    va_start(args, fmt);
    const std::string message = FormatV(fmt, args);
    va_end(args);

    const std::string timestamp = FormatTimestamp();
    const std::string normalizedModule = NormalizeModule(
        module, function, message.c_str());
    const std::string line = "[" + timestamp + "] [" + normalizedModule +
        "][" + LevelToString(level) + "] " + message + "\n";

    LogEntry entry;
    entry.level = level;
    entry.date = PathHelper::GetDateString();
    entry.line = line;
    if (!Enqueue(std::move(entry))) {
        if (level == LogLevel::Error) {
            if (!WriteEmergencyLine(line, "error queue full or logger stopping")) {
                m_totalDroppedCount.fetch_add(1);
            }
        } else {
            m_totalDroppedCount.fetch_add(1);
            m_droppedSinceNotice.fetch_add(1);
        }
    }
    OutputDebugStringW(PathHelper::Utf8ToWide(line).c_str());
}

void Logger::LogRateLimited(LogLevel level, const char* module,
                            const char* function, const char* rateKey,
                            const char* fmt, ...) {
    if (!IsLevelEnabled(level)) return;

    va_list args;
    va_start(args, fmt);
    const std::string message = FormatV(fmt, args);
    va_end(args);

    std::string windowSummary;
    bool emitCurrent = false;
    EnterCriticalSection(&m_cs);
    emitCurrent = CheckRateLimitLocked(rateKey ? rateKey : "", message,
                                       windowSummary);
    LeaveCriticalSection(&m_cs);
    if (!emitCurrent) return;

    if (!windowSummary.empty()) {
        const std::string boundaryMessage = windowSummary +
            "，本次错误=" + SanitizeScalar(message);
        Log(level, module, function, "%s", boundaryMessage.c_str());
        return;
    }
    Log(level, module, function, "%s", message.c_str());
}

void Logger::Debug(const char* module, const char* function,
                   const char* fmt, ...) {
    if (!IsLevelEnabled(LogLevel::Debug)) return;
    va_list args;
    va_start(args, fmt);
    const std::string message = FormatV(fmt, args);
    va_end(args);
    Log(LogLevel::Debug, module, function, "%s", message.c_str());
}

void Logger::Info(const char* module, const char* function,
                  const char* fmt, ...) {
    if (!IsLevelEnabled(LogLevel::Info)) return;
    va_list args;
    va_start(args, fmt);
    const std::string message = FormatV(fmt, args);
    va_end(args);
    Log(LogLevel::Info, module, function, "%s", message.c_str());
}

void Logger::Warn(const char* module, const char* function,
                  const char* fmt, ...) {
    if (!IsLevelEnabled(LogLevel::Warn)) return;
    va_list args;
    va_start(args, fmt);
    const std::string message = FormatV(fmt, args);
    va_end(args);
    Log(LogLevel::Warn, module, function, "%s", message.c_str());
}

void Logger::Error(const char* module, const char* function,
                   const char* fmt, ...) {
    if (!IsLevelEnabled(LogLevel::Error)) return;
    va_list args;
    va_start(args, fmt);
    const std::string message = FormatV(fmt, args);
    va_end(args);
    Log(LogLevel::Error, module, function, "%s", message.c_str());
}

void Logger::SetLevel(LogLevel level) {
    m_minLevel.store(LevelToNumber(level));
}

uint64_t Logger::PendingCount() const {
    std::lock_guard<std::mutex> lock(m_queueMutex);
    return static_cast<uint64_t>(m_normalQueue.size() + m_errorQueue.size());
}

uint64_t Logger::TotalDroppedCount() const {
    return m_totalDroppedCount.load();
}

uint64_t Logger::WriteFailureCount() const {
    return m_writeFailureCount.load();
}

uint64_t Logger::CurrentFileLength() const {
    return m_currentFileLength.load();
}

int64_t Logger::LastFlushAgeMs() const {
    const ULONGLONG lastFlush = m_lastSuccessfulFlushTick.load();
    if (lastFlush == 0) return -1;
    return static_cast<int64_t>(GetTickCount64() - lastFlush);
}

bool Logger::LowDiskMode() const {
    return m_lowDiskMode.load();
}

} // HZCYKJTHardWare 命名空间结束
