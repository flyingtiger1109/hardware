#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 终端状态检测模块：只负责检测终端是否在线，不启动/停止进程
class TerminalStatusChecker {
public:
    // 检测指定 URL 是否可访问，返回 HZCYKJTHardWare_RET_OK 或 HZCYKJTHardWare_RET_TERMINAL_UNREACHABLE
    static int Check(const std::string& baseUrl, int connectTimeoutMs);
};

} // namespace HZCYKJTHardWare
