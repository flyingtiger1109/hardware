#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// Base64 编解码
class Base64 {
public:
    // Base64 解码，返回解码后的二进制数据
    static std::vector<unsigned char> Decode(const std::string& encoded);

    // Base64 编码
    static std::string Encode(const std::vector<unsigned char>& data);
    static std::string Encode(const unsigned char* data, size_t len);
};

} // HZCYKJTHardWare 命名空间结束
