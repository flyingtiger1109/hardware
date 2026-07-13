using Newtonsoft.Json.Linq;
using System.Text;

namespace HZCYKJTHardWare.Proxy.Parsing
{
    public static class JsonHelper
    {
        public static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            try
            {
                var obj = JObject.Parse(json);
                return ExtractString(obj, key);
            }
            catch
            {
                // Fallback: manual extraction for malformed JSON
                return ExtractStringManual(json, key);
            }
        }

        public static int ExtractInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return 0;
            try
            {
                var obj = JObject.Parse(json);
                return ExtractInt(obj, key);
            }
            catch
            {
                return 0;
            }
        }

        public static string ExtractObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            try
            {
                var obj = JObject.Parse(json);
                var token = obj[key];
                return token?.ToString(Newtonsoft.Json.Formatting.None) ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static string ExtractArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            try
            {
                var obj = JObject.Parse(json);
                var token = obj[key] as JArray;
                return token?.ToString(Newtonsoft.Json.Formatting.None) ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        public static string ToLogValue(string value, int maxLength = 256)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (maxLength <= 0) maxLength = 256;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch == '\r' || ch == '\n' || ch == '\t' || char.IsControl(ch))
                    sb.Append(' ');
                else
                    sb.Append(ch);

                if (sb.Length >= maxLength)
                    break;
            }

            return value.Length > maxLength ? sb.ToString() + "..." : sb.ToString();
        }

        public static string JsonStr(string name, string value)
        {
            return $"\"{EscapeString(name)}\":\"{EscapeString(value)}\"";
        }

        public static string JsonInt(string name, long value)
        {
            return $"\"{EscapeString(name)}\":{value}";
        }

        public static string JsonBool(string name, bool value)
        {
            return $"\"{EscapeString(name)}\":{(value ? "true" : "false")}";
        }

        internal static string ExtractString(JObject obj, string key)
        {
            if (obj == null || string.IsNullOrEmpty(key)) return "";
            var token = obj[key];
            return token?.ToString() ?? "";
        }

        internal static int ExtractInt(JObject obj, string key)
        {
            if (obj == null || string.IsNullOrEmpty(key)) return 0;
            try
            {
                var token = obj[key];
                if (token == null) return 0;
                if (token.Type == JTokenType.Integer) return token.Value<int>();
                int.TryParse(token.ToString(), out int result);
                return result;
            }
            catch
            {
                return 0;
            }
        }

        internal static string ExtractStringManual(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            var searchKey = "\"" + key + "\"";
            var idx = json.IndexOf(searchKey);
            if (idx < 0) return "";
            idx += searchKey.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t'))
                idx++;
            if (idx >= json.Length || json[idx] != '"') return "";
            idx++;
            var start = idx;
            while (idx < json.Length)
            {
                if (json[idx] == '\\') { idx += 2; continue; }
                if (json[idx] == '"') break;
                idx++;
            }
            return json.Substring(start, idx - start);
        }
    }
}
