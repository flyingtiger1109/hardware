#include "pch.h"
#include "terminal_manager.h"
#include "hzsjkjt_context.h"
#include "preview_manager.h"
#include "request_session_manager.h"
#include "event_dispatcher.h"
#include "logger.h"
#include "network_detector.h"

namespace HZCYKJTHardWare {

namespace { TerminalManager* g_pTermMgr = nullptr; }

TerminalManager& TerminalManager::Instance() {
    // 审查风险：并发首次访问时裸指针惰性初始化存在数据竞争；建议改用 std::call_once 或函数局部静态实例。
    if (!g_pTermMgr) g_pTermMgr = new TerminalManager();
    return *g_pTermMgr;
}

int TerminalManager::Init(const ConfigManager& cfg, const std::string& selectedSubnetPrefix) {
    m_cfg = cfg;
    m_hasConfig = true;
    m_selectedSubnetPrefix = selectedSubnetPrefix;

    auto mode = m_cfg.GetTerminalMode();
    LOG_DEBUG("TerminalMgr", "终端配置初始化：mode=%d，subnet=%s，defaultIndex=%d",
             (int)mode, selectedSubnetPrefix.c_str(), m_cfg.GetDefaultIndex());

    if (mode == TerminalMode::Manual) {
        LOG_DEBUG("TerminalMgr", "manual 模式：等待第三方手动指定终端");
        return HZCYKJTHardWare_RET_OK;
    }

    int defaultIndex = m_cfg.GetDefaultIndex();
    if (defaultIndex >= 1 && defaultIndex <= 2) {
        return SwitchTerminal(defaultIndex);
    }

    return HZCYKJTHardWare_RET_OK;
}

int TerminalManager::SwitchTerminal(int terminalIndex) {
    if (!m_hasConfig) {
        LOG_ERROR("TerminalMgr", "切换终端失败：配置未加载");
        return HZCYKJTHardWare_RET_FAILED;
    }

    if (terminalIndex < 1 || terminalIndex > 2) {
        LOG_ERROR("TerminalMgr", "切换终端失败：终端序号无效，terminalIndex=%d", terminalIndex);
        return HZCYKJTHardWare_RET_TERMINAL_INDEX_INVALID;
    }

    std::string baseUrl;

    auto mode = m_cfg.GetTerminalMode();
    if (mode == TerminalMode::FixedUrl) {
        const auto& fixedTerms = m_cfg.GetFixedTerminals();
        for (const auto& term : fixedTerms) {
            if (term.index == terminalIndex) {
                baseUrl = term.base_url;
                break;
            }
        }
        if (baseUrl.empty()) {
            LOG_ERROR("TerminalMgr", "切换终端失败：固定终端未配置 base_url，terminalIndex=%d", terminalIndex);
            return HZCYKJTHardWare_RET_TERMINAL_INDEX_INVALID;
        }
    } else if (mode == TerminalMode::AutoSubnet) {
        if (m_selectedSubnetPrefix.empty()) {
            LOG_ERROR("TerminalMgr", "切换终端失败：未检测到本机网段");
            return HZCYKJTHardWare_RET_SUBNET_DETECT_FAILED;
        }

        const auto& devices = m_cfg.GetAutoSubnetDevices();
        int suffix = 0;
        for (const auto& dev : devices) {
            if (dev.index == terminalIndex) {
                suffix = dev.host_suffix;
                break;
            }
        }
        if (suffix == 0) {
            LOG_ERROR("TerminalMgr", "切换终端失败：未配置 host_suffix，terminalIndex=%d", terminalIndex);
            return HZCYKJTHardWare_RET_TERMINAL_INDEX_INVALID;
        }

        int port = m_cfg.GetPort();
        char urlBuf[256];
        snprintf(urlBuf, sizeof(urlBuf), "%s://%s.%d:%d",
                 m_cfg.GetScheme().c_str(),
                 m_selectedSubnetPrefix.c_str(),
                 suffix, port);
        baseUrl = urlBuf;
    } else {
        LOG_ERROR("TerminalMgr", "切换终端失败：manual 模式不能按序号自动切换");
        return HZCYKJTHardWare_RET_TERMINAL_NOT_SELECTED;
    }

    return SwitchTerminalByUrl(baseUrl);
}

int TerminalManager::SwitchTerminalByUrl(const std::string& baseUrl) {
    if (baseUrl.empty()) {
        LOG_ERROR("TerminalMgr", "切换终端失败：base_url 为空");
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    LOG_DEBUG("TerminalMgr", "DLL切换当前终端：target=%s", SanitizeUrlForLog(baseUrl).c_str());

    auto previewSnapshot = PreviewManager::Instance().CaptureActivePreviewSnapshot();
    PreviewManager::Instance().StopAllForTerminalSwitch();

    RequestSessionManager::Instance().ExpireAllForTerminalSwitch();

    {
        auto lock = WriteLock();
        auto& ctx = HzsjkjtContext::Instance();

        ctx.current_terminal_base_url = baseUrl;

        int resolvedIndex = 0;
        size_t lastColon = baseUrl.find_last_of(':');
        if (lastColon != std::string::npos) {
            std::string hostPart = baseUrl.substr(0, lastColon);
            size_t lastDot = hostPart.find_last_of('.');
            if (lastDot != std::string::npos) {
                std::string lastOctet = hostPart.substr(lastDot + 1);
                int lastNum = atoi(lastOctet.c_str());
                for (const auto& dev : m_cfg.GetAutoSubnetDevices()) {
                    if (dev.host_suffix == lastNum) {
                        resolvedIndex = dev.index;
                        break;
                    }
                }
            }
        }

        if (resolvedIndex == 0) {
            const auto& fixedTerms = m_cfg.GetFixedTerminals();
            for (const auto& term : fixedTerms) {
                if (!term.base_url.empty() && term.base_url == baseUrl) {
                    resolvedIndex = term.index;
                    break;
                }
            }
        }

        ctx.current_terminal_index = resolvedIndex;
    }

    int previewRestoreRet = PreviewManager::Instance().RestorePreviewsForTerminalSwitch(previewSnapshot);
    if (previewRestoreRet != HZCYKJTHardWare_RET_OK) {
        LOG_ERROR("TerminalMgr", "终端已切换，但自动恢复预览失败：target=%s，ret=%d",
                  SanitizeUrlForLog(baseUrl).c_str(), previewRestoreRet);
        return previewRestoreRet;
    }

    {
        auto lock = ReadLock();
        auto& ctx = HzsjkjtContext::Instance();

        HZCYKJTHardWare_EVENT event;
        memset(&event, 0, sizeof(event));
        event.struct_size = sizeof(HZCYKJTHardWare_EVENT);
        event.event_type = HZCYKJTHardWare_EVENT_TERMINAL_SWITCHED;
        event.terminal_base_url = ctx.current_terminal_base_url.c_str();
        event.terminal_index = ctx.current_terminal_index;
        event.status = HZCYKJTHardWare_RET_OK;

        EventDispatcher::Instance().PostEvent(event);
    }

    LOG_DEBUG("TerminalMgr", "当前终端已切换：terminal_index=%d，terminal=%s",
              HzsjkjtContext::Instance().current_terminal_index,
              SanitizeUrlForLog(baseUrl).c_str());

    return HZCYKJTHardWare_RET_OK;
}

std::string TerminalManager::GetCurrentBaseUrl() const {
    auto lock = ReadLock();
    return HzsjkjtContext::Instance().current_terminal_base_url;
}

int TerminalManager::GetCurrentIndex() const {
    auto lock = ReadLock();
    return HzsjkjtContext::Instance().current_terminal_index;
}

std::string TerminalManager::BuildUrl(const std::string& path) const {
    auto lock = ReadLock();
    const auto& ctx = HzsjkjtContext::Instance();
    std::string base = ctx.current_terminal_base_url;
    if (base.empty()) return "";

    while (!base.empty() && base.back() == '/') base.pop_back();
    std::string p = path;
    if (!p.empty() && p.front() != '/') p = "/" + p;

    return base + p;
}

bool TerminalManager::IsTerminalSelected() const {
    auto lock = ReadLock();
    return !HzsjkjtContext::Instance().current_terminal_base_url.empty();
}

std::string TerminalManager::GetTerminalUrl(int index) const {
    if (!m_hasConfig) return "";
    if (m_selectedSubnetPrefix.empty()) return "";

    const auto& devices = m_cfg.GetAutoSubnetDevices();
    int suffix = 0;
    for (const auto& dev : devices) {
        if (dev.index == index) {
            suffix = dev.host_suffix;
            break;
        }
    }
    if (suffix == 0) return "";

    int port = m_cfg.GetPort();
    char urlBuf[256];
    snprintf(urlBuf, sizeof(urlBuf), "%s://%s.%d:%d",
             m_cfg.GetScheme().c_str(),
             m_selectedSubnetPrefix.c_str(),
             suffix, port);
    return urlBuf;
}

} // HZCYKJTHardWare 命名空间结束
