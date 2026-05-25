#include "pch.h"
#include "base64.h"

namespace HZCYKJTHardWare {

static const char kBase64Chars[] =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

static inline bool IsBase64Char(char c) {
    return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
           (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=';
}

std::vector<unsigned char> Base64::Decode(const std::string& encoded) {
    std::vector<unsigned char> result;
    if (encoded.empty()) return result;

    std::string input;
    input.reserve(encoded.size());
    for (unsigned char c : encoded) {
        if (IsBase64Char((char)c)) {
            input.push_back((char)c);
        }
    }
    if (input.empty()) return result;

    // 构建解码表
    static int decodeTable[256];
    static bool tableInit = false;
    if (!tableInit) {
        for (int i = 0; i < 256; i++) decodeTable[i] = -1;
        for (int i = 0; i < 64; i++) decodeTable[(unsigned char)kBase64Chars[i]] = i;
        tableInit = true;
    }

    size_t inLen = input.size();
    // 跳过末尾的 '='
    size_t validLen = inLen;
    while (validLen > 0 && input[validLen - 1] == '=') validLen--;

    result.reserve(validLen * 3 / 4 + 4);

    for (size_t i = 0; i < validLen; i += 4) {
        int b0 = (i < validLen) ? decodeTable[(unsigned char)input[i]] : 0;
        int b1 = (i + 1 < validLen) ? decodeTable[(unsigned char)input[i + 1]] : 0;
        int b2 = (i + 2 < validLen) ? decodeTable[(unsigned char)input[i + 2]] : 0;
        int b3 = (i + 3 < validLen) ? decodeTable[(unsigned char)input[i + 3]] : 0;

        unsigned char out0 = (b0 << 2) | (b1 >> 4);
        unsigned char out1 = (b1 << 4) | (b2 >> 2);
        unsigned char out2 = (b2 << 6) | b3;

        result.push_back(out0);
        if (i + 2 < validLen) result.push_back(out1);
        if (i + 3 < validLen) result.push_back(out2);
    }

    return result;
}

std::string Base64::Encode(const std::vector<unsigned char>& data) {
    return Encode(data.data(), data.size());
}

std::string Base64::Encode(const unsigned char* data, size_t len) {
    std::string result;
    result.reserve((len + 2) / 3 * 4);

    for (size_t i = 0; i < len; i += 3) {
        unsigned char b0 = data[i];
        unsigned char b1 = (i + 1 < len) ? data[i + 1] : 0;
        unsigned char b2 = (i + 2 < len) ? data[i + 2] : 0;

        result.push_back(kBase64Chars[b0 >> 2]);
        result.push_back(kBase64Chars[((b0 & 0x03) << 4) | (b1 >> 4)]);
        result.push_back((i + 1 < len) ? kBase64Chars[((b1 & 0x0F) << 2) | (b2 >> 6)] : '=');
        result.push_back((i + 2 < len) ? kBase64Chars[b2 & 0x3F] : '=');
    }

    return result;
}

} // namespace HZCYKJTHardWare
