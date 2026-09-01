#include "pch.h"
#include "terminal_status_checker.h"
#include "include/HZCYKJTHardWare_types.h"
#include "http_client.h"
#include "logger.h"

namespace HZCYKJTHardWare {

int TerminalStatusChecker::Check(const std::string& baseUrl, int connectTimeoutMs) {
    if (baseUrl.empty()) {
        return HZCYKJTHardWare_RET_TERMINAL_NOT_SELECTED;
    }

    LOG_DEBUG("TerminalChecker", "正在检查终端状态：terminal=%s", SanitizeUrlForLog(baseUrl).c_str());

    HttpClient client;
    std::string responseBody;
    int statusCode = 0;

    // 尝试访问终端根路径或一个简单接口
    std::string checkUrl = baseUrl;
    if (checkUrl.back() != '/') checkUrl += '/';

    bool ok = client.Get(checkUrl, connectTimeoutMs, connectTimeoutMs, responseBody, statusCode);

    if (!ok) {
        const std::string safeTerminal = SanitizeUrlForLog(baseUrl);
        LOG_WARN_RATE_LIMITED(safeTerminal.c_str(), "TerminalChecker",
            "终端不可达：terminal=%s", safeTerminal.c_str());
        return HZCYKJTHardWare_RET_TERMINAL_UNREACHABLE;
    }

    // 任何 HTTP 响应（包括 404）都说明终端在线
    LOG_DEBUG("TerminalChecker", "终端已响应：terminal=%s，status=%d",
              SanitizeUrlForLog(baseUrl).c_str(), statusCode);
    return HZCYKJTHardWare_RET_OK;
}

} // HZCYKJTHardWare 命名空间结束
