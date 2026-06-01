#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 日志级别
enum class LogLevel {
    Debug,
    Info,
    Warn,
    Error
};

// 线程安全日志模块
class Logger {
public:
    static Logger& Instance();

    // 初始化，指定日志目录
    bool Init(const std::string& logDir);
    void Shutdown();

    void Log(LogLevel level, const char* module, const char* function, const char* fmt, ...);

    void Debug(const char* module, const char* function, const char* fmt, ...);
    void Info(const char* module, const char* function, const char* fmt, ...);
    void Warn(const char* module, const char* function, const char* fmt, ...);
    void Error(const char* module, const char* function, const char* fmt, ...);

    void SetLevel(LogLevel level);

private:
    Logger();
    ~Logger();
    Logger(const Logger&) = delete;
    Logger& operator=(const Logger&) = delete;

    const char* LevelToString(LogLevel level);
    std::string GetLogFilePath();

    CRITICAL_SECTION m_cs;
    FILE* m_file = nullptr;
    LogLevel m_level = LogLevel::Info;
    std::string m_logDir;
    std::string m_currentLogPath;
};

} // namespace HZCYKJTHardWare

// 便捷宏
#define LOG_DEBUG(mod, fmt, ...) HZCYKJTHardWare::Logger::Instance().Debug(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
#define LOG_INFO(mod, fmt, ...)  HZCYKJTHardWare::Logger::Instance().Info(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
#define LOG_WARN(mod, fmt, ...)  HZCYKJTHardWare::Logger::Instance().Warn(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
#define LOG_ERROR(mod, fmt, ...) HZCYKJTHardWare::Logger::Instance().Error(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
