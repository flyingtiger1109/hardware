#include "pch.h"
#include "result_parser.h"
#include "json_helper.h"
#include "logger.h"

namespace HZCYKJTHardWare {

static std::string GetStringOrNumber(const std::string& json, const std::string& key) {
    std::string value = JsonHelper::GetString(json, key);
    if (!value.empty()) return value;
    if (!JsonHelper::HasKey(json, key)) return "";
    return std::to_string(JsonHelper::GetInt(json, key, 0));
}

bool ResultParser::IsErrorResponse(const std::string& json,
                                    std::string& errorCode,
                                    std::string& message) {
    std::string status = JsonHelper::GetString(json, "status");
    if (status == "error" || status == "failed" || status == "rejected") {
        errorCode = JsonHelper::GetString(json, "error_code");
        message = JsonHelper::GetString(json, "message");
        return true;
    }
    std::string topCode = JsonHelper::GetString(json, "error_code");
    if (!topCode.empty() && topCode != "0") {
        errorCode = topCode;
        message = JsonHelper::GetString(json, "message");
        return true;
    }
    // 也检查 data 中的 error_code
    std::string dataObj = JsonHelper::GetJsonObject(json, "data");
    if (!dataObj.empty()) {
        std::string code = JsonHelper::GetString(dataObj, "error_code");
        if (!code.empty() && code != "0") {
            errorCode = code;
            message = JsonHelper::GetString(dataObj, "message");
            return true;
        }
    }
    return false;
}

FaceResult ResultParser::ParseFaceResult(const std::string& json) {
    FaceResult result;
    std::string dataObj = JsonHelper::GetJsonObject(json, "data");
    if (dataObj.empty()) {
        LOG_ERROR("ResultParser", "人脸回调解析失败：缺少 data 字段");
        return result;
    }

    // 尝试从 data 中提取字段
    result.image_base64 = JsonHelper::GetString(dataObj, "image_base64");
    result.image_mime_type = JsonHelper::GetString(dataObj, "image_mime_type");
    result.width = JsonHelper::GetInt(dataObj, "face_width", 0);
    result.height = JsonHelper::GetInt(dataObj, "face_height", 0);
    result.face_score = JsonHelper::GetDouble(dataObj, "face_score", 0.0);
    result.face_capture = JsonHelper::GetInt(dataObj, "face_capture", 0);

    // 兼容其他可能的字段命名
    if (result.image_base64.empty()) {
        result.image_base64 = JsonHelper::GetString(dataObj, "imageData");
    }
    if (result.width == 0) {
        result.width = JsonHelper::GetInt(dataObj, "width", 0);
    }
    if (result.height == 0) {
        result.height = JsonHelper::GetInt(dataObj, "height", 0);
    }

    result.valid = !result.image_base64.empty();
    if (!result.valid) {
        LOG_ERROR("ResultParser", "人脸回调解析失败：image_base64 为空");
    } else {
        LOG_DEBUG("ResultParser", "人脸回调解析成功：%dx%d，mime=%s，score=%.2f",
                 result.width, result.height, result.image_mime_type.c_str(), result.face_score);
    }

    return result;
}

FingerprintResult ResultParser::ParseFingerprintResult(const std::string& json) {
    FingerprintResult result;
    std::string dataObj = JsonHelper::GetJsonObject(json, "data");
    if (dataObj.empty()) {
        LOG_ERROR("ResultParser", "指纹回调解析失败：缺少 data 字段");
        return result;
    }

    result.image_base64 = JsonHelper::GetString(dataObj, "image_base64");
    result.image_mime_type = JsonHelper::GetString(dataObj, "image_mime_type");
    result.width = JsonHelper::GetInt(dataObj, "width", 0);
    result.height = JsonHelper::GetInt(dataObj, "height", 0);
    result.finger_score = JsonHelper::GetDouble(dataObj, "finger_score", 0.0);

    // 兼容其他可能的字段命名
    if (result.image_base64.empty()) {
        result.image_base64 = JsonHelper::GetString(dataObj, "imageData");
    }

    result.valid = !result.image_base64.empty();
    if (!result.valid) {
        LOG_ERROR("ResultParser", "指纹回调解析失败：image_base64 为空");
    } else {
        LOG_DEBUG("ResultParser", "指纹回调解析成功：%dx%d，mime=%s，score=%.2f",
                 result.width, result.height, result.image_mime_type.c_str(), result.finger_score);
    }

    return result;
}

OcrResult ResultParser::ParseOcrResult(const std::string& json) {
    OcrResult result;

    std::string dataObj = JsonHelper::GetJsonObject(json, "data");
    if (dataObj.empty()) {
        LOG_ERROR("ResultParser", "OCR回调解析失败：缺少 data 字段");
        return result;
    }

    result.card_type = GetStringOrNumber(dataObj, "card_type");
    result.person_info_json = JsonHelper::GetArray(dataObj, "person_info");
    if (result.person_info_json.empty()) {
        result.person_info_json = JsonHelper::GetJsonObject(dataObj, "person_info");
    }

    // 解析 evidence_images 数组
    std::string evidenceArr = JsonHelper::GetArray(dataObj, "evidence_images");
    if (!evidenceArr.empty()) {
        size_t pos = 0;
        while (pos < evidenceArr.size()) {
            size_t objStart = evidenceArr.find('{', pos);
            if (objStart == std::string::npos) break;

            int depth = 0;
            size_t objEnd = objStart;
            while (objEnd < evidenceArr.size()) {
                if (evidenceArr[objEnd] == '{') depth++;
                else if (evidenceArr[objEnd] == '}') {
                    depth--;
                    if (depth == 0) break;
                } else if (evidenceArr[objEnd] == '"') {
                    objEnd++;
                    while (objEnd < evidenceArr.size()) {
                        if (evidenceArr[objEnd] == '\\') { objEnd += 2; continue; }
                        if (evidenceArr[objEnd] == '"') break;
                        objEnd++;
                    }
                }
                objEnd++;
            }
            if (objEnd >= evidenceArr.size()) break;

            std::string imgObj = evidenceArr.substr(objStart, objEnd - objStart + 1);

            EvidenceImage img;
            img.image_data = JsonHelper::GetString(imgObj, "imageData");
            if (img.image_data.empty()) {
                img.image_data = JsonHelper::GetString(imgObj, "image_data");
                if (img.image_data.empty()) {
                    img.image_data = JsonHelper::GetString(imgObj, "image_base64");
                }
            }
            img.image_type = GetStringOrNumber(imgObj, "imageType");
            if (img.image_type.empty() || img.image_type == "0") {
                img.image_type = GetStringOrNumber(imgObj, "image_type");
            }
            img.lamp_type = GetStringOrNumber(imgObj, "lampType");
            if (img.lamp_type.empty() || img.lamp_type == "0") {
                img.lamp_type = GetStringOrNumber(imgObj, "lamp_type");
            }
            img.card_type = GetStringOrNumber(imgObj, "cardType");
            if (img.card_type.empty() || img.card_type == "0") {
                img.card_type = GetStringOrNumber(imgObj, "card_type");
            }

            if (!img.image_data.empty()) {
                result.evidence_images.push_back(img);
            }

            pos = objEnd + 1;
        }
    }

    // 拼接 MRZ 信息：MRZ1^MRZ2^MRZ3
    std::string mrz1 = JsonHelper::GetString(dataObj, "MRZ1");
    std::string mrz2 = JsonHelper::GetString(dataObj, "MRZ2");
    std::string mrz3 = JsonHelper::GetString(dataObj, "MRZ3");
    if (!mrz1.empty() || !mrz2.empty() || !mrz3.empty()) {
        result.mrz = mrz1 + "^" + mrz2 + "^" + mrz3;
    }

    result.valid = true;
    LOG_DEBUG("ResultParser", "OCR回调解析成功：card_type=%s，person_info=%s，evidence_images=%zu，mrz=%s",
             result.card_type.c_str(),
             result.person_info_json.empty() ? "none" : "present",
             result.evidence_images.size(),
             result.mrz.c_str());

    return result;
}

IrisResult ResultParser::ParseIrisResult(const std::string& json) {
    IrisResult result;
    std::string dataObj = JsonHelper::GetJsonObject(json, "data");
    if (dataObj.empty()) {
        LOG_ERROR("ResultParser", "虹膜回调解析失败：缺少 data 字段");
        return result;
    }

    result.left_iris_base64 = JsonHelper::GetString(dataObj, "left_eye_image_base64");
    if (result.left_iris_base64.empty()) {
        result.left_iris_base64 = JsonHelper::GetString(dataObj, "leftIris_capture");
    }
    result.left_width = JsonHelper::GetInt(dataObj, "left_eye_width", 0);
    if (result.left_width == 0) {
        result.left_width = JsonHelper::GetInt(dataObj, "leftIris_width", 0);
    }
    result.left_height = JsonHelper::GetInt(dataObj, "left_eye_height", 0);
    if (result.left_height == 0) {
        result.left_height = JsonHelper::GetInt(dataObj, "leftIris_height", 0);
    }
    result.left_score = JsonHelper::GetDouble(dataObj, "left_eye_quality", 0.0);
    if (result.left_score == 0.0) {
        result.left_score = JsonHelper::GetDouble(dataObj, "leftIris_score", 0.0);
    }

    result.right_iris_base64 = JsonHelper::GetString(dataObj, "right_eye_image_base64");
    if (result.right_iris_base64.empty()) {
        result.right_iris_base64 = JsonHelper::GetString(dataObj, "rightIris_capture");
    }
    result.right_width = JsonHelper::GetInt(dataObj, "right_eye_width", 0);
    if (result.right_width == 0) {
        result.right_width = JsonHelper::GetInt(dataObj, "rightIris_width", 0);
    }
    result.right_height = JsonHelper::GetInt(dataObj, "right_eye_height", 0);
    if (result.right_height == 0) {
        result.right_height = JsonHelper::GetInt(dataObj, "rightIris_height", 0);
    }
    result.right_score = JsonHelper::GetDouble(dataObj, "right_eye_quality", 0.0);
    if (result.right_score == 0.0) {
        result.right_score = JsonHelper::GetDouble(dataObj, "rightIris_score", 0.0);
    }

    result.image_mime_type = JsonHelper::GetString(dataObj, "image_mime_type");
    result.valid = !result.left_iris_base64.empty() || !result.right_iris_base64.empty();
    if (!result.valid) {
        LOG_ERROR("ResultParser", "虹膜回调解析失败：左右眼图像均为空");
    } else {
        LOG_DEBUG("ResultParser", "虹膜回调解析成功：left=%dx%d，right=%dx%d，mime=%s",
                 result.left_width, result.left_height,
                 result.right_width, result.right_height,
                 result.image_mime_type.c_str());
    }

    return result;
}

NfcCardResult ResultParser::ParseNfcCardResult(const std::string& json) {
    NfcCardResult result;
    std::string dataObj = JsonHelper::GetJsonObject(json, "data");
    if (dataObj.empty()) {
        LOG_ERROR("ResultParser", "IC卡识别回调解析失败：缺少 data 字段");
        return result;
    }

    result.card_text = GetStringOrNumber(dataObj, "card_text");
    result.ic_number = result.card_text;
    result.valid = !result.ic_number.empty();
    if (!result.valid) {
        LOG_ERROR("ResultParser", "IC卡识别回调解析失败：card_text 为空");
    } else {
        LOG_DEBUG("ResultParser", "IC卡识别回调解析成功：card_text=%s", result.card_text.c_str());
    }

    return result;
}

std::string ResultParser::ExtractPreviewUrl(const std::string& json) {
    std::string url = JsonHelper::GetString(json, "preview_url");
    if (!url.empty()) return url;

    // 也可能是 rtsp_url
    url = JsonHelper::GetString(json, "rtsp_url");
    if (!url.empty()) return url;

    // 尝试从 data 中提取
    std::string dataObj = JsonHelper::GetJsonObject(json, "data");
    if (!dataObj.empty()) {
        url = JsonHelper::GetString(dataObj, "preview_url");
        if (url.empty()) {
            url = JsonHelper::GetString(dataObj, "rtsp_url");
        }
    }

    return url;
}

} // namespace HZCYKJTHardWare
