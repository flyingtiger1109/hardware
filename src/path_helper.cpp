#include "pch.h"
#include "path_helper.h"
#include "hzsjkjt_context.h"
#include <algorithm>
#include <shlobj.h>

namespace HZCYKJTHardWare {

std::string PathHelper::GetDllDir(HMODULE hModule) {
    if (!hModule) {
        // 如果没有传入，从上下文中获取
        return HzsjkjtContext::Instance().dll_dir;
    }
    wchar_t path[MAX_PATH] = {0};
    GetModuleFileNameW(hModule, path, MAX_PATH);
    std::wstring ws(path);
    std::wstring dir = ws.substr(0, ws.find_last_of(L'\\'));
    return WideToUtf8(dir);
}

std::string PathHelper::Join(const std::string& base, const std::string& sub) {
    if (base.empty()) return sub;
    if (sub.empty()) return base;
    if (base.back() == '\\' || base.back() == '/') {
        return base + sub;
    }
    return base + "\\" + sub;
}

bool PathHelper::CreateDirectoryRecursive(const std::string& path) {
    if (path.empty()) return true;
    if (DirectoryExists(path)) return true;

    std::wstring wpath = Utf8ToWide(path);

    // 递归创建父目录
    size_t pos = path.find_last_of("\\/");
    if (pos != std::string::npos) {
        std::string parent = path.substr(0, pos);
        if (!DirectoryExists(parent)) {
            if (!CreateDirectoryRecursive(parent)) {
                return false;
            }
        }
    }

    return CreateDirectoryW(wpath.c_str(), nullptr) != 0 || GetLastError() == ERROR_ALREADY_EXISTS;
}

bool PathHelper::DirectoryExists(const std::string& path) {
    std::wstring wpath = Utf8ToWide(path);
    DWORD attrs = GetFileAttributesW(wpath.c_str());
    return (attrs != INVALID_FILE_ATTRIBUTES && (attrs & FILE_ATTRIBUTE_DIRECTORY));
}

bool PathHelper::FileExists(const std::string& path) {
    std::wstring wpath = Utf8ToWide(path);
    DWORD attrs = GetFileAttributesW(wpath.c_str());
    return (attrs != INVALID_FILE_ATTRIBUTES && !(attrs & FILE_ATTRIBUTE_DIRECTORY));
}

std::string PathHelper::GetDateString() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[16];
    snprintf(buf, sizeof(buf), "%04d%02d%02d", st.wYear, st.wMonth, st.wDay);
    return buf;
}

std::string PathHelper::GetTimestampString() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[32];
    snprintf(buf, sizeof(buf), "%04d%02d%02d%02d%02d%02d%03d",
             st.wYear, st.wMonth, st.wDay,
             st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    return buf;
}

std::string PathHelper::GetTimeString() {
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[16];
    snprintf(buf, sizeof(buf), "%02d%02d%02d_%03d",
             st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    return buf;
}

std::string PathHelper::GetFileName(const std::string& path) {
    size_t pos = path.find_last_of("\\/");
    if (pos != std::string::npos) {
        return path.substr(pos + 1);
    }
    return path;
}

std::string PathHelper::GetParentDir(const std::string& path) {
    size_t pos = path.find_last_of("\\/");
    if (pos != std::string::npos) {
        return path.substr(0, pos);
    }
    return "";
}

std::string PathHelper::WideToUtf8(const std::wstring& wstr) {
    if (wstr.empty()) return "";
    int len = WideCharToMultiByte(CP_UTF8, 0, wstr.c_str(), (int)wstr.size(),
                                   nullptr, 0, nullptr, nullptr);
    if (len <= 0) return "";
    std::string result(len, '\0');
    WideCharToMultiByte(CP_UTF8, 0, wstr.c_str(), (int)wstr.size(),
                        &result[0], len, nullptr, nullptr);
    return result;
}

std::wstring PathHelper::Utf8ToWide(const std::string& str) {
    if (str.empty()) return L"";
    int len = MultiByteToWideChar(CP_UTF8, 0, str.c_str(), (int)str.size(),
                                   nullptr, 0);
    if (len <= 0) return L"";
    std::wstring result(len, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, str.c_str(), (int)str.size(),
                        &result[0], len);
    return result;
}

bool PathHelper::NormalizeExternalTextToUtf8(const char* value,
                                             const std::string& encodingMode,
                                             std::string& result) {
    result = value ? value : "";
    if (result.empty()) return true;

    // ASCII 在 UTF-8 与 GBK 中字节完全一致，不需要分配转换缓冲区。
    const bool asciiOnly = std::all_of(
        result.begin(), result.end(),
        [](unsigned char ch) { return ch <= 0x7F; });
    if (asciiOnly) return true;

    auto isStrictUtf8 = [](const std::string& input) {
        return MultiByteToWideChar(
                   CP_UTF8, MB_ERR_INVALID_CHARS,
                   input.data(), static_cast<int>(input.size()),
                   nullptr, 0) > 0;
    };

    if (encodingMode == "utf8" || encodingMode == "auto") {
        if (isStrictUtf8(result)) return true;
        if (encodingMode == "utf8") {
            result.clear();
            return false;
        }
    }

    if (encodingMode != "gbk" && encodingMode != "auto") {
        result.clear();
        return false;
    }

    const int wideLength = MultiByteToWideChar(
        936, MB_ERR_INVALID_CHARS,
        result.data(), static_cast<int>(result.size()),
        nullptr, 0);
    if (wideLength <= 0) {
        result.clear();
        return false;
    }

    std::wstring wide(static_cast<size_t>(wideLength), L'\0');
    if (MultiByteToWideChar(
            936, MB_ERR_INVALID_CHARS,
            result.data(), static_cast<int>(result.size()),
            wide.data(), wideLength) != wideLength) {
        result.clear();
        return false;
    }

    const int utf8Length = WideCharToMultiByte(
        CP_UTF8, WC_ERR_INVALID_CHARS,
        wide.data(), wideLength,
        nullptr, 0, nullptr, nullptr);
    if (utf8Length <= 0) {
        result.clear();
        return false;
    }

    std::string utf8(static_cast<size_t>(utf8Length), '\0');
    if (WideCharToMultiByte(
            CP_UTF8, WC_ERR_INVALID_CHARS,
            wide.data(), wideLength,
            utf8.data(), utf8Length,
            nullptr, nullptr) != utf8Length) {
        result.clear();
        return false;
    }

    result.swap(utf8);
    return true;
}

} // HZCYKJTHardWare 命名空间结束
