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

namespace { Logger* g_pLogger = nullptr; }

Logger& Logger::Instance() {
    // 审查建议：裸指针惰性初始化不具备并发首次访问保障，且实例未显式释放。
    // 如需支持并发首次调用或 DLL 反复装卸，建议使用 std::call_once，并明确进程级生命周期策略。
    if (!g_pLogger) g_pLogger = new Logger();
    return *g_pLogger;
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
                 "[日志磁盘预警] 日志盘剩余空间不足：available_mb=%llu, threshold_mb=%llu\n",
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
    snprintf(lineBuf, sizeof(lineBuf), "[%s] [%s] [%s] %s\n",
             timeBuf, LevelToString(level), module ? module : "", msgBuf);

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
