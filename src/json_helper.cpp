#include "pch.h"
#include "json_helper.h"

namespace HZCYKJTHardWare {

size_t JsonHelper::FindKey(const std::string& json, const std::string& key) {
    std::string searchKey = "\"" + key + "\"";
    size_t pos = 0;
    bool inString = false;

    while (pos < json.size()) {
        if (json[pos] == '"' && (pos == 0 || json[pos - 1] != '\\')) {
            if (!inString) {
                // 检查是否匹配搜索 key
                if (json.compare(pos, searchKey.size(), searchKey) == 0) {
                    // 确认后面跟着 ':'
                    size_t after = pos + searchKey.size();
                    while (after < json.size() && (json[after] == ' ' || json[after] == '\t' || json[after] == '\n' || json[after] == '\r'))
                        after++;
                    if (after < json.size() && json[after] == ':') {
                        return after + 1; // 返回 ':' 后面的位置
                    }
                }
            }
            inString = !inString;
        }
        pos++;
    }
    return std::string::npos;
}

std::string JsonHelper::ExtractStringValue(const std::string& json, size_t pos) {
    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\n' || json[pos] == '\r'))
        pos++;

    if (pos >= json.size()) return "";

    // 跳过第一个引号
    if (json[pos] == '"') {
        pos++;
        std::string value;
        while (pos < json.size()) {
            if (json[pos] == '\\' && pos + 1 < json.size()) {
                pos++;
                switch (json[pos]) {
                    case '"': value += '"'; break;
                    case '\\': value += '\\'; break;
                    case '/': value += '/'; break;
                    case 'n': value += '\n'; break;
                    case 'r': value += '\r'; break;
                    case 't': value += '\t'; break;
                    default: value += json[pos]; break;
                }
                pos++;
            } else if (json[pos] == '"') {
                return value;
            } else {
                value += json[pos];
                pos++;
            }
        }
    }

    return "";
}

std::string JsonHelper::GetString(const std::string& json, const std::string& key) {
    size_t pos = FindKey(json, key);
    if (pos == std::string::npos) return "";
    return ExtractStringValue(json, pos);
}

int JsonHelper::GetInt(const std::string& json, const std::string& key, int defaultVal) {
    std::string s = GetString(json, key);
    if (s.empty()) {
        // 尝试数字值（不带引号）
        size_t pos = FindKey(json, key);
        if (pos == std::string::npos) return defaultVal;
        while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t')) pos++;
        if (pos >= json.size()) return defaultVal;
        // 读取数字
        std::string num;
        while (pos < json.size() && (isdigit((unsigned char)json[pos]) || json[pos] == '-')) {
            num += json[pos];
            pos++;
        }
        if (!num.empty()) {
            return atoi(num.c_str());
        }
        return defaultVal;
    }
    return atoi(s.c_str());
}

double JsonHelper::GetDouble(const std::string& json, const std::string& key, double defaultVal) {
    std::string s = GetString(json, key);
    if (s.empty()) {
        size_t pos = FindKey(json, key);
        if (pos == std::string::npos) return defaultVal;
        while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t')) pos++;
        if (pos >= json.size()) return defaultVal;
        std::string num;
        while (pos < json.size() && (isdigit((unsigned char)json[pos]) || json[pos] == '-' || json[pos] == '.')) {
            num += json[pos];
            pos++;
        }
        if (!num.empty()) return atof(num.c_str());
        return defaultVal;
    }
    return atof(s.c_str());
}

bool JsonHelper::GetBool(const std::string& json, const std::string& key, bool defaultVal) {
    std::string s = GetString(json, key);
    if (s == "true" || s == "1") return true;
    if (s == "false" || s == "0") return false;

    // 尝试不带引号的值
    size_t pos = FindKey(json, key);
    if (pos == std::string::npos) return defaultVal;
    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t')) pos++;
    if (pos + 4 <= json.size() && json.compare(pos, 4, "true") == 0) return true;
    if (pos + 5 <= json.size() && json.compare(pos, 5, "false") == 0) return false;
    return defaultVal;
}

bool JsonHelper::HasKey(const std::string& json, const std::string& key) {
    return FindKey(json, key) != std::string::npos;
}

std::string JsonHelper::GetJsonObject(const std::string& json, const std::string& key) {
    size_t pos = FindKey(json, key);
    if (pos == std::string::npos) return "";

    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\n' || json[pos] == '\r'))
        pos++;

    if (pos >= json.size() || json[pos] != '{') return "";

    int depth = 0;
    size_t start = pos;
    while (pos < json.size()) {
        if (json[pos] == '{') depth++;
        else if (json[pos] == '}') {
            depth--;
            if (depth == 0) {
                return json.substr(start, pos - start + 1);
            }
        } else if (json[pos] == '"') {
            // 跳过字符串
            pos++;
            while (pos < json.size()) {
                if (json[pos] == '\\' && pos + 1 < json.size()) {
                    pos += 2; continue;
                }
                if (json[pos] == '"') break;
                pos++;
            }
        }
        pos++;
    }
    return "";
}

std::string JsonHelper::GetArray(const std::string& json, const std::string& key) {
    size_t pos = FindKey(json, key);
    if (pos == std::string::npos) return "";

    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\n' || json[pos] == '\r'))
        pos++;

    if (pos >= json.size() || json[pos] != '[') return "";

    int depth = 0;
    size_t start = pos;
    while (pos < json.size()) {
        if (json[pos] == '[') depth++;
        else if (json[pos] == ']') {
            depth--;
            if (depth == 0) {
                return json.substr(start, pos - start + 1);
            }
        } else if (json[pos] == '"') {
            pos++;
            while (pos < json.size()) {
                if (json[pos] == '\\' && pos + 1 < json.size()) {
                    pos += 2; continue;
                }
                if (json[pos] == '"') break;
                pos++;
            }
        }
        pos++;
    }
    return "";
}

std::string JsonHelper::BuildJson(const std::map<std::string, std::string>& fields) {
    std::string s = "{";
    bool first = true;
    for (const auto& kv : fields) {
        if (!first) s += ",";
        s += "\"" + kv.first + "\":\"" + kv.second + "\"";
        first = false;
    }
    s += "}";
    return s;
}

} // HZCYKJTHardWare 命名空间结束
