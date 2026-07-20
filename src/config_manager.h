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

enum class PlatePreviewChannel {
    CJ,
    RJ2,
    RJ3
};

struct PlatePreviewCameraConfig {
    bool enabled = false;
    std::string host;
    int port = 554;
    std::string username;
    std::string password;
    int stream_channel = 101;
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
    int GetAuthorizeTimeoutMs() const;

    // DLL 对外 char* 输入编码：auto / gbk / utf8。
    // DLL 内部、HTTP/JSON 与第三方回调始终保持 UTF-8。
    const std::string& GetThirdPartyInputEncoding() const;

    const std::string& GetSaveDefaultRoot() const;
    const std::string& GetCameraDefaultPath() const;
    const std::string& GetFingerprintDefaultPath() const;
    bool GetCreateDateFolder() const;
    bool GetCreateRequestFolder() const;

    const std::string& GetPreviewRenderer() const;
    bool GetAutoReconnect() const;
    int GetCheckHwndIntervalMs() const;
    bool GetStopPreviewOnEndProcess() const;
    int GetRtspNetworkCachingMs() const;
    int GetRtspLiveCachingMs() const;
    const std::string& GetRtspTransport() const;
    const PlatePreviewCameraConfig& GetPlatePreviewConfig(PlatePreviewChannel channel) const;
    std::string BuildPlatePreviewUrl(PlatePreviewChannel channel) const;

    const std::string& GetLogDir() const;
    const std::string& GetLogLevel() const;
    int GetLogRetentionDays() const;
    int GetLogMaxTotalSizeMb() const;
    int GetLogDiskWarningFreeMb() const;
    int GetLogFlushIntervalMs() const;
    int GetLogFlushBatchSize() const;

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
    int m_delphiServerPort = 18080;
    bool m_delphiAutoStart = true;
    std::string m_delphiExecutable = "HZCYKJTHardWare.exe";
    int m_delphiStartWaitMs = 10000;
    int m_delphiPingIntervalMs = 300;

    std::vector<TerminalDeviceConfig> m_fixedTerminals;
    std::vector<TerminalDeviceConfig> m_autoSubnetDevices;

    std::string m_callbackServerHost;
    int m_callbackServerPort = 39091;
    bool m_autoBindLanIp = true;
    bool m_listenAny = false;
    std::string m_callbackBasePath = "/HZCYKJTHardWare/callback";

    int m_httpConnectTimeoutMs = 3000;
    int m_httpRequestTimeoutMs = 5000;
    int m_faceCaptureTimeoutMs = 15000;
    int m_fingerprintCaptureTimeoutMs = 15000;
    int m_ocrTimeoutMs = 20000;
    int m_authorizeTimeoutMs = 60000;
    std::string m_thirdPartyInputEncoding = "auto";

    std::string m_saveDefaultRoot;
    std::string m_cameraDefaultPath = ".\\captures\\camera.jpg";
    std::string m_fingerprintDefaultPath = ".\\captures\\fingerprint.jpg";
    bool m_createDateFolder = true;
    bool m_createRequestFolder = true;

    std::string m_previewRenderer = "libvlc";
    bool m_autoReconnect = true;
    int m_checkHwndIntervalMs = 500;
    bool m_stopPreviewOnEndProcess = false;
    int m_rtspNetworkCachingMs = 150;
    int m_rtspLiveCachingMs = 150;
    std::string m_rtspTransport = "tcp";
    PlatePreviewCameraConfig m_platePreviewCJ;
    PlatePreviewCameraConfig m_platePreviewRJ2;
    PlatePreviewCameraConfig m_platePreviewRJ3;

    std::string m_logDir = "HZCYKJTHardWareDLL_Logs";
    std::string m_logLevel = "info";
    int m_logRetentionDays = 30;
    int m_logMaxTotalSizeMb = 2048;
    int m_logDiskWarningFreeMb = 2048;
    int m_logFlushIntervalMs = 500;
    int m_logFlushBatchSize = 50;

    bool m_hasConfigFile = false;
};

} // HZCYKJTHardWare 命名空间结束
