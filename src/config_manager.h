#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 终端模式
enum class TerminalMode {
    AutoSubnet,  // 自动检测 192.168 网段 + host_suffix 拼接
    FixedUrl,    // 使用 fixed_terminals 中配置的 base_url
    Manual       // 手动调用 SwitchTerminalByUrl
};

// 终端配置项
struct TerminalDeviceConfig {
    int index = 0;
    std::string name;
    std::string base_url;
    int host_suffix = 0;
};

// 配置管理器
class ConfigManager {
public:
    // 加载配置：
    // 1. 优先读取 dllDir 下的 HZCYKJTHardWare.json
    // 2. 不存在则使用默认配置
    // 3. JSON 格式错误返回 HZCYKJTHardWare_RET_CONFIG_INVALID
    int Load(const std::string& dllDir);

    // 访问器
    TerminalMode GetTerminalMode() const;
    const std::string& GetScheme() const;
    int GetPort() const;
    bool GetCheckOnInit() const;
    int GetDefaultIndex() const;
    const std::string& GetPreferredSubnetPrefix() const;

    const std::string& GetDelphiServerHost() const;
    int GetDelphiServerPort() const;
    std::string GetDelphiServerUrl() const;
    bool GetDelphiAutoStart() const;
    const std::string& GetDelphiExecutable() const;
    int GetDelphiStartWaitMs() const;
    int GetDelphiPingIntervalMs() const;

    const std::vector<TerminalDeviceConfig>& GetFixedTerminals() const;
    const std::vector<TerminalDeviceConfig>& GetAutoSubnetDevices() const;

    const std::string& GetCallbackServerHost() const;
    int GetCallbackServerPort() const;
    bool GetAutoBindLanIp() const;
    bool GetListenAny() const;
    const std::string& GetCallbackBasePath() const;

    int GetHttpConnectTimeoutMs() const;
    int GetHttpRequestTimeoutMs() const;
    int GetFaceCaptureTimeoutMs() const;
    int GetFingerprintCaptureTimeoutMs() const;
    int GetOcrTimeoutMs() const;

    const std::string& GetSaveDefaultRoot() const;
    bool GetCreateDateFolder() const;
    bool GetCreateRequestFolder() const;

    const std::string& GetPreviewRenderer() const;
    bool GetAutoReconnect() const;
    int GetCheckHwndIntervalMs() const;
    bool GetStopPreviewOnEndProcess() const;
    int GetRtspNetworkCachingMs() const;
    int GetRtspLiveCachingMs() const;
    const std::string& GetRtspTransport() const;

    const std::string& GetLogDir() const;
    const std::string& GetLogLevel() const;

    // 是否存在配置文件
    bool HasConfigFile() const;

private:
    int ParseJson(const std::string& json);
    void ApplyDefaults();

    TerminalMode m_mode = TerminalMode::AutoSubnet;
    std::string m_scheme = "http";
    int m_port = 8080;
    bool m_checkOnInit = false;
    int m_defaultIndex = 1;
    std::string m_preferredSubnetPrefix;

    std::string m_delphiServerHost = "127.0.0.1";
    int m_delphiServerPort = 8080;
    bool m_delphiAutoStart = true;
    std::string m_delphiExecutable = "HZCYKJTHardWare.exe";
    int m_delphiStartWaitMs = 10000;
    int m_delphiPingIntervalMs = 300;

    std::vector<TerminalDeviceConfig> m_fixedTerminals;
    std::vector<TerminalDeviceConfig> m_autoSubnetDevices;

    std::string m_callbackServerHost;
    int m_callbackServerPort = 39091;
    bool m_autoBindLanIp = true;
    bool m_listenAny = true;
    std::string m_callbackBasePath = "/HZCYKJTHardWare/callback";

    int m_httpConnectTimeoutMs = 3000;
    int m_httpRequestTimeoutMs = 5000;
    int m_faceCaptureTimeoutMs = 15000;
    int m_fingerprintCaptureTimeoutMs = 15000;
    int m_ocrTimeoutMs = 20000;

    std::string m_saveDefaultRoot;
    bool m_createDateFolder = true;
    bool m_createRequestFolder = true;

    std::string m_previewRenderer = "libvlc";
    bool m_autoReconnect = true;
    int m_checkHwndIntervalMs = 500;
    bool m_stopPreviewOnEndProcess = false;
    int m_rtspNetworkCachingMs = 150;
    int m_rtspLiveCachingMs = 150;
    std::string m_rtspTransport = "tcp";

    std::string m_logDir = "HZCYKJTHardWareDLL_Logs";
    std::string m_logLevel = "info";

    bool m_hasConfigFile = false;
};

} // namespace HZCYKJTHardWare
