using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Parsing
{
    public class OcrCallbackResult
    {
        public string RequestId { get; set; }
        public string Mrz { get; set; }
        public string SavePath { get; set; }
        public List<string> EvidenceImages { get; set; }
        public bool Valid { get; set; }
    }

    public class NfcCallbackResult
    {
        public string RequestId { get; set; }
        public string CardText { get; set; }
        public bool Valid { get; set; }
    }

    public class ImageCallbackResult
    {
        public string RequestId { get; set; }
        public string ResourceType { get; set; }
        public string ImageBase64 { get; set; }
        public string ImageMimeType { get; set; }
        public bool Valid { get; set; }
    }

    public class IrisCallbackResult
    {
        public string RequestId { get; set; }
        public string LeftImageBase64 { get; set; }
        public int LeftWidth { get; set; }
        public int LeftHeight { get; set; }
        public int LeftScore { get; set; }
        public string RightImageBase64 { get; set; }
        public int RightWidth { get; set; }
        public int RightHeight { get; set; }
        public int RightScore { get; set; }
        public string ImageMimeType { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public bool Valid { get; set; }
    }

    public class AuthorizeCallbackResult
    {
        public string RequestId { get; set; }
        public string AuthResult { get; set; }
        public string Message { get; set; }
        public bool Valid { get; set; }
    }

    public static class CallbackParser
    {
        public static string GetResourceType(string bodyUtf8)
        {
            return JsonHelper.ExtractString(bodyUtf8, "resource_type");
        }

        public static OcrCallbackResult ParseOcrDocument(string bodyUtf8)
        {
            var result = new OcrCallbackResult();
            try
            {
                var obj = JObject.Parse(bodyUtf8);
                result.RequestId = obj["request_id"]?.ToString() ?? "";
                result.Mrz = obj["mrz"]?.ToString() ?? "";

                // Search ENTIRE body for MRZ fields (same as Delphi - they might be at any nesting level)
                if (string.IsNullOrEmpty(result.Mrz))
                {
                    // Try uppercase first, then lowercase (same as Delphi)
                    var mrz1 = FindMrzField(bodyUtf8, "MRZ1") ?? FindMrzField(bodyUtf8, "mrz1") ?? "";
                    var mrz2 = FindMrzField(bodyUtf8, "MRZ2") ?? FindMrzField(bodyUtf8, "mrz2") ?? "";
                    var mrz3 = FindMrzField(bodyUtf8, "MRZ3") ?? FindMrzField(bodyUtf8, "mrz3") ?? "";
                    if (!string.IsNullOrEmpty(mrz1) || !string.IsNullOrEmpty(mrz2))
                    {
                        result.Mrz = mrz1 + "^" + mrz2 + "^" + mrz3;
                    }
                }

                // Parse evidence images
                var data = obj["data"] as JObject;
                var evidenceArray = data?["evidence_images"] as JArray ?? obj["evidence_images"] as JArray;
                if (evidenceArray != null)
                {
                    result.EvidenceImages = new List<string>();
                    foreach (var item in evidenceArray)
                        result.EvidenceImages.Add(item.ToString(Newtonsoft.Json.Formatting.None));
                }

                // Always valid if we got a request_id (same as Delphi)
                result.Valid = !string.IsNullOrEmpty(result.RequestId);
            }
            catch
            {
                result.Valid = false;
            }
            return result;
        }

        private static string FindMrzField(string json, string fieldName)
        {
            // Simple string search for field value (same approach as Delphi's ExtractField)
            var searchKey = "\"" + fieldName + "\"";
            var idx = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += searchKey.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t'))
                idx++;
            if (idx >= json.Length || json[idx] != '"') return null;
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

        public static NfcCallbackResult ParseNfcCard(string bodyUtf8)
        {
            var result = new NfcCallbackResult();
            try
            {
                var obj = JObject.Parse(bodyUtf8);
                result.RequestId = obj["request_id"]?.ToString() ?? "";
                result.CardText = obj["card_text"]?.ToString()
                    ?? obj["card_id"]?.ToString()
                    ?? obj["cardId"]?.ToString()
                    ?? obj["id_number"]?.ToString()
                    ?? "";
                // Also try inside data sub-object (same as Delphi)
                if (string.IsNullOrEmpty(result.CardText))
                {
                    var data = obj["data"];
                    if (data != null)
                    {
                        result.CardText = data["card_text"]?.ToString()
                            ?? data["card_id"]?.ToString()
                            ?? data["cardId"]?.ToString()
                            ?? data["id_number"]?.ToString()
                            ?? "";
                    }
                }
                // Valid only if CardText is found (same as Delphi)
                result.Valid = !string.IsNullOrEmpty(result.CardText);
            }
            catch
            {
                result.Valid = false;
            }
            return result;
        }

        public static ImageCallbackResult ParseImageCapture(string bodyUtf8, string resourceType)
        {
            var result = new ImageCallbackResult { ResourceType = resourceType };
            try
            {
                var obj = JObject.Parse(bodyUtf8);
                result.RequestId = obj["request_id"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(result.ResourceType))
                    result.ResourceType = obj["resource_type"]?.ToString() ?? "";

                // Extract data section (same as Delphi)
                var data = obj["data"] as JObject;

                // Try resource-specific field names in data section (same order as Delphi)
                if (result.ResourceType == "face_image")
                {
                    result.ImageBase64 = GetStringField(data, "face_capture")
                        ?? GetStringField(obj, "face_capture")
                        ?? GetStringField(data, "image_base64")
                        ?? GetStringField(obj, "image_base64")
                        ?? "";
                }
                else if (result.ResourceType == "fingerprint_image")
                {
                    result.ImageBase64 = GetStringField(data, "image_base64")
                        ?? GetStringField(obj, "image_base64")
                        ?? GetStringField(data, "fingerprint_capture")
                        ?? GetStringField(obj, "fingerprint_capture")
                        ?? "";
                }
                else if (result.ResourceType == "iris_image")
                {
                    result.ImageBase64 = GetStringField(data, "leftIris_capture")
                        ?? GetStringField(obj, "leftIris_capture")
                        ?? "";
                }
                else
                {
                    // Unknown resource type, try common field names
                    result.ImageBase64 = GetStringField(data, "image_base64")
                        ?? GetStringField(obj, "image_base64")
                        ?? GetStringField(data, "face_capture")
                        ?? GetStringField(obj, "face_capture")
                        ?? GetStringField(data, "fingerprint_capture")
                        ?? GetStringField(obj, "fingerprint_capture")
                        ?? "";
                }

                // MIME type (same as Delphi)
                result.ImageMimeType = GetStringField(data, "image_mime_type")
                    ?? GetStringField(obj, "image_mime_type")
                    ?? GetStringField(data, "mime_type")
                    ?? GetStringField(obj, "mime_type")
                    ?? "";
                if (string.IsNullOrEmpty(result.ImageMimeType))
                {
                    result.ImageMimeType = (result.ResourceType == "fingerprint_image")
                        ? "image/jpeg" : "image/bmp";
                }

                // Valid only if image data found (same as Delphi)
                result.Valid = !string.IsNullOrEmpty(result.ImageBase64);
            }
            catch
            {
                result.Valid = false;
            }
            return result;
        }

        public static IrisCallbackResult ParseIrisCapture(string bodyUtf8)
        {
            var result = new IrisCallbackResult();
            try
            {
                var obj = JObject.Parse(bodyUtf8);
                var data = obj["data"] as JObject;

                result.RequestId = obj["request_id"]?.ToString() ?? "";
                result.LeftImageBase64 = GetStringField(data, "leftIris_capture")
                    ?? GetStringField(data, "left_eye_image_base64")
                    ?? GetStringField(data, "image_base64")
                    ?? "";
                result.RightImageBase64 = GetStringField(data, "rightIris_capture")
                    ?? GetStringField(data, "right_eye_image_base64")
                    ?? "";
                result.LeftWidth = GetIntField(data, "leftIris_width", "left_eye_width");
                result.LeftHeight = GetIntField(data, "leftIris_height", "left_eye_height");
                result.LeftScore = GetIntField(data, "leftIris_score", "left_eye_quality");
                result.RightWidth = GetIntField(data, "rightIris_width", "right_eye_width");
                result.RightHeight = GetIntField(data, "rightIris_height", "right_eye_height");
                result.RightScore = GetIntField(data, "rightIris_score", "right_eye_quality");
                result.ImageMimeType = GetStringField(data, "image_mime_type")
                    ?? GetStringField(obj, "image_mime_type")
                    ?? "image/bmp";
                result.ErrorCode = obj["error_code"]?.ToString()
                    ?? obj["code"]?.ToString()
                    ?? "";
                result.Message = obj["message"]?.ToString() ?? "";
                result.Valid = !string.IsNullOrEmpty(result.RequestId) &&
                    (!string.IsNullOrEmpty(result.ErrorCode) ||
                     !string.IsNullOrEmpty(result.LeftImageBase64) ||
                     !string.IsNullOrEmpty(result.RightImageBase64));
            }
            catch
            {
                result.Valid = false;
            }
            return result;
        }

        private static string GetStringField(JObject obj, string key)
        {
            if (obj == null) return null;
            var token = obj[key];
            if (token == null) return null;
            if (token.Type == JTokenType.String) return token.ToString();
            return null;
        }

        private static int GetIntField(JObject obj, params string[] keys)
        {
            if (obj == null || keys == null) return 0;
            foreach (var key in keys)
            {
                var token = obj[key];
                if (token == null) continue;
                if (token.Type == JTokenType.Integer) return token.Value<int>();
                if (int.TryParse(token.ToString(), out var value)) return value;
            }
            return 0;
        }

        public static AuthorizeCallbackResult ParseAuthorize(string bodyUtf8)
        {
            var result = new AuthorizeCallbackResult();
            try
            {
                var obj = JObject.Parse(bodyUtf8);
                result.RequestId = obj["request_id"]?.ToString() ?? "";
                result.AuthResult = obj["auth_result"]?.ToString()?.ToLower() ?? "";
                result.Message = obj["message"]?.ToString() ?? "";
                result.Valid = !string.IsNullOrEmpty(result.RequestId);
            }
            catch
            {
                result.Valid = false;
            }
            return result;
        }

        public static List<string> ParseEvidenceImages(string bodyUtf8)
        {
            var images = new List<string>();
            try
            {
                var obj = JObject.Parse(bodyUtf8);
                // Protocol: evidence_images is inside data section (same as Delphi/C++ behavior)
                var data = obj["data"] as JObject;
                var arr = data?["evidence_images"] as JArray ?? obj["evidence_images"] as JArray;
                if (arr != null)
                {
                    foreach (var item in arr)
                        images.Add(item.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
            catch { }
            return images;
        }
    }
}
