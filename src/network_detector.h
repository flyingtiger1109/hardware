#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 网络检测结果
struct NetworkInfo {
    std::string selected_ip;
    std::string selected_subnet_prefix;
    std::vector<std::string> candidates;
    std::string terminal_1_url;
    std::string terminal_2_url;
    std::string callback_host;
    int callback_port = 39091;
};

// 网络检测模块
class NetworkDetector {
public:
    // 检测本机 192.168 网段
    // preferredSubnet 为空时自动选择；多候选时返回 MULTI_NIC_NEED_CONFIG
    int Detect(const std::string& preferredSubnet);

    // 获取检测的网络信息（JSON 格式）
    std::string GetNetworkInfoJson(int callbackPort) const;

    // 获取选中的本机 IP
    const std::string& GetSelectedIp() const;
    const std::string& GetSelectedSubnetPrefix() const;

    // 获取候选 IP 列表
    const std::vector<std::string>& GetCandidates() const;

private:
    // 枚举本机所有 IPv4 地址
    std::vector<std::string> EnumerateIPv4Addresses();

    // 过滤地址
    bool IsValidAddress(const std::string& ip);

    // 是否为 192.168.x.x
    bool Is192168(const std::string& ip);

    // 提取子网前缀（如 192.168.1）
    std::string GetSubnetPrefix(const std::string& ip);

    std::string m_selectedIp;
    std::string m_selectedSubnetPrefix;
    std::vector<std::string> m_candidates;
};

} // namespace HZCYKJTHardWare
