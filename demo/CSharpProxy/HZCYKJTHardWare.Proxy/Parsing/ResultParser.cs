namespace HZCYKJTHardWare.Proxy.Parsing
{
    public static class ResultParser
    {
        public static string ExtractSavePath(string responseBody)
        {
            return JsonHelper.ExtractString(responseBody, "save_path");
        }

        public static string ExtractPreviewUrl(string responseBody)
        {
            // 依次尝试多个兼容字段名
            var url = JsonHelper.ExtractString(responseBody, "preview_url");
            if (string.IsNullOrEmpty(url))
                url = JsonHelper.ExtractString(responseBody, "rtsp_url");
            if (string.IsNullOrEmpty(url))
                url = JsonHelper.ExtractString(responseBody, "url");
            return url;
        }

        public static bool IsOkResponse(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody)) return false;
            if (responseBody.Contains("\"error\":true") || responseBody.Contains("\"error\": true"))
                return false;
            return JsonHelper.ExtractString(responseBody, "status") == "ok";
        }

        public static bool IsAcceptedResponse(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody)) return false;
            if (responseBody.Contains("\"error\":true") || responseBody.Contains("\"error\": true"))
                return false;
            return responseBody.Contains("\"accepted\":true") || responseBody.Contains("\"accepted\": true");
        }

        public static bool ResponseSignalsFailure(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody)) return false;
            return responseBody.Contains("\"error\":true")
                || responseBody.Contains("\"success\":false")
                || responseBody.Contains("\"status\":\"error\"")
                || responseBody.Contains("\"status\":\"failed\"")
                || responseBody.Contains("\"status\":\"fail\"");
        }

        public static string ExtractErrorCode(string responseBody)
        {
            var code = JsonHelper.ExtractString(responseBody, "error_code");
            if (string.IsNullOrEmpty(code))
                code = JsonHelper.ExtractString(responseBody, "code");
            return code;
        }

        public static string ExtractErrorMessage(string responseBody)
        {
            var message = JsonHelper.ExtractString(responseBody, "message");
            if (string.IsNullOrEmpty(message))
                message = JsonHelper.ExtractString(responseBody, "msg");
            return message;
        }

        public static string FormatErrorDetail(string responseBody, string fallback)
        {
            var code = ExtractErrorCode(responseBody);
            var message = ExtractErrorMessage(responseBody);

            if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(message))
                return code + ": " + message;
            if (!string.IsNullOrEmpty(message))
                return message;
            if (!string.IsNullOrEmpty(code))
                return code;
            return fallback;
        }
    }
}
