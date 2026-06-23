#include "pch.h"
#include "logger.h"
#include "path_helper.h"
#include <share.h>

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
}

Logger::Logger() {
    InitializeCriticalSection(&m_cs);
}

Logger::~Logger() {
    DeleteCriticalSection(&m_cs);
}

namespace { Logger* g_pLogger = nullptr; }

Logger& Logger::Instance() {
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

    std::wstring wLogDir = PathHelper::Utf8ToWide(m_logDir);
    CreateDirectoryW(wLogDir.c_str(), nullptr);

    std::string logPath = GetLogFilePath();
    std::wstring wLogPath = PathHelper::Utf8ToWide(logPath);
    m_file = _wfsopen(wLogPath.c_str(), L"a", _SH_DENYNO);
    if (m_file) {
        setvbuf(m_file, nullptr, _IONBF, 0);
    }
    m_currentLogPath = logPath;

    LeaveCriticalSection(&m_cs);
    return m_file != nullptr;
}

void Logger::Shutdown() {
    EnterCriticalSection(&m_cs);
    if (m_file) {
        fclose(m_file);
        m_file = nullptr;
    }
    m_currentLogPath.clear();
    LeaveCriticalSection(&m_cs);
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
            fclose(m_file);
            m_file = nullptr;
        }

        std::wstring wLogDir = PathHelper::Utf8ToWide(m_logDir);
        CreateDirectoryW(wLogDir.c_str(), nullptr);

        std::wstring wLogPath = PathHelper::Utf8ToWide(desiredLogPath);
        m_file = _wfsopen(wLogPath.c_str(), L"a", _SH_DENYNO);
        if (m_file) {
            setvbuf(m_file, nullptr, _IONBF, 0);
            m_currentLogPath = desiredLogPath;
        } else {
            m_currentLogPath.clear();
        }
    }

    if (m_file) {
        fputs(lineBuf, m_file);
        fflush(m_file);
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

} // namespace HZCYKJTHardWare
