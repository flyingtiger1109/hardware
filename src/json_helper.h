#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 简易 JSON 解析辅助类（不引入第三方 JSON 库，使用手写解析器）
class JsonHelper {
public:
    // 从 JSON 字符串中提取指定 key 的字符串值
    static std::string GetString(const std::string& json, const std::string& key);

    // 从 JSON 字符串中提取指定 key 的整数值
    static int GetInt(const std::string& json, const std::string& key, int defaultVal = 0);

    // 从 JSON 字符串中提取指定 key 的浮点值
    static double GetDouble(const std::string& json, const std::string& key, double defaultVal = 0.0);

    // 从 JSON 字符串中提取指定 key 的布尔值
    static bool GetBool(const std::string& json, const std::string& key, bool defaultVal = false);

    // 检查 JSON 对象中是否存在指定 key
    static bool HasKey(const std::string& json, const std::string& key);

    // 从嵌套 JSON 中提取子对象字符串
    static std::string GetJsonObject(const std::string& json, const std::string& key);

    // 从 JSON 中提取数组的字符串表示
    static std::string GetArray(const std::string& json, const std::string& key);

    // 构建简易 JSON 字符串
    static std::string BuildJson(const std::map<std::string, std::string>& fields);

private:
    // 查找 key 的位置
    static size_t FindKey(const std::string& json, const std::string& key);
    static std::string ExtractStringValue(const std::string& json, size_t pos);
};

} // HZCYKJTHardWare 命名空间结束
