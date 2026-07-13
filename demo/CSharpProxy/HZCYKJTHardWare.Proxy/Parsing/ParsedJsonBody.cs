using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Parsing
{
    /// <summary>
    /// Owns one parsed representation of an incoming JSON body. Normal requests
    /// reuse Root for every field lookup; malformed input keeps the historical
    /// best-effort string fallback without attempting to parse again.
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
                    // Callers preserve their existing missing/invalid-field result.
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
    }
}
