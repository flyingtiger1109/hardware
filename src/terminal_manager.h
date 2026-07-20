#pragma once
#include "pch.h"
#include "config_manager.h"

namespace HZCYKJTHardWare {

// 终端管理器：负责终端地址拼接、切换、生命周期
class TerminalManager {
public:
    static TerminalManager& Instance();

    // 用配置初始化
    int Init(const ConfigManager& cfg, const std::string& selectedSubnetPrefix);

    // 根据 index 切换终端
    int SwitchTerminal(int terminalIndex);

    // 根据完整 URL 切换终端
    int SwitchTerminalByUrl(const std::string& baseUrl);

    // 获取当前终端 base_url
    std::string GetCurrentBaseUrl() const;

    // 获取当前终端 index
    int GetCurrentIndex() const;

    // 构建完整请求 URL
    std::string BuildUrl(const std::string& path) const;

    // 终端是否已选择
    bool IsTerminalSelected() const;

    // 获取指定 index 的终端 URL（不切换）
    std::string GetTerminalUrl(int index) const;

private:
    TerminalManager() = default;
    TerminalManager(const TerminalManager&) = delete;
    TerminalManager& operator=(const TerminalManager&) = delete;

    // 停止预览、取消 pending、标记过期（切换终端时调用）
    int PerformSwitchCleanup();

    ConfigManager m_cfg;
    bool m_hasConfig = false;
    std::string m_selectedSubnetPrefix;

};

} // HZCYKJTHardWare 命名空间结束
