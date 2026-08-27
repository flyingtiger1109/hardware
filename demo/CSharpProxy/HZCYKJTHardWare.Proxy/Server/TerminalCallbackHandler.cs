using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Parsing;
using HZCYKJTHardWare.Proxy.Storage;
using HZCYKJTHardWare.Proxy.Terminal;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Server
{
    public class TerminalCallbackHandler
    {
        private readonly TerminalClient _terminalClient;
        private readonly TerminalManager _terminalManager;
        private readonly DllCallbackSender _dllCallback;
        private readonly RequestRegistry _requestRegistry;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly Action<string> _log;

        internal TerminalCallbackHandler(
            TerminalClient terminalClient,
            TerminalManager terminalManager,
            DllCallbackSender dllCallback,
            RequestRegistry requestRegistry,
            TerminalProcessRegistry processRegistry,
            Action<string> log)
        {
            _terminalClient = terminalClient;
            _terminalManager = terminalManager;
            _dllCallback = dllCallback;
            _requestRegistry = requestRegistry;
            _processRegistry = processRegistry;
            _log = log;
        }

        public async Task<string> HandleAsync(string bodyUtf8,
            IPAddress sourceAddress = null, string callbackPath = null)
        {
            try
            {
                var resourceType = CallbackParser.GetResourceType(bodyUtf8);
                var inferredResourceType = false;
                var cbStatus = "";

                if (string.IsNullOrEmpty(resourceType))
                {
                    // 回退处理：根据特征字段识别缺少 resource_type 的回调。
                    // 2.22 协议回调正文为 {"request_id":"...","status":"yes|no"}，
                    // 终端可能不会回传 id_no、name 等全部字段。
                    cbStatus = JsonHelper.ExtractString(bodyUtf8, "status");
                    var cbRequestId = JsonHelper.ExtractString(bodyUtf8, "request_id");
                    if ((string.Equals(cbStatus, "yes", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cbStatus, "no", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrEmpty(cbRequestId))
                    {
                        resourceType = "protocol";
                        inferredResourceType = true;
                    }
                }

                // ocr_event_status 高频推送，日志已在 HandleOcrEventStatus 内按事件类型精简
                if (!string.IsNullOrEmpty(resourceType) && resourceType != "ocr_event_status")
                    _log("[终端回调][调试] 资源类型=" + resourceType +
                        (inferredResourceType ? "（根据字段推断）" : ""));

                switch (resourceType)
                {
                    case "ocr_event_status":
                        HandleOcrEventStatus(bodyUtf8);
                        break;
                    case "ocr_document":
                        await HandleOcrDocumentAsync(bodyUtf8, sourceAddress)
                            .ConfigureAwait(false);
                        break;
                    case "nfc_card":
                        await HandleNfcCardAsync(bodyUtf8, sourceAddress)
                            .ConfigureAwait(false);
                        break;
                    case "iris_image":
                        await HandleIrisImageAsync(bodyUtf8, sourceAddress)
                            .ConfigureAwait(false);
                        break;
                    case "face_image":
                        HandleFaceImage(bodyUtf8);
                        break;
                    case "fingerprint_image":
                        HandleFingerprintImage(bodyUtf8);
                        break;
                    case "protocol":
                        if (inferredResourceType)
                            _log($"[授权回调][调试] 通过字段特征识别协议回调：状态={cbStatus}");
                        await HandleProtocolAsync(bodyUtf8, sourceAddress)
                            .ConfigureAwait(false);
                        break;
                    default:
                        // 记录正文片段，用于定位未知回调类型
                        var snippet = bodyUtf8?.Length > 200 ? bodyUtf8.Substring(0, 200) : (bodyUtf8 ?? "");
                        _log($"[终端回调][警告] 未知资源类型：路径={callbackPath}，正文={snippet}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[终端回调] 处理异常", ex);
            }

            return "{\"status\":\"ok\"}";
        }

        /// <summary>
        /// 将 2.5 协议的 OCR event_type 映射为中文说明。
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

        private static string FormatOpticalCheckResult(int result)
        {
            switch (result)
            {
                case 0: return "通过(0)";
                case 1: return "不通过(1)";
                default: return "未知/未检测(-1)";
            }
        }

        private void HandleOcrEventStatus(string bodyUtf8)
        {
            var requestId = JsonHelper.ExtractString(bodyUtf8, "request_id");

            // 2.5 协议的 event_type 位于 data.event_type，字段值采用蛇形命名
            var dataObj = JsonHelper.ExtractObject(bodyUtf8, "data");
            var eventType = "";
            if (!string.IsNullOrEmpty(dataObj))
                eventType = JsonHelper.ExtractString(dataObj, "event_type");
            if (string.IsNullOrEmpty(eventType))
                eventType = JsonHelper.ExtractString(bodyUtf8, "event_type");

            var chineseEvent = TranslateOcrEventType(eventType);

            // 检查错误状态
            var errorCode = JsonHelper.ExtractString(bodyUtf8, "error_code");
            var message = JsonHelper.ExtractString(bodyUtf8, "message");

            // UI 仅显示“证件检测”和“证件离开”，其他事件只写入日志文件
            bool showInUi = (eventType == "event_type_card_detect" || eventType == "event_type_card_leave");

            var logLine = !string.IsNullOrEmpty(errorCode)
                ? $"[OCR事件] request_id={requestId}，事件={chineseEvent}，错误码={errorCode}，消息={message}"
                : $"[OCR事件] request_id={requestId}，事件={chineseEvent}";

            if (showInUi)
                _log(logLine);           // UI + file — 证件检测/证件离开
            else if (eventType == "event_type_rfid_result_fail")
                Logger.Warn(logLine);    // RFID识别失败 → 警告
            else
                Logger.Debug(logLine);   // 其余事件 → Debug
        }

        private async Task HandleOcrDocumentAsync(string bodyUtf8,
            IPAddress sourceAddress)
        {
            var parsedBody = ParsedJsonBody.Parse(bodyUtf8);
            var result = CallbackParser.ParseOcrDocument(
                parsedBody.Root, parsedBody.RawBody);
            if (!result.Valid) { _log("[OCR回调][警告] 数据无效"); return; }
            var route = await ResolveCallbackAsync(result.RequestId,
                ProxyResourceTypes.OcrDocument, "OCR", bodyUtf8, sourceAddress)
                .ConfigureAwait(false);
            if (route == null)
                return;

            var saveDir = route.SaveDir;
            if (result.CardType == 30)
            {
                _log("[OCR回调] ID卡: 姓名=" + JsonHelper.ToLogValue(result.Name) +
                    ", 性别=" + JsonHelper.ToLogValue(result.Sex) +
                    ", 证号=" + JsonHelper.ToLogValue(result.CardId) +
                    ", 光学鉴权分数=" + result.AuthenScore +
                    ", 鉴伪结果=" + FormatOpticalCheckResult(result.OpticalCheckResult));
            }
            else
            {
                _log($"[OCR回调] MRZ={result.Mrz}");
            }

            // 保存 OCR 结果 JSON
            FileSaver.SaveJsonFile(bodyUtf8, saveDir, result.RequestId, "ocr_result.json");

            // 保存证据图像，包括红外光、紫外光、可见光和证件人像
            SaveEvidenceImages(parsedBody.Root, saveDir, result.RequestId);

            // 保存 MRZ 信息，MRZ.json 包含 MRZ 行及 person_info
            SaveMrzJson(parsedBody.Root, saveDir, result.RequestId);

            var savePath = PathHelper.EnsureRequestFolder(saveDir, result.RequestId);
            if (!CanDeliver(route, "OCR")) return;
            var delivery = await _dllCallback.SendOcrResult(route.DeliveryRequestId,
                result.Mrz, savePath, result, route.CancellationToken).ConfigureAwait(false);
            FinishDelivery(route, delivery);
        }

        private async Task HandleNfcCardAsync(string bodyUtf8,
            IPAddress sourceAddress)
        {
            var result = CallbackParser.ParseNfcCard(bodyUtf8);
            if (!result.Valid) { _log("[IC卡回调][警告] 数据无效"); return; }
            var route = await ResolveCallbackAsync(result.RequestId,
                ProxyResourceTypes.NfcCard, "NFC", bodyUtf8, sourceAddress)
                .ConfigureAwait(false);
            if (route == null)
                return;

            _log($"[IC卡回调] 卡片文本={result.CardText}");

            if (!CanDeliver(route, "NFC")) return;
            var delivery = await _dllCallback.SendNfcResult(route.DeliveryRequestId,
                result.CardText, route.CancellationToken).ConfigureAwait(false);
            FinishDelivery(route, delivery);
        }

        private async Task HandleIrisImageAsync(string bodyUtf8,
            IPAddress sourceAddress)
        {
            var result = CallbackParser.ParseIrisCapture(bodyUtf8);
            if (!result.Valid) { _log("[虹膜回调][警告] 数据无效"); return; }

            var route = await ResolveCallbackAsync(result.RequestId,
                ProxyResourceTypes.IrisImage, "虹膜", bodyUtf8, sourceAddress)
                .ConfigureAwait(false);
            if (route == null)
                return;

            if (!string.IsNullOrEmpty(result.ErrorCode))
            {
                if (!CanDeliver(route, "虹膜")) return;
                var failureDelivery = await ForwardIrisFailureAsync(
                    route.DeliveryRequestId, result.ErrorCode, result.Message,
                    route.CancellationToken)
                    .ConfigureAwait(false);
                FinishDelivery(route, failureDelivery);
                return;
            }

            var saveDir = route.SaveDir;
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
                if (!CanDeliver(route, "虹膜")) return;
                var failureDelivery = await ForwardIrisFailureAsync(
                    route.DeliveryRequestId, "save_file_failed",
                    "虹膜图片保存失败", route.CancellationToken)
                    .ConfigureAwait(false);
                FinishDelivery(route, failureDelivery);
                return;
            }

            var savePath = PathHelper.EnsureRequestFolder(saveDir, result.RequestId);
            _log($"[虹膜回调] 保存完成：request_id={result.RequestId}，眼数={savedCount}，路径={savePath}");
            if (!CanDeliver(route, "虹膜")) return;
            var delivery = await _dllCallback.SendIrisResult(route.DeliveryRequestId,
                savePath, route.CancellationToken).ConfigureAwait(false);
            FinishDelivery(route, delivery);
        }

        private Task<CallbackDeliveryResult> ForwardIrisFailureAsync(string requestId,
            string errorCode, string message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(errorCode)) errorCode = "collect_failed";
            var payload = "{\"request_id\":\"" + JsonHelper.EscapeString(requestId) +
                "\",\"resource_type\":\"iris_image\",\"error\":true,\"code\":\"" +
                JsonHelper.EscapeString(errorCode) + "\",\"message\":\"" +
                JsonHelper.EscapeString(message) + "\"}";
            _log($"[虹膜回调] 采集失败：request_id={requestId}，错误码={errorCode}，消息={message}");
            return _dllCallback.PostCallbackRaw("/iris", payload, cancellationToken);
        }

        private void HandleFaceImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseImageCapture(bodyUtf8, "face_image");
            if (!result.Valid) { _log("[人脸回调][警告] 数据无效"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"[人脸回调] 异步抓拍结果：request_id={result.RequestId}");

            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                FileSaver.SaveBase64Image(result.ImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "face");
            }
        }

        private void HandleFingerprintImage(string bodyUtf8)
        {
            var result = CallbackParser.ParseImageCapture(bodyUtf8, "fingerprint_image");
            if (!result.Valid) { _log("[指纹回调][警告] 数据无效"); return; }

            var saveDir = GetSaveDir(result.RequestId);
            _log($"[指纹回调] 异步抓拍结果：request_id={result.RequestId}");

            if (!string.IsNullOrEmpty(result.ImageBase64))
            {
                FileSaver.SaveBase64Image(result.ImageBase64, result.ImageMimeType,
                    saveDir, result.RequestId, "fingerprint");
            }
        }

        /// <summary>
        /// 处理终端推送的 2.22 协议签名结果。
        /// 终端发送 request_id、name、sex、id_no、doc_type、birthday、nationality、status（yes/no），
        /// 并映射为使用中文缩写字段名的 DLL 回调格式：ZJHM、ZJLB、GJDQDM、XM、XB、CSRQ。
        /// </summary>
        private async Task HandleProtocolAsync(string bodyUtf8,
            IPAddress sourceAddress)
        {
            var result = CallbackParser.ParseAuthorize(bodyUtf8);
            if (!result.Valid) { _log("[授权回调][警告] 数据无效"); return; }
            var route = await ResolveCallbackAsync(result.RequestId,
                ProxyResourceTypes.Protocol, "授权", bodyUtf8, sourceAddress)
                .ConfigureAwait(false);
            if (route == null)
                return;

            // 2.22 协议 status 字段："yes" 表示同意，"no" 表示拒绝
            var status = ExtractTopOrDataString(bodyUtf8, "status");

            // 将 2.22 协议终端字段映射回 DLL 中文缩写字段名
            // 终端字段 name、sex、id_no、doc_type、birthday、nationality、port_code
            // 对应 DLL 字段 XM、XB、ZJHM、ZJLB、CSRQ、GJDQDM、KADM
            var originalBody = route.OriginalRequestBodyUtf8;
            var name = CoalesceString(ExtractTopOrDataString(bodyUtf8, "name"),
                JsonHelper.ExtractString(originalBody, "XM"));
            var sex = CoalesceString(ExtractTopOrDataString(bodyUtf8, "sex"),
                JsonHelper.ExtractString(originalBody, "XB"));
            var idNo = CoalesceString(ExtractTopOrDataString(bodyUtf8, "id_no"),
                JsonHelper.ExtractString(originalBody, "ZJHM"));
            var docType = CoalesceString(ExtractTopOrDataString(bodyUtf8, "doc_type"),
                JsonHelper.ExtractString(originalBody, "ZJLB"));
            var birthday = CoalesceString(ExtractTopOrDataString(bodyUtf8, "birthday"),
                JsonHelper.ExtractString(originalBody, "CSRQ"));
            var nationality = CoalesceString(ExtractTopOrDataString(bodyUtf8, "nationality"),
                JsonHelper.ExtractString(originalBody, "GJDQDM"));
            var portCode = CoalesceString(ExtractTopOrDataString(bodyUtf8, "port_code"),
                JsonHelper.ExtractString(originalBody, "KADM"));  // KADM
            _log("[授权回调] 收到终端回调：请求ID=" + JsonHelper.ToLogValue(result.RequestId) +
                "，来源=" + JsonHelper.ToLogValue(sourceAddress?.ToString()) +
                "，状态=" + JsonHelper.ToLogValue(status) +
                "，证件号码=" + JsonHelper.ToLogValue(idNo) +
                "，证件类别=" + JsonHelper.ToLogValue(docType) +
                "，国家地区代码=" + JsonHelper.ToLogValue(nationality) +
                "，姓名=" + JsonHelper.ToLogValue(name) +
                "，性别=" + JsonHelper.ToLogValue(sex) +
                "，出生日期=" + JsonHelper.ToLogValue(birthday) +
                "，口岸代码=" + JsonHelper.ToLogValue(portCode));

            // 构建 DLL 回调载荷，使用与 Delphi 一致的中文缩写字段名格式
            var isYes = (status == "yes");
            var message = isYes ? "同意授权" : "旅客拒绝签署";
            var authResult = isYes ? "1" : "0";

            var payload = "{" +
                "\"request_id\":\"" + JsonHelper.EscapeString(route.DeliveryRequestId) + "\"," +
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

            _log("[授权回调] 转发至DLL：原始请求ID=" + JsonHelper.ToLogValue(result.RequestId) +
                "，投递请求ID=" + JsonHelper.ToLogValue(route.DeliveryRequestId) +
                "，授权结果=" + authResult +
                "，消息=" + JsonHelper.ToLogValue(message) +
                "，证件号码=" + JsonHelper.ToLogValue(idNo) +
                "，证件类别=" + JsonHelper.ToLogValue(docType) +
                "，国家地区代码=" + JsonHelper.ToLogValue(nationality) +
                "，姓名=" + JsonHelper.ToLogValue(name) +
                "，性别=" + JsonHelper.ToLogValue(sex) +
                "，出生日期=" + JsonHelper.ToLogValue(birthday) +
                "，口岸代码=" + JsonHelper.ToLogValue(portCode));
            if (!CanDeliver(route, "授权")) return;
            var delivery = await _dllCallback.PostCallbackRaw("/authorize", payload,
                route.CancellationToken).ConfigureAwait(false);
            FinishDelivery(route, delivery);
        }

        private static string ExtractTopOrDataString(string json, string key)
        {
            var value = JsonHelper.ExtractString(json, key);
            if (!string.IsNullOrEmpty(value))
                return value;

            var dataJson = JsonHelper.ExtractObject(json, "data");
            return JsonHelper.ExtractString(dataJson, key);
        }

        private static string CoalesceString(string primary, string fallback)
        {
            return !string.IsNullOrEmpty(primary) ? primary : (fallback ?? "");
        }

        /// <summary>
        /// 将 OCR 证据图光源类型映射为中文文件名，与 C++ DLL 保持一致。
        /// lampType/lamp_type: 1 → 可见光, 2 → 红外光, 3 → 紫外光
        /// imageType/image_type 不表示光源；image_type == 2 表示证件人像。
        /// </summary>
        private static string MapEvidenceImageName(JObject image)
        {
            var lampType = JsonHelper.ExtractInt(image, "lampType");
            if (lampType == 0)
                lampType = JsonHelper.ExtractInt(image, "lamp_type");
            switch (lampType)
            {
                case 1: return "可见光";
                case 2: return "红外光";
                case 3: return "紫外光";
                default: return "";
            }
        }

        /// <summary>
        /// 检查证据图像是否为证件人像图。根据 2.6 协议，imageType/image_type == 2 表示证件人像。
        /// </summary>
        private static bool IsOcrPortraitImage(JObject image)
        {
            var imageType = JsonHelper.ExtractInt(image, "imageType");
            if (imageType == 0)
                imageType = JsonHelper.ExtractInt(image, "image_type");
            return imageType == 2;
        }

        private void SaveEvidenceImages(JObject root, string saveDir, string requestId)
        {
            try
            {
                var data = root?["data"] as JObject;
                var imageItems = data?["evidence_images"] as JArray
                    ?? root?["evidence_images"] as JArray;
                if (imageItems == null || imageItems.Count == 0) return;

                var saveDir2 = PathHelper.EnsureRequestFolder(saveDir, requestId);
                bool savedVisible = false, savedInfrared = false, savedUltraviolet = false, savedPortrait = false;
                var savedNames = new System.Collections.Generic.List<string>();

                foreach (var token in imageItems)
                {
                    var image = token as JObject;
                    if (image == null) continue;

                    // 提取 Base64 数据：协议使用驼峰命名 imageData，同时兼容蛇形命名
                    var base64 = JsonHelper.ExtractString(image, "imageData");
                    if (string.IsNullOrEmpty(base64))
                        base64 = JsonHelper.ExtractString(image, "image_data");
                    if (string.IsNullOrEmpty(base64))
                        base64 = JsonHelper.ExtractString(image, "image_base64");
                    if (string.IsNullOrEmpty(base64)) continue;

                    // 按 lamp_type 保存光源图像，每种类型仅保存第一张
                    var lampName = MapEvidenceImageName(image);
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

                    // 保存证件人像（imageType == 2），仅保存第一张
                    if (!savedPortrait && IsOcrPortraitImage(image))
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
        /// 从 OCR 回调正文中提取 MRZ 信息并保存为 MRZ.json。
        /// 按 2.6 协议提取 MRZ1、MRZ2、MRZ3 和 person_info 数组。
        /// </summary>
        private void SaveMrzJson(JObject root, string saveDir, string requestId)
        {
            try
            {
                var data = root?["data"] as JObject;
                if (data == null) return;

                // 提取 MRZ 行，字段名与 C++ DLL 保持一致
                var mrz1 = data["MRZ1"]?.ToString() ?? "";
                var mrz2 = data["MRZ2"]?.ToString() ?? "";
                var mrz3 = data["MRZ3"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mrz1)) mrz1 = data["mrz1"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mrz2)) mrz2 = data["mrz2"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mrz3)) mrz3 = data["mrz3"]?.ToString() ?? "";

                // 提取 person_info 数组
                var personInfoArray = data["person_info"] as JArray;

                // 构建 MRZ.json
                var mrzObj = new JObject();
                mrzObj["request_id"] = requestId;

                var mrzLines = new JArray();
                if (!string.IsNullOrEmpty(mrz1)) mrzLines.Add(mrz1);
                if (!string.IsNullOrEmpty(mrz2)) mrzLines.Add(mrz2);
                if (!string.IsNullOrEmpty(mrz3)) mrzLines.Add(mrz3);
                mrzObj["mrz_lines"] = mrzLines;

                if (personInfoArray != null && personInfoArray.Count > 0)
                    mrzObj["person_info"] = personInfoArray;
                else
                    mrzObj["person_info"] = new JArray();

                var saveDir2 = PathHelper.EnsureRequestFolder(saveDir, requestId);
                var filePath = System.IO.Path.Combine(saveDir2, "MRZ.json");
                System.IO.File.WriteAllText(filePath,
                    mrzObj.ToString(Newtonsoft.Json.Formatting.Indented),
                    System.Text.Encoding.UTF8);
                _log($"[OCR] MRZ信息已保存：路径={filePath}");
            }
            catch (Exception ex)
            {
                _log($"[OCR] 保存MRZ信息异常: {ex.Message}");
            }
        }

        private string GetSaveDir(string requestId)
        {
            var terminalIndex = _terminalManager?.CurrentIndex ?? 0;
            return PathHelper.SafeResolveSaveDir(
                _processRegistry.GetCurrentSaveDir(terminalIndex));
        }

        private async Task<CallbackRoute> ResolveCallbackAsync(string requestId,
            string resourceType, string operation, string callbackBody,
            IPAddress sourceAddress)
        {
            if (_requestRegistry.TryClaimCallback(requestId, resourceType,
                out var context))
            {
                var current = _terminalManager.CurrentRoute;
                if (context.TerminalIndex > 0 &&
                    (context.TerminalIndex != current.TerminalIndex ||
                     !SourceMatchesTerminal(sourceAddress, context.TerminalIndex,
                         operation, requestId)))
                {
                    _requestRegistry.Fail(requestId, resourceType);
                    Logger.Warn($"[{operation}回调] 回调终端与请求路由不一致，已跳过：request_id={requestId}，请求终端={context.TerminalIndex}，当前终端={current.TerminalIndex}");
                    return null;
                }

                return new CallbackRoute(requestId, requestId, resourceType,
                    PathHelper.SafeResolveSaveDir(context.SaveDir),
                    context.CancellationToken, context.TerminalIndex,
                    current.RouteEpoch, false, context.OriginalRequestBodyUtf8);
            }

            if (!_processRegistry.TryGetByRequestId(requestId, out var session))
            {
                Logger.Warn($"[{operation}回调] 请求重复、已过期或未登记，已跳过：request_id={requestId}");
                return null;
            }

            var activeRoute = _terminalManager.CurrentRoute;
            if (session.TerminalIndex != activeRoute.TerminalIndex ||
                !SourceMatchesTerminal(sourceAddress, session.TerminalIndex,
                    operation, requestId))
            {
                Logger.Warn($"[{operation}回调] 来自非当前终端的流程回调已跳过：request_id={requestId}，回调终端={session.TerminalIndex}，当前终端={activeRoute.TerminalIndex}");
                return null;
            }

            if (!_processRegistry.TryReserveEvent(session, resourceType,
                callbackBody, out var deliveryRequestId))
            {
                Logger.Warn($"[{operation}回调] 流程事件重复或会话已失效，已跳过：request_id={requestId}");
                return null;
            }

            Logger.Debug($"[{operation}回调] 流程路由：process_request_id={requestId}，delivery_request_id={deliveryRequestId}，终端={session.TerminalIndex}");
            return new CallbackRoute(requestId, deliveryRequestId, resourceType,
                PathHelper.SafeResolveSaveDir(session.SaveDir),
                session.CancellationToken, session.TerminalIndex,
                activeRoute.RouteEpoch, true);
        }

        private bool SourceMatchesTerminal(IPAddress sourceAddress,
            int expectedTerminalIndex, string operation, string requestId)
        {
            if (sourceAddress == null)
            {
                Logger.Warn($"[{operation}回调] 缺少来源IP，已拒绝：request_id={requestId}，期望终端={expectedTerminalIndex}");
                return false;
            }

            if (!_terminalManager.TryResolveTerminalIndex(sourceAddress,
                out var sourceTerminalIndex))
            {
                Logger.Warn($"[{operation}回调] 来源IP不属于已配置终端，已拒绝：request_id={requestId}，来源={sourceAddress}，期望终端={expectedTerminalIndex}");
                return false;
            }

            if (sourceTerminalIndex == expectedTerminalIndex)
                return true;

            Logger.Warn($"[{operation}回调] 来源IP与回调会话不一致：request_id={requestId}，来源={sourceAddress}，来源终端={sourceTerminalIndex}，期望终端={expectedTerminalIndex}");
            return false;
        }

        private bool CanDeliver(CallbackRoute route, string operation)
        {
            if (route == null || route.CancellationToken.IsCancellationRequested)
                return false;

            if (route.TerminalIndex <= 0)
                return true;

            var current = _terminalManager.CurrentRoute;
            if (current.TerminalIndex == route.TerminalIndex &&
                current.RouteEpoch == route.RouteEpoch)
                return true;

            Logger.Warn($"[{operation}回调] 处理期间终端路由已变更，取消投递：request_id={route.SourceRequestId}，回调终端={route.TerminalIndex}，当前终端={current.TerminalIndex}");
            if (!route.Persistent)
                _requestRegistry.Fail(route.SourceRequestId, route.ResourceType);
            return false;
        }

        private void FinishDelivery(CallbackRoute route,
            CallbackDeliveryResult delivery)
        {
            if (route == null || route.Persistent)
            {
                if (route != null && delivery == CallbackDeliveryResult.Failed)
                    Logger.Warn($"[DLL回调] 流程事件投递失败，本次不重试且会话保持有效：process_request_id={route.SourceRequestId}，delivery_request_id={route.DeliveryRequestId}，资源={route.ResourceType}");
                return;
            }

            if (delivery == CallbackDeliveryResult.Delivered)
            {
                _requestRegistry.Complete(route.SourceRequestId,
                    route.ResourceType);
                return;
            }

            if (delivery == CallbackDeliveryResult.Failed)
            {
                Logger.Warn($"[DLL回调] 结果投递失败，请求立即结束且不重试：request_id={route.SourceRequestId}，资源={route.ResourceType}");
                _requestRegistry.Fail(route.SourceRequestId, route.ResourceType);
            }
        }

        private sealed class CallbackRoute
        {
            internal CallbackRoute(string sourceRequestId,
                string deliveryRequestId, string resourceType, string saveDir,
                CancellationToken cancellationToken, int terminalIndex,
                long routeEpoch, bool persistent,
                string originalRequestBodyUtf8 = "")
            {
                SourceRequestId = sourceRequestId;
                DeliveryRequestId = deliveryRequestId;
                ResourceType = resourceType;
                SaveDir = saveDir;
                OriginalRequestBodyUtf8 = originalRequestBodyUtf8 ?? "";
                CancellationToken = cancellationToken;
                TerminalIndex = terminalIndex;
                RouteEpoch = routeEpoch;
                Persistent = persistent;
            }

            internal string SourceRequestId { get; }
            internal string DeliveryRequestId { get; }
            internal string ResourceType { get; }
            internal string SaveDir { get; }
            internal string OriginalRequestBodyUtf8 { get; }
            internal CancellationToken CancellationToken { get; }
            internal int TerminalIndex { get; }
            internal long RouteEpoch { get; }
            internal bool Persistent { get; }
        }
    }
}
