#include "pch.h"
#include "config_manager.h"
#include "include/HZCYKJTHardWare_types.h"
#include "logger.h"
#include "path_helper.h"
#include "json_helper.h"

namespace HZCYKJTHardWare {

int ConfigManager::Load(const std::string& dllDir) {
    // 始终先填充默认值，后续由 JSON 文件中存在的字段覆盖
    ApplyDefaults();

    std::string configPath = PathHelper::Join(dllDir, "HZCYKJTHardWare.json");

    if (!PathHelper::FileExists(configPath)) {
        m_hasConfigFile = false;
        LOG_WARN("ConfigManager", "未找到 HZCYKJTHardWare.json，使用默认配置：path=%s", configPath.c_str());
        LOG_DEBUG("ConfigManager", "默认配置：terminal.preferred_subnet_prefix=%s",
                 m_preferredSubnetPrefix.empty() ? "(empty)" : m_preferredSubnetPrefix.c_str());
        return HZCYKJTHardWare_RET_OK; // 不存在但可使用默认配置
    }

    // 读取文件内容
    std::wstring wConfigPath = PathHelper::Utf8ToWide(configPath);
    std::ifstream file(wConfigPath, std::ios::in | std::ios::binary);
    if (!file.is_open()) {
        LOG_ERROR("ConfigManager", "打开 HZCYKJTHardWare.json 失败：path=%s", configPath.c_str());
        return HZCYKJTHardWare_RET_CONFIG_INVALID;
    }

    std::stringstream ss;
    ss << file.rdbuf();
    std::string json = ss.str();
    file.close();

    m_hasConfigFile = true;

    int ret = ParseJson(json);
    if (ret != HZCYKJTHardWare_RET_OK) {
        return ret;
    }

    LOG_INFO("ConfigManager", "配置文件加载成功：path=%s", configPath.c_str());
    LOG_DEBUG("ConfigManager", "配置：terminal.mode=%s",
             m_mode == TerminalMode::AutoSubnet ? "auto_subnet" :
             (m_mode == TerminalMode::FixedUrl ? "fixed_url" : "manual"));
    LOG_DEBUG("ConfigManager", "配置：callback_server.auto_bind_lan_ip=%s", m_autoBindLanIp ? "true" : "false");

    return HZCYKJTHardWare_RET_OK;
}

int ConfigManager::ParseJson(const std::string& json) {
    // delphi_server 配置：新架构下 DLL 只代理到 Delphi 程序。
    std::string delphiObj = JsonHelper::GetJsonObject(json, "delphi_server");
    if (!delphiObj.empty()) {
        if (JsonHelper::HasKey(delphiObj, "host"))
            m_delphiServerHost = JsonHelper::GetString(delphiObj, "host");
        if (JsonHelper::HasKey(delphiObj, "port"))
            m_delphiServerPort = JsonHelper::GetInt(delphiObj, "port", 8080);
    }
    if (m_delphiServerHost.empty()) {
        m_delphiServerHost = "127.0.0.1";
    }
    if (m_delphiServerPort <= 0 || m_delphiServerPort > 65535) {
        m_delphiServerPort = 8080;
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
    }

    // save 配置
    std::string saveObj = JsonHelper::GetJsonObject(json, "save");
    if (!saveObj.empty()) {
        if (JsonHelper::HasKey(saveObj, "default_root"))
            m_saveDefaultRoot = JsonHelper::GetString(saveObj, "default_root");
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
    }

    // log 配置
    std::string logObj = JsonHelper::GetJsonObject(json, "log");
    if (!logObj.empty()) {
        if (JsonHelper::HasKey(logObj, "dir"))
            m_logDir = JsonHelper::GetString(logObj, "dir");
        if (JsonHelper::HasKey(logObj, "level"))
            m_logLevel = JsonHelper::GetString(logObj, "level");
    }

    return HZCYKJTHardWare_RET_OK;
}

void ConfigManager::ApplyDefaults() {
    m_mode = TerminalMode::AutoSubnet;
    m_scheme = "http";
    m_port = 9098;
    m_checkOnInit = false;
    m_defaultIndex = 1;
    m_preferredSubnetPrefix = "192.168.20";

    m_delphiServerHost = "127.0.0.1";
    m_delphiServerPort = 8080;

    m_fixedTerminals.clear();
    m_fixedTerminals.push_back({1, "terminal_a", ""});
    m_fixedTerminals.push_back({2, "terminal_b", ""});

    m_autoSubnetDevices.clear();
    m_autoSubnetDevices.push_back({1, "terminal_a", "", 30});
    m_autoSubnetDevices.push_back({2, "terminal_b", "", 11});

    m_callbackServerHost.clear();
    m_callbackServerPort = 39091;
    m_autoBindLanIp = true;
    m_listenAny = true;
    m_callbackBasePath = "/HZCYKJTHardWare/callback";

    m_httpConnectTimeoutMs = 3000;
    m_httpRequestTimeoutMs = 3000;
    m_faceCaptureTimeoutMs = 5000;
    m_fingerprintCaptureTimeoutMs = 5000;
    m_ocrTimeoutMs = 10000;

    m_saveDefaultRoot = ".\\captures";
    m_createDateFolder = true;
    m_createRequestFolder = true;

    m_previewRenderer = "libvlc";
    m_autoReconnect = true;
    m_checkHwndIntervalMs = 500;
    m_stopPreviewOnEndProcess = false;
    m_rtspNetworkCachingMs = 150;
    m_rtspLiveCachingMs = 150;
    m_rtspTransport = "tcp";

    m_logDir = "HZCYKJTHardWare_Logs";
    m_logLevel = "info";
}

// 访问器实现
TerminalMode ConfigManager::GetTerminalMode() const { return m_mode; }
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
const std::string& ConfigManager::GetSaveDefaultRoot() const { return m_saveDefaultRoot; }
bool ConfigManager::GetCreateDateFolder() const { return m_createDateFolder; }
bool ConfigManager::GetCreateRequestFolder() const { return m_createRequestFolder; }
const std::string& ConfigManager::GetPreviewRenderer() const { return m_previewRenderer; }
bool ConfigManager::GetAutoReconnect() const { return m_autoReconnect; }
int ConfigManager::GetCheckHwndIntervalMs() const { return m_checkHwndIntervalMs; }
bool ConfigManager::GetStopPreviewOnEndProcess() const { return m_stopPreviewOnEndProcess; }
int ConfigManager::GetRtspNetworkCachingMs() const { return m_rtspNetworkCachingMs; }
int ConfigManager::GetRtspLiveCachingMs() const { return m_rtspLiveCachingMs; }
const std::string& ConfigManager::GetRtspTransport() const { return m_rtspTransport; }
const std::string& ConfigManager::GetLogDir() const { return m_logDir; }
const std::string& ConfigManager::GetLogLevel() const { return m_logLevel; }
bool ConfigManager::HasConfigFile() const { return m_hasConfigFile; }

} // namespace HZCYKJTHardWare
