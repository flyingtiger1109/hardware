#include "pch.h"
#include "config_manager.h"
#include <algorithm>
#include <cctype>
#include "include/HZCYKJTHardWare_types.h"
#include "logger.h"
#include "path_helper.h"
#include "json_helper.h"

namespace HZCYKJTHardWare {

namespace {

std::string EncodeRtspUserInfo(const std::string& value) {
    static const char kHex[] = "0123456789ABCDEF";
    std::string encoded;
    encoded.reserve(value.size());
    for (unsigned char ch : value) {
        const bool unreserved =
            (ch >= 'a' && ch <= 'z') ||
            (ch >= 'A' && ch <= 'Z') ||
            (ch >= '0' && ch <= '9') ||
            ch == '-' || ch == '.' || ch == '_' || ch == '~';
        if (unreserved) {
            encoded.push_back(static_cast<char>(ch));
        } else {
            encoded.push_back('%');
            encoded.push_back(kHex[(ch >> 4) & 0x0F]);
            encoded.push_back(kHex[ch & 0x0F]);
        }
    }
    return encoded;
}

std::string NormalizeRtspHost(const std::string& host) {
    if (host.empty() || host.front() == '[' || host.find(':') == std::string::npos) {
        return host;
    }
    return "[" + host + "]";
}

void ParsePlatePreviewCamera(const std::string& plateObj,
                             const char* cameraKey,
                             PlatePreviewCameraConfig& config) {
    const std::string cameraObj = JsonHelper::GetJsonObject(plateObj, cameraKey);
    if (cameraObj.empty()) return;

    if (JsonHelper::HasKey(cameraObj, "enabled"))
        config.enabled = JsonHelper::GetBool(cameraObj, "enabled", false);
    if (JsonHelper::HasKey(cameraObj, "host"))
        config.host = JsonHelper::GetString(cameraObj, "host");
    if (JsonHelper::HasKey(cameraObj, "port"))
        config.port = JsonHelper::GetInt(cameraObj, "port", 554);
    if (JsonHelper::HasKey(cameraObj, "username"))
        config.username = JsonHelper::GetString(cameraObj, "username");
    if (JsonHelper::HasKey(cameraObj, "password"))
        config.password = JsonHelper::GetString(cameraObj, "password");
    if (JsonHelper::HasKey(cameraObj, "stream_channel"))
        config.stream_channel = JsonHelper::GetInt(cameraObj, "stream_channel", 101);

    if (config.port <= 0 || config.port > 65535)
        config.port = 554;
    if (config.stream_channel != 101 && config.stream_channel != 102)
        config.stream_channel = 101;
}

} // 匿名命名空间结束

int ConfigManager::Load(const std::string& dllDir) {
    // 始终先填充默认值，后续由 JSON 文件中存在的字段覆盖
    ApplyDefaults();

    std::string configPath = PathHelper::Join(dllDir, "HZCYKJTHardWare.json");

    if (!PathHelper::FileExists(configPath)) {
        m_hasConfigFile = false;
        LOG_WARN("配置管理", "未找到 HZCYKJTHardWare.json，使用默认配置：path=%s", configPath.c_str());
        LOG_DEBUG("配置管理", "默认配置：terminal.preferred_subnet_prefix=%s",
                 m_preferredSubnetPrefix.empty() ? "(empty)" : m_preferredSubnetPrefix.c_str());
        return HZCYKJTHardWare_RET_OK; // 不存在但可使用默认配置
    }

    // 读取文件内容
    std::wstring wConfigPath = PathHelper::Utf8ToWide(configPath);
    std::ifstream file(wConfigPath, std::ios::in | std::ios::binary);
    if (!file.is_open()) {
        LOG_ERROR("配置管理", "打开 HZCYKJTHardWare.json 失败：path=%s", configPath.c_str());
        return HZCYKJTHardWare_RET_CONFIG_INVALID;
    }

    std::stringstream ss;
    ss << file.rdbuf();
    std::string json = ss.str();
    file.close();

    const size_t first = json.find_first_not_of(" \t\r\n");
    const size_t last = json.find_last_not_of(" \t\r\n");
    if (first == std::string::npos || json[first] != '{' ||
        last == std::string::npos || json[last] != '}') {
        LOG_ERROR("配置管理", "配置文件内容损坏，使用默认配置并回退DeviceMode=1：路径=%s",
                  configPath.c_str());
        return HZCYKJTHardWare_RET_OK;
    }

    m_hasConfigFile = true;

    int ret = ParseJson(json);
    if (ret != HZCYKJTHardWare_RET_OK) {
        LOG_ERROR("配置管理", "配置内容无效，使用完整默认配置并回退 DeviceMode=1");
        ApplyDefaults();
        return HZCYKJTHardWare_RET_OK;
    }

    LOG_INFO("配置管理", "配置文件加载成功：path=%s", configPath.c_str());
    LOG_DEBUG("配置管理", "配置：terminal.mode=%s",
             m_mode == TerminalMode::AutoSubnet ? "auto_subnet" :
             (m_mode == TerminalMode::FixedUrl ? "fixed_url" : "manual"));
    LOG_DEBUG("配置管理", "配置：callback_server.auto_bind_lan_ip=%s", m_autoBindLanIp ? "true" : "false");

    return HZCYKJTHardWare_RET_OK;
}

int ConfigManager::ParseJson(const std::string& json) {
    if (JsonHelper::HasKey(json, "device_mode")) {
        const int mode = JsonHelper::GetInt(json, "device_mode", 0);
        if (mode == 1 || mode == 2) {
            m_deviceMode = mode;
        } else {
            m_deviceMode = 1;
        LOG_WARN("配置管理", "device_mode配置非法，仅支持1/2，回退到DeviceMode=1");
        }
    } else {
        LOG_WARN("配置管理", "device_mode配置缺失，回退到DeviceMode=1");
    }
    if (JsonHelper::HasKey(json, "third_party_input_encoding")) {
        m_thirdPartyInputEncoding = JsonHelper::GetString(
            json, "third_party_input_encoding");
        std::transform(m_thirdPartyInputEncoding.begin(),
                       m_thirdPartyInputEncoding.end(),
                       m_thirdPartyInputEncoding.begin(),
                       [](unsigned char ch) {
                           return static_cast<char>(std::tolower(ch));
                       });
        if (m_thirdPartyInputEncoding != "auto" &&
            m_thirdPartyInputEncoding != "gbk" &&
            m_thirdPartyInputEncoding != "utf8") {
            LOG_ERROR("配置管理",
                      "third_party_input_encoding 配置无效：value=%s，仅支持 auto/gbk/utf8",
                      m_thirdPartyInputEncoding.c_str());
            return HZCYKJTHardWare_RET_CONFIG_INVALID;
        }
    }

    // delphi_server 配置：字段名保持兼容，当前 DLL 转发到 C# Proxy。
    std::string delphiObj = JsonHelper::GetJsonObject(json, "delphi_server");
    if (!delphiObj.empty()) {
        if (JsonHelper::HasKey(delphiObj, "host"))
            m_delphiServerHost = JsonHelper::GetString(delphiObj, "host");
        if (JsonHelper::HasKey(delphiObj, "port"))
            m_delphiServerPort = JsonHelper::GetInt(delphiObj, "port", 18080);
        if (JsonHelper::HasKey(delphiObj, "auto_start"))
            m_delphiAutoStart = JsonHelper::GetBool(delphiObj, "auto_start", true);
        if (JsonHelper::HasKey(delphiObj, "executable"))
            m_delphiExecutable = JsonHelper::GetString(delphiObj, "executable");
        if (JsonHelper::HasKey(delphiObj, "start_wait_ms"))
            m_delphiStartWaitMs = JsonHelper::GetInt(delphiObj, "start_wait_ms", 10000);
        if (JsonHelper::HasKey(delphiObj, "ping_interval_ms"))
            m_delphiPingIntervalMs = JsonHelper::GetInt(delphiObj, "ping_interval_ms", 300);
    }
    if (m_delphiServerHost.empty()) {
        m_delphiServerHost = "127.0.0.1";
    }
    if (m_delphiServerPort <= 0 || m_delphiServerPort > 65535) {
        m_delphiServerPort = 18080;
    }
    if (m_delphiExecutable.empty()) {
        m_delphiExecutable = "HZCYKJTHardWare.exe";
    }
    if (m_delphiStartWaitMs <= 0) {
        m_delphiStartWaitMs = 10000;
    }
    if (m_delphiPingIntervalMs <= 0) {
        m_delphiPingIntervalMs = 300;
    }

    // terminal 配置
    std::string termObj = JsonHelper::GetJsonObject(json, "terminal");
    if (!termObj.empty()) {
        std::string mode = JsonHelper::GetString(termObj, "mode");
        if (mode == "fixed_url") {
            m_mode = TerminalMode::FixedUrl;
        } else if (mode == "manual") {
            m_mode = TerminalMode::Manual;
        } else {
            m_mode = TerminalMode::AutoSubnet;
        }

        if (JsonHelper::HasKey(termObj, "scheme"))
            m_scheme = JsonHelper::GetString(termObj, "scheme");
        if (JsonHelper::HasKey(termObj, "port"))
            m_port = JsonHelper::GetInt(termObj, "port");
        if (JsonHelper::HasKey(termObj, "check_on_init"))
            m_checkOnInit = JsonHelper::GetBool(termObj, "check_on_init");
        if (JsonHelper::HasKey(termObj, "default_index"))
            m_defaultIndex = JsonHelper::GetInt(termObj, "default_index");
        if (JsonHelper::HasKey(termObj, "preferred_subnet_prefix"))
            m_preferredSubnetPrefix = JsonHelper::GetString(termObj, "preferred_subnet_prefix");

        // fixed_terminals 数组
        std::string fixedArr = JsonHelper::GetArray(termObj, "fixed_terminals");
        if (!fixedArr.empty()) {
            m_fixedTerminals.clear();
            // 简易解析数组中的对象
            size_t pos = 0;
            while (pos < fixedArr.size()) {
                size_t objStart = fixedArr.find('{', pos);
                if (objStart == std::string::npos) break;
                int depth = 0;
                size_t objEnd = objStart;
                while (objEnd < fixedArr.size()) {
                    if (fixedArr[objEnd] == '{') depth++;
                    else if (fixedArr[objEnd] == '}') {
                        depth--;
                        if (depth == 0) break;
                    } else if (fixedArr[objEnd] == '"') {
                        objEnd++;
                        while (objEnd < fixedArr.size()) {
                            if (fixedArr[objEnd] == '\\') { objEnd += 2; continue; }
                            if (fixedArr[objEnd] == '"') break;
                            objEnd++;
                        }
                    }
                    objEnd++;
                }
                if (objEnd >= fixedArr.size()) break;

                std::string obj = fixedArr.substr(objStart, objEnd - objStart + 1);
                TerminalDeviceConfig dev;
                dev.index = JsonHelper::GetInt(obj, "index", 0);
                dev.name = JsonHelper::GetString(obj, "name");
                dev.base_url = JsonHelper::GetString(obj, "base_url");
                m_fixedTerminals.push_back(dev);

                pos = objEnd + 1;
            }
        }

        // auto_subnet_devices 数组
        std::string autoArr = JsonHelper::GetArray(termObj, "auto_subnet_devices");
        if (!autoArr.empty()) {
            m_autoSubnetDevices.clear();
            size_t pos = 0;
            while (pos < autoArr.size()) {
                size_t objStart = autoArr.find('{', pos);
                if (objStart == std::string::npos) break;
                int depth = 0;
                size_t objEnd = objStart;
                while (objEnd < autoArr.size()) {
                    if (autoArr[objEnd] == '{') depth++;
                    else if (autoArr[objEnd] == '}') {
                        depth--;
                        if (depth == 0) break;
                    } else if (autoArr[objEnd] == '"') {
                        objEnd++;
                        while (objEnd < autoArr.size()) {
                            if (autoArr[objEnd] == '\\') { objEnd += 2; continue; }
                            if (autoArr[objEnd] == '"') break;
                            objEnd++;
                        }
                    }
                    objEnd++;
                }
                if (objEnd >= autoArr.size()) break;

                std::string obj = autoArr.substr(objStart, objEnd - objStart + 1);
                TerminalDeviceConfig dev;
                dev.index = JsonHelper::GetInt(obj, "index", 0);
                dev.name = JsonHelper::GetString(obj, "name");
                dev.host_suffix = JsonHelper::GetInt(obj, "host_suffix", 0);
                m_autoSubnetDevices.push_back(dev);

                pos = objEnd + 1;
            }
        }

        // 如果配置文件没提供 auto_subnet_devices，填充默认值
        if (m_autoSubnetDevices.empty() && m_mode == TerminalMode::AutoSubnet) {
            m_autoSubnetDevices.push_back({1, "terminal_a", "", 10});
            m_autoSubnetDevices.push_back({2, "terminal_b", "", 11});
        }
    }

    // callback_server 配置
    std::string cbObj = JsonHelper::GetJsonObject(json, "callback_server");
    if (!cbObj.empty()) {
        if (JsonHelper::HasKey(cbObj, "host"))
            m_callbackServerHost = JsonHelper::GetString(cbObj, "host");
        if (JsonHelper::HasKey(cbObj, "port"))
            m_callbackServerPort = JsonHelper::GetInt(cbObj, "port");
        if (JsonHelper::HasKey(cbObj, "auto_bind_lan_ip"))
            m_autoBindLanIp = JsonHelper::GetBool(cbObj, "auto_bind_lan_ip");
        if (JsonHelper::HasKey(cbObj, "listen_any"))
            m_listenAny = JsonHelper::GetBool(cbObj, "listen_any");
        if (JsonHelper::HasKey(cbObj, "base_path"))
            m_callbackBasePath = JsonHelper::GetString(cbObj, "base_path");
    }

    // timeout 配置
    std::string timeoutObj = JsonHelper::GetJsonObject(json, "timeout");
    if (!timeoutObj.empty()) {
        if (JsonHelper::HasKey(timeoutObj, "http_connect_ms"))
            m_httpConnectTimeoutMs = JsonHelper::GetInt(timeoutObj, "http_connect_ms");
        if (JsonHelper::HasKey(timeoutObj, "http_request_ms"))
            m_httpRequestTimeoutMs = JsonHelper::GetInt(timeoutObj, "http_request_ms");
        if (JsonHelper::HasKey(timeoutObj, "face_capture_ms"))
            m_faceCaptureTimeoutMs = JsonHelper::GetInt(timeoutObj, "face_capture_ms");
        if (JsonHelper::HasKey(timeoutObj, "fingerprint_capture_ms"))
            m_fingerprintCaptureTimeoutMs = JsonHelper::GetInt(timeoutObj, "fingerprint_capture_ms");
        if (JsonHelper::HasKey(timeoutObj, "ocr_ms"))
            m_ocrTimeoutMs = JsonHelper::GetInt(timeoutObj, "ocr_ms");
        if (JsonHelper::HasKey(timeoutObj, "authorize_ms"))
            m_authorizeTimeoutMs = JsonHelper::GetInt(timeoutObj, "authorize_ms");
    }

    // save 配置
    std::string saveObj = JsonHelper::GetJsonObject(json, "save");
    if (!saveObj.empty()) {
        if (JsonHelper::HasKey(saveObj, "default_root"))
            m_saveDefaultRoot = JsonHelper::GetString(saveObj, "default_root");
        if (JsonHelper::HasKey(saveObj, "camera_default_path"))
            m_cameraDefaultPath = JsonHelper::GetString(saveObj, "camera_default_path");
        if (JsonHelper::HasKey(saveObj, "fingerprint_default_path"))
            m_fingerprintDefaultPath = JsonHelper::GetString(saveObj, "fingerprint_default_path");
        if (JsonHelper::HasKey(saveObj, "create_date_folder"))
            m_createDateFolder = JsonHelper::GetBool(saveObj, "create_date_folder");
        if (JsonHelper::HasKey(saveObj, "create_request_folder"))
            m_createRequestFolder = JsonHelper::GetBool(saveObj, "create_request_folder");
    }

    // preview 配置
    std::string previewObj = JsonHelper::GetJsonObject(json, "preview");
    if (!previewObj.empty()) {
        if (JsonHelper::HasKey(previewObj, "renderer"))
            m_previewRenderer = JsonHelper::GetString(previewObj, "renderer");
        if (JsonHelper::HasKey(previewObj, "auto_reconnect"))
            m_autoReconnect = JsonHelper::GetBool(previewObj, "auto_reconnect");
        if (JsonHelper::HasKey(previewObj, "check_hwnd_interval_ms"))
            m_checkHwndIntervalMs = JsonHelper::GetInt(previewObj, "check_hwnd_interval_ms");
        if (JsonHelper::HasKey(previewObj, "stop_preview_on_end_process"))
            m_stopPreviewOnEndProcess = JsonHelper::GetBool(previewObj, "stop_preview_on_end_process");
        if (JsonHelper::HasKey(previewObj, "rtsp_network_caching_ms"))
            m_rtspNetworkCachingMs = JsonHelper::GetInt(previewObj, "rtsp_network_caching_ms");
        if (JsonHelper::HasKey(previewObj, "rtsp_live_caching_ms"))
            m_rtspLiveCachingMs = JsonHelper::GetInt(previewObj, "rtsp_live_caching_ms");
        if (JsonHelper::HasKey(previewObj, "rtsp_transport"))
            m_rtspTransport = JsonHelper::GetString(previewObj, "rtsp_transport");

        // 车牌相机采用扁平化配置。方向与相机的组合关系由第三方调用方维护，不由 DLL 或 C# Proxy 解析。
        std::string plateObj = JsonHelper::GetJsonObject(previewObj, "plate");
        if (!plateObj.empty()) {
            ParsePlatePreviewCamera(plateObj, "cj", m_platePreviewCJ);
            ParsePlatePreviewCamera(plateObj, "rj2", m_platePreviewRJ2);
            ParsePlatePreviewCamera(plateObj, "rj3", m_platePreviewRJ3);
        }
    }

    // log 配置
    std::string logObj = JsonHelper::GetJsonObject(json, "log");
    if (!logObj.empty()) {
        if (JsonHelper::HasKey(logObj, "dir"))
            m_logDir = JsonHelper::GetString(logObj, "dir");
        if (JsonHelper::HasKey(logObj, "level"))
            m_logLevel = JsonHelper::GetString(logObj, "level");
        if (JsonHelper::HasKey(logObj, "retention_days"))
            m_logRetentionDays = std::clamp(
                JsonHelper::GetInt(logObj, "retention_days"), 1, 3650);
        if (JsonHelper::HasKey(logObj, "max_total_size_mb"))
            m_logMaxTotalSizeMb = std::clamp(
                JsonHelper::GetInt(logObj, "max_total_size_mb"), 16, 102400);
        if (JsonHelper::HasKey(logObj, "disk_warning_free_mb"))
            m_logDiskWarningFreeMb = std::clamp(
                JsonHelper::GetInt(logObj, "disk_warning_free_mb"), 0, 102400);
        if (JsonHelper::HasKey(logObj, "flush_interval_ms"))
            m_logFlushIntervalMs = std::clamp(
                JsonHelper::GetInt(logObj, "flush_interval_ms"), 50, 10000);
        if (JsonHelper::HasKey(logObj, "flush_batch_size"))
            m_logFlushBatchSize = std::clamp(
                JsonHelper::GetInt(logObj, "flush_batch_size"), 1, 10000);
    }

    return HZCYKJTHardWare_RET_OK;
}

void ConfigManager::ApplyDefaults() {
    m_deviceMode = 1;
    m_mode = TerminalMode::AutoSubnet;
    m_scheme = "http";
    m_port = 9098;
    m_checkOnInit = false;
    m_defaultIndex = 1;
    m_preferredSubnetPrefix = "192.168.20";

    m_delphiServerHost = "127.0.0.1";
    m_delphiServerPort = 18080;
    m_delphiAutoStart = true;
    m_delphiExecutable = "HZCYKJTHardWare.exe";
    m_delphiStartWaitMs = 10000;
    m_delphiPingIntervalMs = 300;

    m_fixedTerminals.clear();
    m_fixedTerminals.push_back({1, "terminal_a", ""});
    m_fixedTerminals.push_back({2, "terminal_b", ""});

    m_autoSubnetDevices.clear();
    m_autoSubnetDevices.push_back({1, "terminal_a", "", 30});
    m_autoSubnetDevices.push_back({2, "terminal_b", "", 31});

    m_callbackServerHost.clear();
    m_callbackServerPort = 39091;
    m_autoBindLanIp = true;
    m_listenAny = false;
    m_callbackBasePath = "/HZCYKJTHardWare/callback";

    m_httpConnectTimeoutMs = 3000;
    m_httpRequestTimeoutMs = 3000;
    m_faceCaptureTimeoutMs = 5000;
    m_fingerprintCaptureTimeoutMs = 5000;
    m_ocrTimeoutMs = 10000;
    m_authorizeTimeoutMs = 60000;
    m_thirdPartyInputEncoding = "auto";

    m_saveDefaultRoot = ".\\captures";
    m_cameraDefaultPath = ".\\captures\\camera.jpg";
    m_fingerprintDefaultPath = ".\\captures\\fingerprint.jpg";
    m_createDateFolder = true;
    m_createRequestFolder = true;

    m_previewRenderer = "libvlc";
    m_autoReconnect = true;
    m_checkHwndIntervalMs = 500;
    m_stopPreviewOnEndProcess = false;
    m_rtspNetworkCachingMs = 150;
    m_rtspLiveCachingMs = 150;
    m_rtspTransport = "tcp";
    m_platePreviewCJ = PlatePreviewCameraConfig{};
    m_platePreviewRJ2 = PlatePreviewCameraConfig{};
    m_platePreviewRJ3 = PlatePreviewCameraConfig{};

    m_logDir = "HZCYKJTHardWareDLL_Logs";
    m_logLevel = "info";
    m_logRetentionDays = 30;
    m_logMaxTotalSizeMb = 2048;
    m_logDiskWarningFreeMb = 2048;
    m_logFlushIntervalMs = 500;
    m_logFlushBatchSize = 50;
}

// 访问器实现
TerminalMode ConfigManager::GetTerminalMode() const { return m_mode; }
int ConfigManager::GetDeviceMode() const { return m_deviceMode; }
const std::string& ConfigManager::GetScheme() const { return m_scheme; }
int ConfigManager::GetPort() const { return m_port; }
bool ConfigManager::GetCheckOnInit() const { return m_checkOnInit; }
int ConfigManager::GetDefaultIndex() const { return m_defaultIndex; }
const std::string& ConfigManager::GetPreferredSubnetPrefix() const { return m_preferredSubnetPrefix; }
const std::string& ConfigManager::GetDelphiServerHost() const { return m_delphiServerHost; }
int ConfigManager::GetDelphiServerPort() const { return m_delphiServerPort; }
std::string ConfigManager::GetDelphiServerUrl() const {
    return "http://" + m_delphiServerHost + ":" + std::to_string(m_delphiServerPort);
}
bool ConfigManager::GetDelphiAutoStart() const { return m_delphiAutoStart; }
const std::string& ConfigManager::GetDelphiExecutable() const { return m_delphiExecutable; }
int ConfigManager::GetDelphiStartWaitMs() const { return m_delphiStartWaitMs; }
int ConfigManager::GetDelphiPingIntervalMs() const { return m_delphiPingIntervalMs; }
const std::vector<TerminalDeviceConfig>& ConfigManager::GetFixedTerminals() const { return m_fixedTerminals; }
const std::vector<TerminalDeviceConfig>& ConfigManager::GetAutoSubnetDevices() const { return m_autoSubnetDevices; }
const std::string& ConfigManager::GetCallbackServerHost() const { return m_callbackServerHost; }
int ConfigManager::GetCallbackServerPort() const { return m_callbackServerPort; }
bool ConfigManager::GetAutoBindLanIp() const { return m_autoBindLanIp; }
bool ConfigManager::GetListenAny() const { return m_listenAny; }
const std::string& ConfigManager::GetCallbackBasePath() const { return m_callbackBasePath; }
int ConfigManager::GetHttpConnectTimeoutMs() const { return m_httpConnectTimeoutMs; }
int ConfigManager::GetHttpRequestTimeoutMs() const { return m_httpRequestTimeoutMs; }
int ConfigManager::GetFaceCaptureTimeoutMs() const { return m_faceCaptureTimeoutMs; }
int ConfigManager::GetFingerprintCaptureTimeoutMs() const { return m_fingerprintCaptureTimeoutMs; }
int ConfigManager::GetOcrTimeoutMs() const { return m_ocrTimeoutMs; }
int ConfigManager::GetAuthorizeTimeoutMs() const { return m_authorizeTimeoutMs; }
const std::string& ConfigManager::GetThirdPartyInputEncoding() const { return m_thirdPartyInputEncoding; }
const std::string& ConfigManager::GetSaveDefaultRoot() const { return m_saveDefaultRoot; }
const std::string& ConfigManager::GetCameraDefaultPath() const { return m_cameraDefaultPath; }
const std::string& ConfigManager::GetFingerprintDefaultPath() const { return m_fingerprintDefaultPath; }
bool ConfigManager::GetCreateDateFolder() const { return m_createDateFolder; }
bool ConfigManager::GetCreateRequestFolder() const { return m_createRequestFolder; }
const std::string& ConfigManager::GetPreviewRenderer() const { return m_previewRenderer; }
bool ConfigManager::GetAutoReconnect() const { return m_autoReconnect; }
int ConfigManager::GetCheckHwndIntervalMs() const { return m_checkHwndIntervalMs; }
bool ConfigManager::GetStopPreviewOnEndProcess() const { return m_stopPreviewOnEndProcess; }
int ConfigManager::GetRtspNetworkCachingMs() const { return m_rtspNetworkCachingMs; }
int ConfigManager::GetRtspLiveCachingMs() const { return m_rtspLiveCachingMs; }
const std::string& ConfigManager::GetRtspTransport() const { return m_rtspTransport; }
const PlatePreviewCameraConfig& ConfigManager::GetPlatePreviewConfig(PlatePreviewChannel channel) const {
    switch (channel) {
        case PlatePreviewChannel::RJ2: return m_platePreviewRJ2;
        case PlatePreviewChannel::RJ3: return m_platePreviewRJ3;
        case PlatePreviewChannel::CJ:
        default: return m_platePreviewCJ;
    }
}

std::string ConfigManager::BuildPlatePreviewUrl(PlatePreviewChannel channel) const {
    const PlatePreviewCameraConfig& config = GetPlatePreviewConfig(channel);
    if (!config.enabled || config.host.empty()) {
        return "";
    }

    std::string authority;
    if (!config.username.empty() || !config.password.empty()) {
        authority = EncodeRtspUserInfo(config.username);
        if (!config.password.empty()) {
            authority += ":" + EncodeRtspUserInfo(config.password);
        }
        authority += "@";
    }

    return "rtsp://" + authority + NormalizeRtspHost(config.host) + ":" +
        std::to_string(config.port) + "/Streaming/Channels/" +
        std::to_string(config.stream_channel);
}
const std::string& ConfigManager::GetLogDir() const { return m_logDir; }
const std::string& ConfigManager::GetLogLevel() const { return m_logLevel; }
int ConfigManager::GetLogRetentionDays() const { return m_logRetentionDays; }
int ConfigManager::GetLogMaxTotalSizeMb() const { return m_logMaxTotalSizeMb; }
int ConfigManager::GetLogDiskWarningFreeMb() const { return m_logDiskWarningFreeMb; }
int ConfigManager::GetLogFlushIntervalMs() const { return m_logFlushIntervalMs; }
int ConfigManager::GetLogFlushBatchSize() const { return m_logFlushBatchSize; }
bool ConfigManager::HasConfigFile() const { return m_hasConfigFile; }

} // HZCYKJTHardWare 命名空间结束
