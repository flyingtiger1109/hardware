using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Storage;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server
{
    public class TerminalCallbackHandler
    {
        private readonly TerminalClient _terminalClient;
        private readonly DllCallbackSender _dllCallback;
        private readonly ConcurrentDictionary<string, string> _requestSaveDirs;
        private readonly ConcurrentDictionary<string, string> _requestCallbacks;
        private readonly ConcurrentDictionary<string, byte> _processedCallbacks;  // Dedup: {requestId}_{resourceType}
        private readonly Action<string> _log;

        // Callback types that should only be forwarded once per request_id (not event streams like ocr_event_status)
        private static readonly string[] DedupResourceTypes = {
            "ocr_document", "nfc_card", "iris_image", "face_image", "fingerprint_image", "protocol"
        };

        public TerminalCallbackHandler(
            TerminalClient terminalClient,
            DllCallbackSender dllCallback,
            ConcurrentDictionary<string, string> requestSaveDirs,
            ConcurrentDictionary<string, string> requestCallbacks,
            Action<string> log)
        {
            _terminalClient = terminalClient;
            _dllCallback = dllCallback;
            _requestSaveDirs = requestSaveDirs;
            _requestCallbacks = requestCallbacks;
            _processedCallbacks = new ConcurrentDictionary<string, byte>();
            _log = log;
        }

        public string Handle(string bodyUtf8)
        {
            try
            {
                var resourceType = CallbackParser.GetResourceType(bodyUtf8);
                _log($"Terminal callback: resource_type={resourceType}");

                switch (resourceType)
            {
                case "ocr_event_status":
                    HandleOcrEventStatus(bodyUtf8);
                    break;
                case "ocr_document":
                    HandleOcrDocument(bodyUtf8);
                    break;
                case "nfc_card":
                    HandleNfcCard(bodyUtf8);
                    break;
                case "iris_image":
                    HandleIrisImage(bodyUtf8);
                    break;
                case "face_image":
                    HandleFaceImage(bodyUtf8);
                    break;
                case "fingerprint_image":
                    HandleFingerprintImage(bodyUtf8);
                    break;
                case "protocol":
                    HandleProtocol(bodyUtf8);
                    break;
                default:
                    // Fallback: detect 2.22 protocol callback by characteristic fields (status=yes/no + id_no)
                    var cbStatus = JsonHelper.ExtractString(bodyUtf8, "status");
                    var cbIdNo = JsonHelper.ExtractString(bodyUtf8, "id_no");
                    if ((cbStatus == "yes" || cbStatus == "no") && !string.IsNullOrEmpty(cbIdNo))
                    {
                        _log($"Detected protocol callback via fallback: status={cbStatus}");
                        HandleProtocol(bodyUtf8);
                    }
                    else
                    {
                        _log($"Unknown resource_type: {resourceType}");
                    }
                    break;
            }

            }
            catch (Exception ex)
            {
                _log($"Terminal callback handler error: {ex.Message}");
            }

            return "{\"status\":\"ok\"}";
        }

        /// <summary>
        /// Map OCR event_type (from 2.5 protocol) to Chinese description.
        /// </summary>
        private static string TranslateOcrEventType(string eventType)
        {
            switch (eventType)
            {
                case "event_type_unknown": return "未知";
                case "event_type_card_detect": return "证件检测";
                case "event_type_card_class": return "证件分类";
                case "event_type_rfid_begin": return "RFID 开始";
                case "event_type_rfid_end": return "RFID 结束";
                case "event_type_ocr_begin": return "OCR 开始";
                case "event_type_ocr_end": return "OCR 结束";
                case "event_type_card_leave": return "证件离开";
                case "event_type_mrz_begin": return "机读区开始";
                case "event_type_mrz_end": return "机读区结束";
                case "event_type_visual_area_begin": return "可视区开始";
                case "event_type_visual_area_end": return "可视区结束";
                case "event_type_barcode_detect": return "条码检测";
                case "event_type_process_end": return "流程结束（不推送）";
                case "event_type_mrz_result_success": return "机读区识别成功";
                case "event_type_mrz_result_fail": return "机读区识别失败";
                case "event_type_viz_result_success": return "可视区识别成功";
                case "event_type_viz_result_fail": return "可视区识别失败";
                case "event_type_rfid_result_success": return "RFID 识别成功";
                case "event_type_rfid_result_fail": return "RFID 识别失败";
                default: return eventType;
            }
        }

        private void HandleOcrEventStatus(string bodyUtf8)
        {
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");

            // 2.5 protocol: event_type is in data.event_type (蛇形字符串)
            var dataObj = JsonHelper.ExtractObject(bodyUtf8, "data");
            var eventType = "";
            if (!string.IsNullOrEmpty(dataObj))
                eventType = JsonHelper.ExtractString(dataObj, "event_type");
            if (string.IsNullOrEmpty(eventType))
                eventType = JsonHelper.ExtractString(bodyUtf8, "event_type");

            var chineseEvent = TranslateOcrEventType(eventType);

            // Check for error conditions
            var errorCode = JsonHelper.ExtractString(bodyUtf8, "error_code");
            var message = JsonHelper.ExtractString(bodyUtf8, "message");

            // Only "证件检测" and "证件离开" show in UI; all others write to log file only
            bool showInUi = (eventType == "event_type_card_detect" || eventType == "event_type_card_leave");

            var logLine = !string.IsNullOrEmpty(errorCode)
                ? $"[OCR事件] request_id={requestId}, event={chineseEvent}, error={errorCode}, message={message}"
                : $"[OCR事件] request_id={requestId}, event={chineseEvent}";

            if (showInUi)
                _log(logLine);           // UI + file
            else
                Logger.Info(logLine);    // file only
        }

        private void HandleOcrDocument(string bodyUtf8)
        {
            var result = CallbackParser.ParseOcrDocument(bodyUtf8);
            if (!result.Valid) { _log("Invalid OCR callback"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"OCR result: request_id={result.RequestId}, mrz={result.Mrz}");

            // Save OCR result JSON
            FileSaver.SaveJsonFile(bodyUtf8, saveDir, result.RequestId, "ocr_result.json");

            // Save evidence images if present
            SaveEvidenceImages(bodyUtf8, saveDir, result.RequestId);

            // Forward to DLL callback — fallback to default URL if request_id not found (same as Delphi)
            var callbackUrl = GetCallback(result.RequestId);
            if (string.IsNullOrEmpty(callbackUrl))
                callbackUrl = AppConfig.Instance.GetDllCallbackBaseUrl() + "/ocr";
            if (!TryMarkProcessed(result.RequestId, "ocr_document"))
            {
                _log("[OCR回调] 重复OCR结果，已去重: " + result.RequestId);
                return;
            }
            var savePath = PathHelper.EnsureRequestFolder(saveDir, result.RequestId);
            _dllCallback.SendOcrResult(result.RequestId, result.Mrz, savePath).GetAwaiter().GetResult();
            CleanupProcessedIfNeeded();
        }

        private void HandleNfcCard(string bodyUtf8)
        {
            var result = CallbackParser.ParseNfcCard(bodyUtf8);
            if (!result.Valid) { _log("Invalid NFC callback"); return; }

            _log($"[IC卡回调] request_id={result.RequestId}, card_text={result.CardText}");

            var callbackUrl = GetCallback(result.RequestId);
            if (string.IsNullOrEmpty(callbackUrl))
                callbackUrl = AppConfig.Instance.GetDllCallbackBaseUrl() + "/nfc-card";
            if (!TryMarkProcessed(result.RequestId, "nfc_card"))
            {
                _log("[IC卡回调] 重复回调，已去重: " + result.RequestId);
                return;
            }
            _dllCallback.SendNfcResult(result.RequestId, result.CardText).GetAwaiter().GetResult();
            CleanupProcessedIfNeeded();
        }

        private void HandleIrisImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseImageCapture(bodyUtf8, "iris_image");
            if (!result.Valid) { _log("Invalid iris callback"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"Iris capture result: request_id={result.RequestId}");

            string savePath = "";
            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                savePath = FileSaver.SaveBase64Image(result.ImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "iris");
            }

            var callbackUrl = GetCallback(result.RequestId);
            if (string.IsNullOrEmpty(callbackUrl))
                callbackUrl = AppConfig.Instance.GetDllCallbackBaseUrl() + "/iris";
            if (!TryMarkProcessed(result.RequestId, "iris_image"))
            {
                _log("[虹膜回调] 重复回调，已去重: " + result.RequestId);
                return;
            }
            _dllCallback.SendIrisResult(result.RequestId, savePath).GetAwaiter().GetResult();
            CleanupProcessedIfNeeded();
        }

        private void HandleFaceImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseImageCapture(bodyUtf8, "face_image");
            if (!result.Valid) { _log("Invalid face callback"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"Face async capture result: request_id={result.RequestId}");

            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                FileSaver.SaveBase64Image(result.ImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "face");
            }
        }

        private void HandleFingerprintImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseImageCapture(bodyUtf8, "fingerprint_image");
            if (!result.Valid) { _log("Invalid fingerprint callback"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"Fingerprint async capture result: request_id={result.RequestId}");

            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                FileSaver.SaveBase64Image(result.ImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "fingerprint");
            }
        }

        /// <summary>
        /// Handle 2.22 protocol signing result pushed from terminal.
        /// Terminal sends: request_id, name, sex, id_no, doc_type, birthday, nationality, status (yes/no)
        /// Maps back to DLL callback format with Chinese field names (ZJHM, ZJLB, GJDQDM, XM, XB, CSRQ).
        /// </summary>
        private void HandleProtocol(string bodyUtf8)
        {
            var result = CallbackParser.ParseAuthorize(bodyUtf8);
            if (!result.Valid) { _log("Invalid protocol callback"); return; }

            // 2.22 status field: "yes" (agreed) or "no" (rejected)
            var status = JsonHelper.ExtractString(bodyUtf8, "status");
            _log($"[授权回调] request_id={result.RequestId}, status={status}");

            var callbackUrl = GetCallback(result.RequestId);
            if (string.IsNullOrEmpty(callbackUrl))
                callbackUrl = AppConfig.Instance.GetDllCallbackBaseUrl() + "/authorize";
            if (!TryMarkProcessed(result.RequestId, "protocol"))
            {
                _log("[授权回调] 重复回调，已去重: " + result.RequestId);
                return;
            }

            // Map 2.22 terminal fields back to DLL Chinese field names
            // Terminal: name,sex,id_no,doc_type,birthday,nationality,port_code → DLL: XM,XB,ZJHM,ZJLB,CSRQ,GJDQDM,KADM
            var name = JsonHelper.ExtractString(bodyUtf8, "name");
            var sex = JsonHelper.ExtractString(bodyUtf8, "sex");
            var idNo = JsonHelper.ExtractString(bodyUtf8, "id_no");
            var docType = JsonHelper.ExtractString(bodyUtf8, "doc_type");
            var birthday = JsonHelper.ExtractString(bodyUtf8, "birthday");
            var nationality = JsonHelper.ExtractString(bodyUtf8, "nationality");
            var portCode = JsonHelper.ExtractString(bodyUtf8, "port_code");  // KADM

            // Build DLL callback payload (matching Delphi format with Chinese field names)
            var isYes = (status == "yes");
            var message = isYes ? "同意授权" : "旅客拒绝签署";
            var authResult = isYes ? "1" : "0";

            var payload = "{" +
                "\"request_id\":\"" + JsonHelper.EscapeString(result.RequestId) + "\"," +
                "\"resource_type\":\"authorization\"," +
                "\"auth_result\":" + authResult + "," +
                "\"ZJHM\":\"" + JsonHelper.EscapeString(idNo) + "\"," +
                "\"ZJLB\":\"" + JsonHelper.EscapeString(docType) + "\"," +
                "\"GJDQDM\":\"" + JsonHelper.EscapeString(nationality) + "\"," +
                "\"XM\":\"" + JsonHelper.EscapeString(name) + "\"," +
                "\"XB\":\"" + JsonHelper.EscapeString(sex) + "\"," +
                "\"CSRQ\":\"" + JsonHelper.EscapeString(birthday) + "\"," +
                "\"KADM\":\"" + JsonHelper.EscapeString(portCode) + "\"," +
                "\"message\":\"" + JsonHelper.EscapeString(message) + "\"" +
                "}";

            _log($"[授权回调] 转发至DLL: request_id={result.RequestId}, auth_result={authResult}");
            _dllCallback.PostCallbackRaw("/authorize", payload).GetAwaiter().GetResult();
            CleanupProcessedIfNeeded();
        }

        /// <summary>
        /// Map OCR evidence lamp type to Chinese name (same as Delphi).
        /// ImageType=2 or LampType=2 → 红外, LampType=1 → 可见光, LampType=3 → 紫外
        /// </summary>
        private static string MapEvidenceImageName(string imgJson)
        {
            var imageType = JsonHelper.ExtractInt(imgJson, "image_type");
            if (imageType == 0)
                imageType = (int)JsonHelper.ExtractInt(imgJson, "imageType");
            if (imageType == 2) return "红外"; // infrared

            var lampType = JsonHelper.ExtractInt(imgJson, "lamp_type");
            if (lampType == 0)
                lampType = (int)JsonHelper.ExtractInt(imgJson, "lampType");
            switch (lampType)
            {
                case 1: return "可见光"; // visible light
                case 2: return "红外";    // infrared
                case 3: return "紫外";   // ultraviolet
                default: return "";
            }
        }

        private void SaveEvidenceImages(string bodyUtf8, string saveDir, string requestId)
        {
            try
            {
                var imageItems = CallbackParser.ParseEvidenceImages(bodyUtf8);
                foreach (var imgJson in imageItems)
                {
                    var base64 = JsonHelper.ExtractString(imgJson, "image_base64");
                    if (string.IsNullOrEmpty(base64))
                        base64 = JsonHelper.ExtractString(imgJson, "base64");
                    if (string.IsNullOrEmpty(base64)) continue;

                    var imgName = MapEvidenceImageName(imgJson);
                    if (string.IsNullOrEmpty(imgName)) continue;

                    var imageType = JsonHelper.ExtractString(imgJson, "image_type");
                    if (string.IsNullOrEmpty(imageType))
                        imageType = JsonHelper.ExtractString(imgJson, "imageType");
                    if (string.IsNullOrEmpty(imageType)) imageType = "jpg";

                    var ext = imageType.Contains("bmp") ? ".bmp" : ".jpg";
                    var saveDir2 = PathHelper.EnsureRequestFolder(saveDir, requestId);
                    var filePath = System.IO.Path.Combine(saveDir2, imgName + ext);
                    FileSaver.SaveBase64ImageToFile(base64, filePath);
                    _log($"[OCR] 照片已保存: type={JsonHelper.ExtractInt(imgJson, "image_type")},lamp={JsonHelper.ExtractInt(imgJson, "lamp_type")},path={filePath}");
                }
            }
            catch (Exception ex)
            {
                _log($"Error saving evidence images: {ex.Message}");
            }
        }

        private string GetSaveDir(string requestId)
        {
            _requestSaveDirs.TryGetValue(requestId, out var dir);
            return PathHelper.SafeResolveSaveDir(dir);
        }

        /// <summary>
        /// Get DLL callback URL for a request. Returns null if request_id is not found
        /// (indicating the request was cancelled, process ended, or terminal switched).
        /// Does NOT fall back to default URL — prevents forwarding stale data to DLL.
        /// </summary>
        private string GetCallback(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) return null;
            _requestCallbacks.TryGetValue(requestId, out var url);
            return url;  // null if not found → callback will be skipped
        }

        /// <summary>
        /// Try to mark a callback as processed. Returns true if this is the first time
        /// (should forward), false if already processed (duplicate, skip).
        /// </summary>
        private bool TryMarkProcessed(string requestId, string resourceType)
        {
            var key = requestId + "_" + resourceType;
            return _processedCallbacks.TryAdd(key, 0);
        }

        /// <summary>
        /// Periodic cleanup of processed callback dedup set (called every ~500 callbacks).
        /// </summary>
        private void CleanupProcessedIfNeeded()
        {
            if (_processedCallbacks.Count > 5000)
            {
                _processedCallbacks.Clear();
                _log("[回调去重] 已清理去重缓存");
            }
        }
    }
}
