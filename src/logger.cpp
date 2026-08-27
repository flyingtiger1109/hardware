#include "pch.h"
#include "logger.h"
#include "path_helper.h"
#include <algorithm>
#include <filesystem>
#include <io.h>
#include <share.h>
#include <vector>

namespace HZCYKJTHardWare {

namespace {
const char* ShortFunctionName(const char* function) {
    if (!function || !function[0]) {
        return "";
    }

    const char* last = function;
    const char* scan = function;
    while ((scan = strstr(scan, "::")) != nullptr) {
        last = scan + 2;
        scan += 2;
    }
    return last;
}

bool ContainsText(const std::string& text, const char* value) {
    return value && text.find(value) != std::string::npos;
}

std::string ModuleFromMessage(const std::string& message) {
    if (ContainsText(message, "预览") || ContainsText(message, "preview") ||
        ContainsText(message, "MJPEG")) {
        return "预览";
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

std::string NormalizeModule(const char* module, const char* message) {
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
        return ModuleFromMessage(text);
    }
    if (name == "能力检查") return "设备能力";
    if (name == "SDK" || name == "SDK生命周期") return "SDK生命周期";
    return name;
}

void WriteUtf8BomIfEmpty(FILE* file) {
    if (!file) return;

    const int descriptor = _fileno(file);
    if (descriptor < 0 || _filelengthi64(descriptor) != 0) return;

    static const unsigned char utf8Bom[] = {0xEF, 0xBB, 0xBF};
    fwrite(utf8Bom, 1, sizeof(utf8Bom), file);
}
}

Logger::Logger() {
    InitializeCriticalSection(&m_cs);
}

Logger::~Logger() {
    DeleteCriticalSection(&m_cs);
}

Logger& Logger::Instance() {
    // 函数内静态对象保证并发首次访问安全，并在模块卸载时释放临界区。
    static Logger instance;
    return instance;
}

const char* Logger::LevelToString(LogLevel level) {
    switch (level) {
        case LogLevel::Debug: return "调试";
        case LogLevel::Info:  return "信息";
        case LogLevel::Warn:  return "警告";
        case LogLevel::Error: return "错误";
        default: return "信息";
    }
}

std::string Logger::GetLogFilePath() {
    std::string dateStr = PathHelper::GetDateString();
    std::string trimmedDir = m_logDir;
    while (!trimmedDir.empty() && (trimmedDir.back() == '\\' || trimmedDir.back() == '/')) {
        trimmedDir.pop_back();
    }
    std::string dirName = PathHelper::GetFileName(trimmedDir);
    if (dirName.empty()) {
        dirName = "HZCYKJTHardWareDLL_Logs";
    }
    return PathHelper::Join(m_logDir, dirName + "_" + dateStr + ".log");
}

bool Logger::Init(const std::string& logDir) {
    EnterCriticalSection(&m_cs);

    m_logDir = logDir;
    if (m_logDir.empty()) {
        m_logDir = "HZCYKJTHardWareDLL_Logs";
    }

    if (m_file) {
        fclose(m_file);
        m_file = nullptr;
    }
    m_currentLogPath.clear();

    std::wstring wLogDir = PathHelper::Utf8ToWide(m_logDir);
    CreateDirectoryW(wLogDir.c_str(), nullptr);
    m_lastCleanupDate.clear();
    CleanupOldLogsLocked();

    std::string logPath = GetLogFilePath();
    std::wstring wLogPath = PathHelper::Utf8ToWide(logPath);
    m_file = _wfsopen(wLogPath.c_str(), L"a", _SH_DENYNO);
    if (m_file) {
        setvbuf(m_file, nullptr, _IOFBF, 64 * 1024);
        WriteUtf8BomIfEmpty(m_file);
    }
    m_currentLogPath = logPath;
    m_pendingLines = 0;
    m_lastFlushTick = GetTickCount64();
    CheckDiskSpaceLocked();

    LeaveCriticalSection(&m_cs);
    return m_file != nullptr;
}

void Logger::Shutdown() {
    EnterCriticalSection(&m_cs);
    if (m_file) {
        FlushLocked();
        fclose(m_file);
        m_file = nullptr;
    }
    m_currentLogPath.clear();
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
    CleanupOldLogsLocked();
    CheckDiskSpaceLocked();
    LeaveCriticalSection(&m_cs);
}

void Logger::CleanupOldLogsLocked() {
    namespace fs = std::filesystem;
    if (m_logDir.empty()) return;

    const std::string today = PathHelper::GetDateString();
    if (today == m_lastCleanupDate) return;
    m_lastCleanupDate = today;

    std::error_code ec;
    const fs::path directory(PathHelper::Utf8ToWide(m_logDir));
    if (!fs::is_directory(directory, ec)) return;

    struct LogFileInfo {
        fs::path path;
        fs::file_time_type writeTime;
        uint64_t size = 0;
    };

    std::vector<LogFileInfo> files;
    const auto now = fs::file_time_type::clock::now();
    const auto keepDuration = std::chrono::hours(
        static_cast<long long>(m_retentionDays) * 24LL);
    const fs::path currentPath(PathHelper::Utf8ToWide(m_currentLogPath));
    std::string trimmedDir = m_logDir;
    while (!trimmedDir.empty() &&
           (trimmedDir.back() == '\\' || trimmedDir.back() == '/')) {
        trimmedDir.pop_back();
    }
    std::string dirName = PathHelper::GetFileName(trimmedDir);
    if (dirName.empty()) dirName = "HZCYKJTHardWareDLL_Logs";
    const std::wstring filePrefix = PathHelper::Utf8ToWide(dirName + "_");

    fs::directory_iterator end;
    for (fs::directory_iterator it(directory, ec); !ec && it != end;
         it.increment(ec)) {
        const auto& entry = *it;
        const std::wstring fileName = entry.path().filename().wstring();
        if (!entry.is_regular_file(ec) || entry.path().extension() != L".log" ||
            fileName.rfind(filePrefix, 0) != 0) {
            ec.clear();
            continue;
        }
        if (!m_currentLogPath.empty() && entry.path() == currentPath) {
            continue;
        }

        const auto writeTime = entry.last_write_time(ec);
        if (ec) {
            ec.clear();
            continue;
        }
        if (now - writeTime > keepDuration) {
            fs::remove(entry.path(), ec);
            ec.clear();
            continue;
        }

        const auto size = entry.file_size(ec);
        if (ec) {
            ec.clear();
            continue;
        }
        files.push_back({entry.path(), writeTime,
                         static_cast<uint64_t>(size)});
    }

    std::sort(files.begin(), files.end(),
        [](const LogFileInfo& left, const LogFileInfo& right) {
            return left.writeTime < right.writeTime;
        });

    uint64_t totalSize = 0;
    for (const auto& file : files) totalSize += file.size;
    for (const auto& file : files) {
        if (totalSize <= m_maxTotalSizeBytes) break;
        if (fs::remove(file.path, ec)) {
            totalSize = file.size <= totalSize ? totalSize - file.size : 0;
        }
        ec.clear();
    }
}

void Logger::CheckDiskSpaceLocked() {
    namespace fs = std::filesystem;
    if (m_logDir.empty() || m_diskWarningFreeBytes == 0) return;
    std::error_code ec;
    const auto info = fs::space(
        fs::path(PathHelper::Utf8ToWide(m_logDir)), ec);
    if (!ec && info.available < m_diskWarningFreeBytes) {
        char warning[256];
        snprintf(warning, sizeof(warning),
                 "[日志管理][警告] 日志盘剩余空间不足：可用空间=%lluMB，预警阈值=%lluMB\n",
                 static_cast<unsigned long long>(info.available / 1024ULL / 1024ULL),
                 static_cast<unsigned long long>(m_diskWarningFreeBytes / 1024ULL / 1024ULL));
        if (m_file) {
            fputs(warning, m_file);
            ++m_pendingLines;
            FlushLocked();
        }
        OutputDebugStringW(PathHelper::Utf8ToWide(warning).c_str());
    }
}

void Logger::FlushLocked() {
    if (m_file) fflush(m_file);
    m_pendingLines = 0;
    m_lastFlushTick = GetTickCount64();
}

void Logger::Log(LogLevel level, const char* module, const char* function, const char* fmt, ...) {
    if (level < m_level) return;

    char msgBuf[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf(msgBuf, sizeof(msgBuf), fmt, args);
    va_end(args);

    SYSTEMTIME st;
    GetLocalTime(&st);
    char timeBuf[32];
    snprintf(timeBuf, sizeof(timeBuf),
             "%04d-%02d-%02d %02d:%02d:%02d.%03d",
             st.wYear, st.wMonth, st.wDay,
             st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);

    char lineBuf[4608];
    const std::string normalizedModule = NormalizeModule(module, msgBuf);
    snprintf(lineBuf, sizeof(lineBuf), "[%s] [%s][%s] %s\n",
             timeBuf, normalizedModule.c_str(), LevelToString(level), msgBuf);

    EnterCriticalSection(&m_cs);
    std::string desiredLogPath = GetLogFilePath();
    if (!m_file || desiredLogPath != m_currentLogPath) {
        if (m_file) {
            FlushLocked();
            fclose(m_file);
            m_file = nullptr;
            m_currentLogPath.clear();
        }

        std::wstring wLogDir = PathHelper::Utf8ToWide(m_logDir);
        CreateDirectoryW(wLogDir.c_str(), nullptr);
        CleanupOldLogsLocked();

        std::wstring wLogPath = PathHelper::Utf8ToWide(desiredLogPath);
        m_file = _wfsopen(wLogPath.c_str(), L"a", _SH_DENYNO);
        if (m_file) {
            setvbuf(m_file, nullptr, _IOFBF, 64 * 1024);
            WriteUtf8BomIfEmpty(m_file);
            m_currentLogPath = desiredLogPath;
            m_pendingLines = 0;
            m_lastFlushTick = GetTickCount64();
            CheckDiskSpaceLocked();
        } else {
            m_currentLogPath.clear();
        }
    }

    if (m_file) {
        fputs(lineBuf, m_file);
        ++m_pendingLines;
        const ULONGLONG elapsed = GetTickCount64() - m_lastFlushTick;
        if (level == LogLevel::Error ||
            m_pendingLines >= m_flushBatchSize ||
            elapsed >= static_cast<ULONGLONG>(m_flushIntervalMs)) {
            FlushLocked();
        }
    }
    LeaveCriticalSection(&m_cs);

    OutputDebugStringW(PathHelper::Utf8ToWide(lineBuf).c_str());
}

void Logger::Debug(const char* module, const char* function, const char* fmt, ...) {
    if (LogLevel::Debug < m_level) return;
    char buf[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);
    Log(LogLevel::Debug, module, function, "%s", buf);
}

void Logger::Info(const char* module, const char* function, const char* fmt, ...) {
    if (LogLevel::Info < m_level) return;
    char buf[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);
    Log(LogLevel::Info, module, function, "%s", buf);
}

void Logger::Warn(const char* module, const char* function, const char* fmt, ...) {
    if (LogLevel::Warn < m_level) return;
    char buf[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);
    Log(LogLevel::Warn, module, function, "%s", buf);
}

void Logger::Error(const char* module, const char* function, const char* fmt, ...) {
    char buf[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);
    Log(LogLevel::Error, module, function, "%s", buf);
}

void Logger::SetLevel(LogLevel level) {
    m_level = level;
}

} // HZCYKJTHardWare 命名空间结束
