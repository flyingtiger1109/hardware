#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// 图片保存模块
class ImageSaver {
public:
    // 从 Base64 解码并保存图片
    // saveDir: 保存根目录
    // fileName: 不含扩展名的文件名
    // base64: Base64 数据
    // mimeType: MIME 类型
    // outPath: 输出完整路径
    // 返回 HZCYKJTHardWare_RET_OK 或错误码
    static int SaveBase64Image(const std::string& saveDir,
                               const std::string& fileName,
                               const std::string& base64,
                               const std::string& mimeType,
                               std::string& outPath);

    // 从 Base64 解码图片并转码保存为 JPEG
    static int SaveBase64ImageAsJpeg(const std::string& saveDir,
                                     const std::string& fileName,
                                     const std::string& base64,
                                     std::string& outPath);

    // 将完整 JPEG 原样保存到调用方指定的精确路径。
    // 先写同目录临时文件，再原子替换目标文件，避免第三方读到半文件。
    static int SaveJpegFileAtomic(const std::string& exactPath,
                                  const std::vector<unsigned char>& jpegData);

    // 保存 JSON 文本到文件
    static int SaveJsonFile(const std::string& saveDir,
                            const std::string& fileName,
                            const std::string& jsonContent,
                            std::string& outPath);

    // 根据 MIME 类型获取文件扩展名
    static std::string GetExtensionFromMimeType(const std::string& mimeType);

    // 生成保存子目录路径（可选日期/请求ID子目录）
    static std::string BuildSavePath(const std::string& rootDir,
                                     const std::string& requestId,
                                     bool createDateFolder,
                                     bool createRequestFolder);
};

} // HZCYKJTHardWare 命名空间结束
