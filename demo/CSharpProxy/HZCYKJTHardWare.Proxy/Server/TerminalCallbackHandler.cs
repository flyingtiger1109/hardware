using System;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Storage;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.Server
{
    public class TerminalCallbackHandler
    {
        private readonly TerminalClient _terminalClient;
        private readonly TerminalManager _terminalManager;
        private readonly DllCallbackSender _dllCallback;
        private readonly RequestRegistry _requestRegistry;
        private readonly Action<string> _log;

        internal TerminalCallbackHandler(
            TerminalClient terminalClient,
            TerminalManager terminalManager,
            DllCallbackSender dllCallback,
            RequestRegistry requestRegistry,
            Action<string> log)
        {
            _terminalClient = terminalClient;
            _terminalManager = terminalManager;
            _dllCallback = dllCallback;
            _requestRegistry = requestRegistry;
            _log = log;
        }

        public string Handle(string bodyUtf8)
        {
            try
            {
                var resourceType = CallbackParser.GetResourceType(bodyUtf8);
                // ocr_event_status 高频推送，日志已在 HandleOcrEventStatus 内按事件类型精简
                if (resourceType != "ocr_event_status")
                    _log($"[终端回调] resource_type={resourceType}");

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
                        _log($"[授权回调] 通过字段特征识别协议回调: status={cbStatus}");
                        HandleProtocol(bodyUtf8);
                    }
                    else
                    {
                        _log($"[终端回调] 未知资源类型: {resourceType}");
                    }
                    break;
            }

            }
            catch (Exception ex)
            {
                _log($"[终端回调] 处理异常: {ex.Message}");
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
                _log(logLine);           // UI + file — 证件检测/证件离开
            else if (eventType == "event_type_rfid_result_fail")
                Logger.Warn(logLine);    // RFID识别失败 → 警告
            else
                Logger.Debug(logLine);   // 其余事件 → Debug
        }

        private void HandleOcrDocument(string bodyUtf8)
        {
            var result = CallbackParser.ParseOcrDocument(bodyUtf8);
            if (!result.Valid) { _log("[OCR回调] 数据无效"); return; }
            if (!TryClaimRequest(result.RequestId, ProxyResourceTypes.OcrDocument, "OCR"))
                return;

            var saveDir = GetSaveDir(result.RequestId, ProxyResourceTypes.OcrDocument);
            _log($"[OCR回调] MRZ={result.Mrz}");

            // Save OCR result JSON
            FileSaver.SaveJsonFile(bodyUtf8, saveDir, result.RequestId, "ocr_result.json");

            // Save evidence images (light source: 红外光/紫外光/可见光 + portrait: 人像)
            SaveEvidenceImages(bodyUtf8, saveDir, result.RequestId);

            // Save MRZ information (MRZ.json with MRZ lines + person_info)
            SaveMrzJson(bodyUtf8, saveDir, result.RequestId);

            var savePath = PathHelper.EnsureRequestFolder(saveDir, result.RequestId);
            _ = _dllCallback.SendOcrResult(result.RequestId, result.Mrz, savePath);
            _requestRegistry.Complete(result.RequestId, ProxyResourceTypes.OcrDocument);
        }

        private void HandleNfcCard(string bodyUtf8)
        {
            var result = CallbackParser.ParseNfcCard(bodyUtf8);
            if (!result.Valid) { _log("[IC卡回调] 数据无效"); return; }
            if (!TryClaimRequest(result.RequestId, ProxyResourceTypes.NfcCard, "NFC"))
                return;

            _log($"[IC卡回调] card_text={result.CardText}");

            _ = _dllCallback.SendNfcResult(result.RequestId, result.CardText);
            _requestRegistry.Complete(result.RequestId, ProxyResourceTypes.NfcCard);
        }

        private void HandleIrisImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseIrisCapture(bodyUtf8);
            if (!result.Valid) { _log("[虹膜回调] 数据无效"); return; }

            if (!TryClaimRequest(result.RequestId, ProxyResourceTypes.IrisImage, "虹膜"))
                return;

            if (!string.IsNullOrEmpty(result.ErrorCode))
            {
                ForwardIrisFailure(result.RequestId, result.ErrorCode, result.Message);
                _requestRegistry.Complete(result.RequestId, ProxyResourceTypes.IrisImage);
                return;
            }

            var saveDir = GetSaveDir(result.RequestId, ProxyResourceTypes.IrisImage);
            var savedCount = 0;
            var leftPath = "";
            var rightPath = "";

            if (!string.IsNullOrEmpty(result.LeftImageBase64))
            {
                leftPath = FileSaver.SaveBase64Image(result.LeftImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "iris_left");
                if (!string.IsNullOrEmpty(leftPath)) savedCount++;
            }
            if (!string.IsNullOrEmpty(result.RightImageBase64))
            {
                rightPath = FileSaver.SaveBase64Image(result.RightImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "iris_right");
                if (!string.IsNullOrEmpty(rightPath)) savedCount++;
            }

            if (savedCount == 0)
            {
                ForwardIrisFailure(result.RequestId, "save_file_failed", "虹膜图片保存失败");
                _requestRegistry.Complete(result.RequestId, ProxyResourceTypes.IrisImage);
                return;
            }

            var savePath = PathHelper.EnsureRequestFolder(saveDir, result.RequestId);
            _log($"[虹膜回调] 保存完成: request_id={result.RequestId}, eyes={savedCount}, path={savePath}");
            _ = _dllCallback.SendIrisResult(result.RequestId, savePath);
            _requestRegistry.Complete(result.RequestId, ProxyResourceTypes.IrisImage);
        }

        private void ForwardIrisFailure(string requestId, string errorCode, string message)
        {
            if (string.IsNullOrEmpty(errorCode)) errorCode = "collect_failed";
            var payload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                "\",\"resource_type\":\"iris_image\",\"error\":true,\"code\":\"" +
                JsonHelper.EscapeString(errorCode) + "\",\"message\":\"" +
                JsonHelper.EscapeString(message) + "\"}";
            _ = _dllCallback.PostCallbackRaw("/iris", payload);
            _log($"[虹膜回调] 采集失败: request_id={requestId}, code={errorCode}, message={message}");
        }

        private void HandleFaceImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseImageCapture(bodyUtf8, "face_image");
            if (!result.Valid) { _log("[人脸回调] 数据无效"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"[人脸回调] 异步抓拍结果: request_id={result.RequestId}");

            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                FileSaver.SaveBase64Image(result.ImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "face");
            }
        }

        private void HandleFingerprintImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseImageCapture(bodyUtf8, "fingerprint_image");
            if (!result.Valid) { _log("[指纹回调] 数据无效"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"[指纹回调] 异步抓拍结果: request_id={result.RequestId}");

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
            if (!result.Valid) { _log("[授权回调] 数据无效"); return; }
            if (!TryClaimRequest(result.RequestId, ProxyResourceTypes.Protocol, "授权"))
                return;

            // 2.22 status field: "yes" (agreed) or "no" (rejected)
            var status = JsonHelper.ExtractString(bodyUtf8, "status");
            _log($"[授权回调] request_id={result.RequestId}, status={status}");

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
            _ = _dllCallback.PostCallbackRaw("/authorize", payload);
            _requestRegistry.Complete(result.RequestId, ProxyResourceTypes.Protocol);
        }

        /// <summary>
        /// Map OCR evidence lamp type to Chinese filename (aligned with C++ DLL).
        /// lampType/lamp_type: 1 → 可见光, 2 → 红外光, 3 → 紫外光
        /// (imageType/image_type is NOT used for light source — image_type == 2 means portrait)
        /// </summary>
        private static string MapEvidenceImageName(string imgJson)
        {
            var lampType = JsonHelper.ExtractInt(imgJson, "lampType");
            if (lampType == 0)
                lampType = (int)JsonHelper.ExtractInt(imgJson, "lamp_type");
            switch (lampType)
            {
                case 1: return "可见光";
                case 2: return "红外光";
                case 3: return "紫外光";
                default: return "";
            }
        }

        /// <summary>
        /// Check if evidence image is a portrait (证件人像图).
        /// imageType/image_type == 2 means portrait per protocol 2.6.
        /// </summary>
        private static bool IsOcrPortraitImage(string imgJson)
        {
            var imageType = JsonHelper.ExtractInt(imgJson, "imageType");
            if (imageType == 0)
                imageType = (int)JsonHelper.ExtractInt(imgJson, "image_type");
            return imageType == 2;
        }

        private void SaveEvidenceImages(string bodyUtf8, string saveDir, string requestId)
        {
            try
            {
                var imageItems = CallbackParser.ParseEvidenceImages(bodyUtf8);
                if (imageItems.Count == 0) return;

                var saveDir2 = PathHelper.EnsureRequestFolder(saveDir, requestId);
                bool savedVisible = false, savedInfrared = false, savedUltraviolet = false, savedPortrait = false;
                var savedNames = new System.Collections.Generic.List<string>();

                foreach (var imgJson in imageItems)
                {
                    // Extract base64: protocol uses imageData (camelCase), also support snake_case
                    var base64 = JsonHelper.ExtractString(imgJson, "imageData");
                    if (string.IsNullOrEmpty(base64))
                        base64 = JsonHelper.ExtractString(imgJson, "image_data");
                    if (string.IsNullOrEmpty(base64))
                        base64 = JsonHelper.ExtractString(imgJson, "image_base64");
                    if (string.IsNullOrEmpty(base64)) continue;

                    // Save light source images by lamp_type (dedup: first of each type only)
                    var lampName = MapEvidenceImageName(imgJson);
                    if (!string.IsNullOrEmpty(lampName))
                    {
                        bool shouldSave = false;
                        if (lampName == "可见光" && !savedVisible) { savedVisible = true; shouldSave = true; }
                        else if (lampName == "红外光" && !savedInfrared) { savedInfrared = true; shouldSave = true; }
                        else if (lampName == "紫外光" && !savedUltraviolet) { savedUltraviolet = true; shouldSave = true; }

                        if (shouldSave)
                        {
                            var filePath = System.IO.Path.Combine(saveDir2, lampName + ".jpg");
                            FileSaver.SaveBase64ImageToFile(base64, filePath);
                            savedNames.Add(lampName);
                        }
                    }

                    // Save portrait image (imageType == 2, first only)
                    if (!savedPortrait && IsOcrPortraitImage(imgJson))
                    {
                        savedPortrait = true;
                        var filePath = System.IO.Path.Combine(saveDir2, "人像.jpg");
                        FileSaver.SaveBase64ImageToFile(base64, filePath);
                        savedNames.Add("人像");
                    }
                }

                if (savedNames.Count > 0)
                    _log($"[OCR] 照片已保存至 {saveDir2}: {string.Join(", ", savedNames)}");
            }
            catch (Exception ex)
            {
                _log($"[OCR] 保存证据图片异常: {ex.Message}");
            }
        }

        /// <summary>
        /// Save MRZ information as MRZ.json from OCR callback body.
        /// Extracts MRZ1/MRZ2/MRZ3 and person_info array per protocol 2.6.
        /// </summary>
        private void SaveMrzJson(string bodyUtf8, string saveDir, string requestId)
        {
            try
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(bodyUtf8);
                var data = obj["data"] as Newtonsoft.Json.Linq.JObject;
                if (data == null) return;

                // Extract MRZ lines (same field names as C++ DLL)
                var mrz1 = data["MRZ1"]?.ToString() ?? "";
                var mrz2 = data["MRZ2"]?.ToString() ?? "";
                var mrz3 = data["MRZ3"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mrz1)) mrz1 = data["mrz1"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mrz2)) mrz2 = data["mrz2"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mrz3)) mrz3 = data["mrz3"]?.ToString() ?? "";

                // Extract person_info array
                var personInfoArray = data["person_info"] as Newtonsoft.Json.Linq.JArray;

                // Build MRZ.json
                var mrzObj = new Newtonsoft.Json.Linq.JObject();
                mrzObj["request_id"] = requestId;

                var mrzLines = new Newtonsoft.Json.Linq.JArray();
                if (!string.IsNullOrEmpty(mrz1)) mrzLines.Add(mrz1);
                if (!string.IsNullOrEmpty(mrz2)) mrzLines.Add(mrz2);
                if (!string.IsNullOrEmpty(mrz3)) mrzLines.Add(mrz3);
                mrzObj["mrz_lines"] = mrzLines;

                if (personInfoArray != null && personInfoArray.Count > 0)
                    mrzObj["person_info"] = personInfoArray;
                else
                    mrzObj["person_info"] = new Newtonsoft.Json.Linq.JArray();

                var saveDir2 = PathHelper.EnsureRequestFolder(saveDir, requestId);
                var filePath = System.IO.Path.Combine(saveDir2, "MRZ.json");
                System.IO.File.WriteAllText(filePath, mrzObj.ToString(Newtonsoft.Json.Formatting.Indented),
                    System.Text.Encoding.UTF8);
                _log($"[OCR] MRZ信息已保存: path={filePath}");
            }
            catch (Exception ex)
            {
                _log($"[OCR] 保存MRZ信息异常: {ex.Message}");
            }
        }

        private string GetSaveDir(string requestId, string resourceType)
        {
            var dir = "";
            if (_requestRegistry.TryGet(requestId, resourceType, out var context))
                dir = context.SaveDir;
            if (string.IsNullOrEmpty(dir))
                dir = _terminalManager?.ProcessSaveDir;
            return PathHelper.SafeResolveSaveDir(dir);
        }

        private string GetSaveDir(string requestId)
        {
            return PathHelper.SafeResolveSaveDir(_terminalManager?.ProcessSaveDir);
        }

        private bool TryClaimRequest(string requestId, string resourceType, string operation)
        {
            if (_requestRegistry.TryClaimCallback(requestId, resourceType, out _))
                return true;

            Logger.Warn($"[{operation}回调] 请求重复、已过期或未登记，已跳过: request_id={requestId}");
            return false;
        }
    }
}
