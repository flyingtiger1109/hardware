#include "pch.h"
#include "image_saver.h"
#include "include/HZCYKJTHardWare_types.h"
#include "base64.h"
#include "path_helper.h"
#include "logger.h"
#include <objidl.h>
#include <gdiplus.h>

#pragma comment(lib, "gdiplus.lib")

namespace HZCYKJTHardWare {

static int GetEncoderClsid(const WCHAR* format, CLSID* pClsid) {
    UINT num = 0;
    UINT size = 0;
    Gdiplus::GetImageEncodersSize(&num, &size);
    if (size == 0) return -1;

    std::vector<unsigned char> buffer(size);
    auto* encoders = reinterpret_cast<Gdiplus::ImageCodecInfo*>(buffer.data());
    Gdiplus::GetImageEncoders(num, size, encoders);

    for (UINT i = 0; i < num; ++i) {
        if (wcscmp(encoders[i].MimeType, format) == 0) {
            *pClsid = encoders[i].Clsid;
            return (int)i;
        }
    }
    return -1;
}

int ImageSaver::SaveBase64Image(const std::string& saveDir,
                                 const std::string& fileName,
                                 const std::string& base64,
                                 const std::string& mimeType,
                                 std::string& outPath) {
    if (base64.empty()) {
        LOG_ERROR("ImageSaver", "保存图片失败：base64 为空，file=%s", fileName.c_str());
        return HZCYKJTHardWare_RET_BASE64_FAILED;
    }

    // Base64 解码
    std::vector<unsigned char> imageData = Base64::Decode(base64);
    if (imageData.empty()) {
        LOG_ERROR("ImageSaver", "保存图片失败：base64 解码失败，file=%s", fileName.c_str());
        return HZCYKJTHardWare_RET_BASE64_FAILED;
    }

    // 确保目录存在
    if (!PathHelper::CreateDirectoryRecursive(saveDir)) {
        LOG_ERROR("ImageSaver", "保存文件失败：创建目录失败，dir=%s", saveDir.c_str());
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    // 确定扩展名
    std::string ext = GetExtensionFromMimeType(mimeType);
    outPath = PathHelper::Join(saveDir, fileName + ext);

    // 如果文件已存在，添加序号
    std::string finalPath = outPath;
    int counter = 1;
    while (PathHelper::FileExists(finalPath)) {
        finalPath = PathHelper::Join(saveDir, fileName + "_" + std::to_string(counter) + ext);
        counter++;
    }
    outPath = finalPath;

    // 写入文件
    std::wstring wPath = PathHelper::Utf8ToWide(outPath);
    std::ofstream file(wPath, std::ios::binary);
    if (!file.is_open()) {
        LOG_ERROR("ImageSaver", "保存文件失败：打开文件失败，path=%s", outPath.c_str());
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    file.write((const char*)imageData.data(), imageData.size());
    file.close();

    LOG_DEBUG("ImageSaver", "图片已保存：path=%s，bytes=%zu", outPath.c_str(), imageData.size());
    return HZCYKJTHardWare_RET_OK;
}

int ImageSaver::SaveJsonFile(const std::string& saveDir,
                              const std::string& fileName,
                              const std::string& jsonContent,
                              std::string& outPath) {
    if (!PathHelper::CreateDirectoryRecursive(saveDir)) {
        LOG_ERROR("ImageSaver", "保存JSON失败：创建目录失败，dir=%s", saveDir.c_str());
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    outPath = PathHelper::Join(saveDir, fileName + ".json");

    // 如果文件已存在，添加序号
    std::string finalPath = outPath;
    int counter = 1;
    while (PathHelper::FileExists(finalPath)) {
        finalPath = PathHelper::Join(saveDir, fileName + "_" + std::to_string(counter) + ".json");
        counter++;
    }
    outPath = finalPath;

    std::wstring wPath = PathHelper::Utf8ToWide(outPath);
    std::ofstream file(wPath, std::ios::out);
    if (!file.is_open()) {
        LOG_ERROR("ImageSaver", "保存JSON失败：打开文件失败，path=%s", outPath.c_str());
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    file << jsonContent;
    file.close();

    LOG_DEBUG("ImageSaver", "JSON已保存：path=%s", outPath.c_str());
    return HZCYKJTHardWare_RET_OK;
}

int ImageSaver::SaveBase64ImageAsJpeg(const std::string& saveDir,
                                       const std::string& fileName,
                                       const std::string& base64,
                                       std::string& outPath) {
    if (base64.empty()) {
        LOG_ERROR("ImageSaver", "保存JPEG失败：base64 为空，file=%s", fileName.c_str());
        return HZCYKJTHardWare_RET_BASE64_FAILED;
    }

    std::vector<unsigned char> imageData = Base64::Decode(base64);
    if (imageData.empty()) {
        LOG_ERROR("ImageSaver", "保存JPEG失败：base64 解码失败，file=%s", fileName.c_str());
        return HZCYKJTHardWare_RET_BASE64_FAILED;
    }

    if (!PathHelper::CreateDirectoryRecursive(saveDir)) {
        LOG_ERROR("ImageSaver", "保存JPEG失败：创建目录失败，dir=%s", saveDir.c_str());
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    outPath = PathHelper::Join(saveDir, fileName + ".jpg");
    std::string finalPath = outPath;
    int counter = 1;
    while (PathHelper::FileExists(finalPath)) {
        finalPath = PathHelper::Join(saveDir, fileName + "_" + std::to_string(counter) + ".jpg");
        counter++;
    }
    outPath = finalPath;

    HGLOBAL hGlobal = GlobalAlloc(GMEM_MOVEABLE, imageData.size());
    if (!hGlobal) {
        LOG_ERROR("ImageSaver", "保存JPEG失败：GlobalAlloc 失败");
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    void* mem = GlobalLock(hGlobal);
    if (!mem) {
        GlobalFree(hGlobal);
        LOG_ERROR("ImageSaver", "保存JPEG失败：GlobalLock 失败");
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }
    memcpy(mem, imageData.data(), imageData.size());
    GlobalUnlock(hGlobal);

    IStream* stream = nullptr;
    HRESULT hr = CreateStreamOnHGlobal(hGlobal, TRUE, &stream);
    if (FAILED(hr) || !stream) {
        GlobalFree(hGlobal);
        LOG_ERROR("ImageSaver", "保存JPEG失败：CreateStreamOnHGlobal hr=0x%08lx", (unsigned long)hr);
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    Gdiplus::GdiplusStartupInput gdiplusInput;
    ULONG_PTR gdiplusToken = 0;
    Gdiplus::Status status = Gdiplus::GdiplusStartup(&gdiplusToken, &gdiplusInput, nullptr);
    if (status != Gdiplus::Ok) {
        stream->Release();
        LOG_ERROR("ImageSaver", "保存JPEG失败：GdiplusStartup status=%d", (int)status);
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    int ret = HZCYKJTHardWare_RET_OK;
    {
        Gdiplus::Bitmap bitmap(stream);
        if (bitmap.GetLastStatus() != Gdiplus::Ok) {
            LOG_ERROR("ImageSaver", "保存JPEG失败：GDI+ 解码图片失败，status=%d", (int)bitmap.GetLastStatus());
            ret = HZCYKJTHardWare_RET_BASE64_FAILED;
        } else {
            CLSID jpegClsid;
            if (GetEncoderClsid(L"image/jpeg", &jpegClsid) < 0) {
                LOG_ERROR("ImageSaver", "保存JPEG失败：未找到 JPEG encoder");
                ret = HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
            } else {
                std::wstring wPath = PathHelper::Utf8ToWide(outPath);
                Gdiplus::EncoderParameters params;
                params.Count = 1;
                params.Parameter[0].Guid = Gdiplus::EncoderQuality;
                params.Parameter[0].Type = Gdiplus::EncoderParameterValueTypeLong;
                params.Parameter[0].NumberOfValues = 1;
                ULONG quality = 90;
                params.Parameter[0].Value = &quality;

                status = bitmap.Save(wPath.c_str(), &jpegClsid, &params);
                if (status != Gdiplus::Ok) {
                    LOG_ERROR("ImageSaver", "保存JPEG失败：path=%s，status=%d", outPath.c_str(), (int)status);
                    ret = HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
                } else {
                    LOG_DEBUG("ImageSaver", "JPEG已保存：path=%s", outPath.c_str());
                }
            }
        }
    }

    Gdiplus::GdiplusShutdown(gdiplusToken);
    stream->Release();
    return ret;
}

std::string ImageSaver::GetExtensionFromMimeType(const std::string& mimeType) {
    if (mimeType.find("image/bmp") != std::string::npos) return ".bmp";
    if (mimeType.find("image/jpeg") != std::string::npos) return ".jpg";
    if (mimeType.find("image/png") != std::string::npos) return ".png";
    if (mimeType.find("image/gif") != std::string::npos) return ".gif";
    if (mimeType.find("image/tiff") != std::string::npos) return ".tif";
    return ".bin";
}

std::string ImageSaver::BuildSavePath(const std::string& rootDir,
                                       const std::string& requestId,
                                       bool createDateFolder,
                                       bool createRequestFolder) {
    std::string basePath = rootDir;
    if (basePath.empty()) {
        basePath = PathHelper::GetDllDir();
        basePath = PathHelper::Join(basePath, "captures");
    }

    if (createDateFolder) {
        basePath = PathHelper::Join(basePath, PathHelper::GetDateString());
    }

    if (createRequestFolder && !requestId.empty()) {
        basePath = PathHelper::Join(basePath, PathHelper::GetTimeString() + "_" + requestId);
    }

    return basePath;
}

} // namespace HZCYKJTHardWare
