#include "pch.h"
#include "network_detector.h"
#include "include/HZCYKJTHardWare_types.h"
#include "logger.h"
#include "json_helper.h"
#include <iphlpapi.h>

namespace HZCYKJTHardWare {

std::vector<std::string> NetworkDetector::EnumerateIPv4Addresses() {
    std::vector<std::string> result;

    ULONG bufLen = 15000;
    std::vector<unsigned char> buffer(bufLen);
    PIP_ADAPTER_ADDRESSES pAddresses = nullptr;

    ULONG ret = GetAdaptersAddresses(AF_INET,
        GAA_FLAG_INCLUDE_PREFIX | GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST,
        nullptr, (PIP_ADAPTER_ADDRESSES)buffer.data(), &bufLen);

    if (ret == ERROR_BUFFER_OVERFLOW) {
        buffer.resize(bufLen);
        ret = GetAdaptersAddresses(AF_INET,
            GAA_FLAG_INCLUDE_PREFIX | GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST,
            nullptr, (PIP_ADAPTER_ADDRESSES)buffer.data(), &bufLen);
    }

    if (ret != NO_ERROR) {
        LOG_ERROR("NetDetector", "网卡枚举失败：GetAdaptersAddresses error=%lu", ret);
        return result;
    }

    pAddresses = (PIP_ADAPTER_ADDRESSES)buffer.data();
    while (pAddresses) {
        PIP_ADAPTER_UNICAST_ADDRESS pUnicast = pAddresses->FirstUnicastAddress;
        while (pUnicast) {
            if (pUnicast->Address.lpSockaddr->sa_family == AF_INET) {
                sockaddr_in* addr = (sockaddr_in*)pUnicast->Address.lpSockaddr;
                char ipStr[INET_ADDRSTRLEN];
                inet_ntop(AF_INET, &addr->sin_addr, ipStr, sizeof(ipStr));
                std::string ip(ipStr);
                LOG_DEBUG("NetDetector", "发现网卡地址：adapter=%s，ip=%s",
                          pAddresses->FriendlyName ? "(unknown)" : "", ip.c_str());
                result.push_back(ip);
            }
            pUnicast = pUnicast->Next;
        }
        pAddresses = pAddresses->Next;
    }

    return result;
}

bool NetworkDetector::IsValidAddress(const std::string& ip) {
    if (ip == "127.0.0.1") return false;
    if (ip == "0.0.0.0") return false;
    if (ip.find("169.254.") == 0) return false;
    return true;
}

bool NetworkDetector::Is192168(const std::string& ip) {
    return ip.find("192.168.") == 0;
}

std::string NetworkDetector::GetSubnetPrefix(const std::string& ip) {
    size_t lastDot = ip.find_last_of('.');
    if (lastDot == std::string::npos) return "";
    return ip.substr(0, lastDot);
}

int NetworkDetector::Detect(const std::string& preferredSubnet) {
    m_selectedIp.clear();
    m_selectedSubnetPrefix.clear();
    m_candidates.clear();

    auto allIps = EnumerateIPv4Addresses();

    // 过滤并收集候选
    std::vector<std::string> valid192Ips;
    for (const auto& ip : allIps) {
        if (!IsValidAddress(ip)) continue;
        if (Is192168(ip)) {
            valid192Ips.push_back(ip);
            m_candidates.push_back(ip);
        }
    }

    LOG_DEBUG("NetDetector", "网卡检测结果：IPv4总数=%zu，192.168有效地址=%zu",
             allIps.size(), valid192Ips.size());
    for (const auto& ip : valid192Ips) {
        LOG_DEBUG("NetDetector", "192.168可选地址：ip=%s", ip.c_str());
    }

    if (valid192Ips.empty()) {
        LOG_ERROR("NetDetector", "网卡检测失败：未找到有效 192.168.x.x 地址");
        return HZCYKJTHardWare_RET_SUBNET_DETECT_FAILED;
    }

    // 如果指定了 preferredSubnet
    if (!preferredSubnet.empty()) {
        LOG_DEBUG("NetDetector", "使用配置网段：preferred_subnet_prefix=%s", preferredSubnet.c_str());
        for (const auto& ip : valid192Ips) {
            std::string prefix = GetSubnetPrefix(ip);
            if (prefix == preferredSubnet) {
                m_selectedIp = ip;
                m_selectedSubnetPrefix = prefix;
                LOG_DEBUG("网络检测", "本机网卡已选择：ip=%s，subnet=%s",
                         ip.c_str(), prefix.c_str());
                return HZCYKJTHardWare_RET_OK;
            }
        }
        LOG_WARN("NetDetector", "配置网段未匹配，降级自动选择，preferred_subnet_prefix=%s", preferredSubnet.c_str());
        // 继续执行自动选择逻辑
    }

    // 只有一个 192.168 地址时自动选择
    if (valid192Ips.size() == 1) {
        m_selectedIp = valid192Ips[0];
        m_selectedSubnetPrefix = GetSubnetPrefix(valid192Ips[0]);
        LOG_DEBUG("网络检测", "本机网卡已自动选择：ip=%s", m_selectedIp.c_str());
        return HZCYKJTHardWare_RET_OK;
    }

    // 多个 192.168 地址，需要配置
    LOG_WARN("NetDetector", "检测到多个 192.168 网卡地址，需要配置 preferred_subnet_prefix");
    for (const auto& ip : valid192Ips) {
        LOG_WARN("NetDetector", "可选网卡：ip=%s，subnet=%s", ip.c_str(), GetSubnetPrefix(ip).c_str());
    }
    return HZCYKJTHardWare_RET_MULTI_NIC_NEED_CONFIG;
}

std::string NetworkDetector::GetNetworkInfoJson(int callbackPort) const {
    std::string json = "{\n";
    json += "  \"selected_ip\": \"" + m_selectedIp + "\",\n";
    json += "  \"selected_subnet_prefix\": \"" + m_selectedSubnetPrefix + "\",\n";
    json += "  \"candidates\": [";
    for (size_t i = 0; i < m_candidates.size(); i++) {
        if (i > 0) json += ", ";
        json += "\"" + m_candidates[i] + "\"";
    }
    json += "],\n";

    // 拼接终端 URL
    if (!m_selectedSubnetPrefix.empty()) {
        json += "  \"terminal_1_url\": \"http://" + m_selectedSubnetPrefix + ".10:8080\",\n";
        json += "  \"terminal_2_url\": \"http://" + m_selectedSubnetPrefix + ".11:8080\",\n";
    } else {
        json += "  \"terminal_1_url\": \"\",\n";
        json += "  \"terminal_2_url\": \"\",\n";
    }

    json += "  \"callback_host\": \"" + m_selectedIp + "\",\n";
    json += "  \"callback_port\": " + std::to_string(callbackPort) + "\n";
    json += "}";
    return json;
}

const std::string& NetworkDetector::GetSelectedIp() const { return m_selectedIp; }
const std::string& NetworkDetector::GetSelectedSubnetPrefix() const { return m_selectedSubnetPrefix; }
const std::vector<std::string>& NetworkDetector::GetCandidates() const { return m_candidates; }

} // HZCYKJTHardWare 命名空间结束
