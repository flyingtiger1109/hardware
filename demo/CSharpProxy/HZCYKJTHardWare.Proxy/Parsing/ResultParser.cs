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
            // Try multiple field names
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
    }
}
