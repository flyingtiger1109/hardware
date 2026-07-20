using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Parsing
{
    /// <summary>
    /// 保存请求 JSON 正文的一次解析结果。正常请求的字段查询复用 Root；
    /// 格式异常时保留原有的尽力字符串提取逻辑，不重复解析。
    /// </summary>
    internal sealed class ParsedJsonBody
    {
        private ParsedJsonBody(string rawBody, JObject root)
        {
            RawBody = rawBody ?? "";
            Root = root;
        }

        internal string RawBody { get; }

        internal JObject Root { get; }

        internal bool IsValid => Root != null;

        internal static ParsedJsonBody Parse(string rawBody)
        {
            var normalized = rawBody ?? "";
            JObject root = null;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                try
                {
                    root = JObject.Parse(normalized);
                }
                catch
                {
                    // 调用方沿用字段缺失或无效时的原有处理结果
                }
            }

            return new ParsedJsonBody(normalized, root);
        }

        internal string GetString(string key)
        {
            return Root != null
                ? JsonHelper.ExtractString(Root, key)
                : JsonHelper.ExtractStringManual(RawBody, key);
        }

        internal int GetInt(string key)
        {
            return Root != null ? JsonHelper.ExtractInt(Root, key) : 0;
        }

        internal long GetInt64(string key)
        {
            return Root != null ? JsonHelper.ExtractInt64(Root, key) : 0;
        }
    }
}
