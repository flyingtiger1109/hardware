#pragma once
#include "pch.h"
#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <deque>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <thread>

namespace HZCYKJTHardWare {

// 日志级别
enum class LogLevel {
    Debug,
    Info,
    Warn,
    Error
};

// 业务日志上下文仅用于 DLL 内部日志，不参与任何导出 ABI。
struct LogContext {
    std::string operation;
    std::string terminalIndex;
    std::string device;
    std::string requestId;
    std::string result;
    std::string errorCode;
    long long durationMs = -1;
    long long queueWaitMs = -1;
    int attempt = -1;
    long long routeEpoch = -1;
};

// 统一业务上下文字段；空字段不输出，避免制造无意义的日志噪声。
std::string FormatLogContext(const LogContext& context);

// 将导出边界中的完整导出名转换为稳定的短 Operation，仅用于日志关联。
std::string CanonicalOperationName(const std::string& operation);

// 日志侧载荷清理：只保留允许的标量摘要，原始正文永不回写日志。
std::string SanitizeLargePayloadForLog(const std::string& payload,
                                       const std::string& requestId = "");

// 清理 URL 中的用户信息及常见凭据查询参数。
std::string SanitizeUrlForLog(const std::string& url);

// 线程安全日志模块
class Logger {
public:
    static Logger& Instance();

    // 初始化，指定日志目录
    bool Init(const std::string& logDir);
    void Shutdown();
    void ConfigureRetention(int retentionDays, int maxTotalSizeMb,
                            int diskWarningFreeMb, int flushIntervalMs,
                            int flushBatchSize);

    void Log(LogLevel level, const char* module, const char* function, const char* fmt, ...);

    // 对重复的同类故障执行 60 秒窗口聚合；首次故障立即记录，窗口切换时
    // 将汇总与本次故障合并为一条日志，恢复由调用方配合记录。
    void LogRateLimited(LogLevel level, const char* module, const char* function,
                        const char* rateKey, const char* fmt, ...);

    void Debug(const char* module, const char* function, const char* fmt, ...);
    void Info(const char* module, const char* function, const char* fmt, ...);
    void Warn(const char* module, const char* function, const char* fmt, ...);
    void Error(const char* module, const char* function, const char* fmt, ...);

    void SetLevel(LogLevel level);

    uint64_t PendingCount() const;
    uint64_t TotalDroppedCount() const;
    uint64_t WriteFailureCount() const;
    uint64_t CurrentFileLength() const;
    int64_t LastFlushAgeMs() const;
    bool LowDiskMode() const;

private:
    struct FlushRequest {
        std::mutex mutex;
        std::condition_variable condition;
        bool completed = false;
    };

    struct LogEntry {
        LogLevel level = LogLevel::Info;
        std::string date;
        std::string line;
        std::shared_ptr<FlushRequest> flush;
    };

    struct RateLimitBucket {
        ULONGLONG windowStart = 0;
        ULONGLONG lastSeen = 0;
        uint64_t count = 0;
        std::string firstTime;
        std::string lastTime;
        std::string lastError;
    };

    Logger();
    ~Logger();
    Logger(const Logger&) = delete;
    Logger& operator=(const Logger&) = delete;

    const char* LevelToString(LogLevel level) const;
    std::string GetLogFilePath(const std::string& date, int rollIndex = 0) const;
    bool OpenLogFileLocked(const std::string& date, int preferredRollIndex = -1);
    bool RotateLogFileLocked(const std::string& date);
    void CloseLogFileLocked();
    bool WriteLineLocked(const std::string& line, LogLevel level);
    void CleanupOldLogsLocked(bool force = false);
    void CheckDiskSpaceLocked();
    bool FlushLocked();
    void WorkerLoop();
    void WriteEntry(const LogEntry& entry);
    bool Enqueue(LogEntry entry);
    bool WriteEmergencyLine(const std::string& line, const char* reason = nullptr);
    void RecordWriterFailure(const char* reason);
    bool IsLevelEnabled(LogLevel level) const;
    bool CheckRateLimitLocked(const std::string& key,
                              const std::string& message,
                              std::string& windowSummary);

    CRITICAL_SECTION m_cs;
    FILE* m_file = nullptr;
    std::string m_logDir;
    std::string m_currentLogPath;
    std::string m_currentLogDate;
    int m_rollIndex = 0;
    uint64_t m_currentFileSize = 0;
    int m_retentionDays = 30;
    uint64_t m_maxTotalSizeBytes = 2048ULL * 1024ULL * 1024ULL;
    uint64_t m_diskWarningFreeBytes = 2048ULL * 1024ULL * 1024ULL;
    uint64_t m_maxFileSizeBytes = 200ULL * 1024ULL * 1024ULL;
    int m_flushIntervalMs = 500;
    int m_flushBatchSize = 50;
    int m_pendingLines = 0;
    ULONGLONG m_lastFlushTick = 0;
    std::string m_lastCleanupDate;

    static constexpr size_t kMaxQueueLength = 10000;
    static constexpr size_t kReservedErrorQueueLength = 256;
    static constexpr ULONGLONG kRateLimitWindowMs = 60000;
    mutable std::mutex m_queueMutex;
    std::condition_variable m_queueCondition;
    std::deque<LogEntry> m_normalQueue;
    std::deque<LogEntry> m_errorQueue;
    std::thread m_worker;
    bool m_accepting = false;
    bool m_stopRequested = false;

    std::atomic<int> m_minLevel{static_cast<int>(LogLevel::Info)};
    std::atomic<bool> m_lowDiskMode{false};
    std::atomic<uint64_t> m_droppedSinceNotice{0};
    std::atomic<uint64_t> m_totalDroppedCount{0};
    std::atomic<uint64_t> m_writeFailureCount{0};
    std::atomic<uint64_t> m_currentFileLength{0};
    std::atomic<ULONGLONG> m_lastSuccessfulFlushTick{0};
    mutable std::mutex m_failureMutex;
    std::string m_lastWriteError;
    RateLimitBucket m_writerFailureBucket;
    std::map<std::string, RateLimitBucket> m_rateLimitBuckets;
};

} // HZCYKJTHardWare 命名空间结束

// 便捷宏
#define LOG_DEBUG(mod, fmt, ...) HZCYKJTHardWare::Logger::Instance().Debug(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
#define LOG_INFO(mod, fmt, ...)  HZCYKJTHardWare::Logger::Instance().Info(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
#define LOG_WARN(mod, fmt, ...)  HZCYKJTHardWare::Logger::Instance().Warn(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
#define LOG_ERROR(mod, fmt, ...) HZCYKJTHardWare::Logger::Instance().Error(mod, __FUNCTION__, fmt, ##__VA_ARGS__)
#define LOG_WARN_RATE_LIMITED(key, mod, fmt, ...) \
    HZCYKJTHardWare::Logger::Instance().LogRateLimited( \
        HZCYKJTHardWare::LogLevel::Warn, mod, __FUNCTION__, key, fmt, ##__VA_ARGS__)
#define LOG_ERROR_RATE_LIMITED(key, mod, fmt, ...) \
    HZCYKJTHardWare::Logger::Instance().LogRateLimited( \
        HZCYKJTHardWare::LogLevel::Error, mod, __FUNCTION__, key, fmt, ##__VA_ARGS__)
