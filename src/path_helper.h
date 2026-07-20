#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 路径辅助模块：获取 DLL 所在目录、路径拼接、目录创建等
class PathHelper {
public:
    // 获取当前 DLL 所在目录（不含尾斜杠）
    static std::string GetDllDir(HMODULE hModule = nullptr);

    // 路径拼接（处理斜杠）
    static std::string Join(const std::string& base, const std::string& sub);

    // 创建目录（递归，支持中文路径）
    static bool CreateDirectoryRecursive(const std::string& path);

    // 检查目录是否存在
    static bool DirectoryExists(const std::string& path);

    // 检查文件是否存在
    static bool FileExists(const std::string& path);

    // 获取当前时间字符串 yyyyMMdd
    static std::string GetDateString();

    // 获取当前时间字符串 yyyyMMddHHmmssfff
    static std::string GetTimestampString();

    // 获取当前时间字符串 HHmmss_fff
    static std::string GetTimeString();

    // 从路径中提取文件名
    static std::string GetFileName(const std::string& path);

    // 获取父目录
    static std::string GetParentDir(const std::string& path);

    // 转换为 UTF-8 字符串（从宽字符）
    static std::string WideToUtf8(const std::wstring& wstr);

    // 转换为宽字符串
    static std::wstring Utf8ToWide(const std::string& str);

    // 将 DLL 外部 char* 输入归一化为 UTF-8。
    // encodingMode 支持 auto / gbk / utf8；auto 先严格校验 UTF-8，失败后按 CP936 转换。
    static bool NormalizeExternalTextToUtf8(const char* value,
                                            const std::string& encodingMode,
                                            std::string& result);

private:
    PathHelper() = default;
};

} // HZCYKJTHardWare 命名空间结束
