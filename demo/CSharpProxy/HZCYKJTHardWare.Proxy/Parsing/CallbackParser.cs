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
        public int CardType { get; set; } = -1;
        public string Name { get; set; } = "";
        public string Sex { get; set; } = "";
        public string CardId { get; set; } = "";
        public string Birthday { get; set; } = "";
        public string DateOfIssue { get; set; } = "";
        public int AuthenScore { get; set; } = -1;
        public int OpticalCheckResult { get; set; } = -1;
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
        public string SavePath { get; set; }
        public string ImageBase64 { get; set; }
        public string UndistortedImageBase64 { get; set; }
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
            var parsedBody = ParsedJsonBody.Parse(bodyUtf8);
            return ParseOcrDocument(parsedBody.Root, parsedBody.RawBody);
        }

        internal static OcrCallbackResult ParseOcrDocument(JObject obj, string bodyUtf8)
        {
            var result = new OcrCallbackResult();
            try
            {
                if (obj == null)
                    return result;

                result.RequestId = obj["request_id"]?.ToString() ?? "";
                result.Mrz = obj["mrz"]?.ToString() ?? "";

                // 在完整正文中查找 MRZ 字段，与 Delphi 保持一致，兼容任意嵌套层级
                if (string.IsNullOrEmpty(result.Mrz))
                {
                    // 优先查找大写字段名，再查找小写字段名，与 Delphi 顺序一致
                    var mrz1 = FindMrzField(bodyUtf8, "MRZ1") ?? FindMrzField(bodyUtf8, "mrz1") ?? "";
                    var mrz2 = FindMrzField(bodyUtf8, "MRZ2") ?? FindMrzField(bodyUtf8, "mrz2") ?? "";
                    var mrz3 = FindMrzField(bodyUtf8, "MRZ3") ?? FindMrzField(bodyUtf8, "mrz3") ?? "";
                    if (!string.IsNullOrEmpty(mrz1) || !string.IsNullOrEmpty(mrz2))
                    {
                        result.Mrz = mrz1 + "^" + mrz2 + "^" + mrz3;
                    }
                }

                // 解析证据图像
                var data = obj["data"] as JObject;
                if (TryReadInt32(data?["card_type"], out var cardType))
                    result.CardType = cardType;

                // 身份证（card_type=30）附加人员信息和光学核验数据。
                // 其他证件类型不处理这些字段，以保持既有回调结构和行为。
                if (result.CardType == 30)
                {
                    var person = FirstObject(data?["person_info"]);
                    if (person != null)
                    {
                        result.Name = ReadString(person["name"]);
                        result.Sex = ReadString(person["sex"]);
                        result.CardId = ReadString(person["cardId"]);
                        result.Birthday = ReadString(person["birthday"]);
                        result.DateOfIssue = ReadString(person["dateOfissue"]);
                    }

                    var opticsAuthen = data?["optics_authen"] as JObject;
                    if (TryReadInt32(opticsAuthen?["authen_score"], out var authenScore))
                        result.AuthenScore = authenScore;
                    if (TryReadInt32(opticsAuthen?["optical_check_result"], out var opticalResult) &&
                        (opticalResult == 0 || opticalResult == 1))
                    {
                        result.OpticalCheckResult = opticalResult;
                    }
                }

                var evidenceArray = data?["evidence_images"] as JArray ?? obj["evidence_images"] as JArray;
                if (evidenceArray != null)
                {
                    result.EvidenceImages = new List<string>();
                    foreach (var item in evidenceArray)
                        result.EvidenceImages.Add(item.ToString(Newtonsoft.Json.Formatting.None));
                }

                // 获取 request_id 后即视为有效，与 Delphi 判定保持一致
                result.Valid = !string.IsNullOrEmpty(result.RequestId);
            }
            catch
            {
                result.Valid = false;
            }
            return result;
        }

        private static JObject FirstObject(JToken token)
        {
            if (token is JObject obj)
                return obj;
            if (token is JArray array && array.Count > 0)
                return array[0] as JObject;
            return null;
        }

        private static string ReadString(JToken token)
        {
            return token?.Type == JTokenType.String ? token.Value<string>() ?? "" : "";
        }

        private static bool TryReadInt32(JToken token, out int value)
        {
            value = -1;
            if (token == null || token.Type != JTokenType.Integer)
                return false;

            try
            {
                var number = token.Value<long>();
                if (number < int.MinValue || number > int.MaxValue)
                    return false;
                value = (int)number;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string FindMrzField(string json, string fieldName)
        {
            // 使用简单字符串搜索提取字段值，与 Delphi 的 ExtractField 实现方式一致
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
                // 同时在 data 子对象中查找，与 Delphi 兼容逻辑一致
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
                // 仅在找到 CardText 时判定有效，与 Delphi 判定保持一致
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
                result.SavePath = GetStringField(obj, "save_path") ?? "";
                if (string.IsNullOrEmpty(result.ResourceType))
                    result.ResourceType = obj["resource_type"]?.ToString() ?? "";

                // 提取 data 数据段，与 Delphi 行为一致
                var data = obj["data"] as JObject;

                // 按 Delphi 的顺序在 data 数据段中查找资源专用字段名
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

                    // 保持 SaveUndistortedFingerprintImage 的既有查找顺序：先顶层，后 data。
                    result.UndistortedImageBase64 =
                        GetStringField(obj, "undistorted_image_base64")
                        ?? GetStringField(data, "undistorted_image_base64")
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
                    // 未知资源类型时尝试通用字段名
                    result.ImageBase64 = GetStringField(data, "image_base64")
                        ?? GetStringField(obj, "image_base64")
                        ?? GetStringField(data, "face_capture")
                        ?? GetStringField(obj, "face_capture")
                        ?? GetStringField(data, "fingerprint_capture")
                        ?? GetStringField(obj, "fingerprint_capture")
                        ?? "";
                }

                // MIME 类型处理与 Delphi 保持一致
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

                // 仅在找到图像数据时判定有效，与 Delphi 判定保持一致
                result.Valid = !string.IsNullOrEmpty(result.ImageBase64);
            }
            catch
            {
                // 保留格式异常 JSON 中 save_path 的原有回退提取逻辑
                result.SavePath = JsonHelper.ExtractString(bodyUtf8, "save_path");
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
                // 协议规定 evidence_images 位于 data 数据段内，与 Delphi/C++ 行为一致
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
