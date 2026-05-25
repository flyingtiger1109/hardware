#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 解析后的人脸结果
struct FaceResult {
    bool valid = false;
    std::string image_base64;
    std::string image_mime_type;
    int width = 0;
    int height = 0;
    double face_score = 0.0;
    int face_capture = 0;
};

// 解析后的指纹结果
struct FingerprintResult {
    bool valid = false;
    std::string image_base64;
    std::string image_mime_type;
    int width = 0;
    int height = 0;
    double finger_score = 0.0;
};

// 证据图片项
struct EvidenceImage {
    std::string image_data;       // Base64 编码的图片数据
    std::string image_type;       // 0=原图, 1=裁剪证件图, 2=证件人像图等
    std::string lamp_type;        // 1=可见光, 2=红外, 3=紫外
    std::string card_type;
};

// 解析后的 OCR 结果
struct OcrResult {
    bool valid = false;
    std::string card_type;
    std::string person_info_json;
    std::string mrz;
    std::vector<EvidenceImage> evidence_images;
    std::string error_code;
    std::string message;
};

struct IrisResult {
    bool valid = false;
    std::string left_iris_base64;
    int left_width = 0;
    int left_height = 0;
    double left_score = 0.0;
    std::string right_iris_base64;
    int right_width = 0;
    int right_height = 0;
    double right_score = 0.0;
    std::string image_mime_type;
};

struct NfcCardResult {
    bool valid = false;
    std::string card_text;
    std::string ic_number;
};

// 回调结果解析器
class ResultParser {
public:
    // 解析人脸回调 JSON
    static FaceResult ParseFaceResult(const std::string& json);

    // 解析指纹回调 JSON
    static FingerprintResult ParseFingerprintResult(const std::string& json);

    // 解析 OCR 回调 JSON
    static OcrResult ParseOcrResult(const std::string& json);
    static IrisResult ParseIrisResult(const std::string& json);
    static NfcCardResult ParseNfcCardResult(const std::string& json);

    // 是否为失败回调
    static bool IsErrorResponse(const std::string& json,
                                std::string& errorCode,
                                std::string& message);

    // 从预览响应中提取 RTSP URL
    static std::string ExtractPreviewUrl(const std::string& json);
};

} // namespace HZCYKJTHardWare
