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

static bool HasJpegSignature(const std::vector<unsigned char>& jpegData) {
    return jpegData.size() >= 4 &&
        jpegData[0] == 0xFF && jpegData[1] == 0xD8 &&
        jpegData[jpegData.size() - 2] == 0xFF &&
        jpegData[jpegData.size() - 1] == 0xD9;
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

int ImageSaver::SaveJpegFileAtomic(
    const std::string& exactPath,
    const std::vector<unsigned char>& jpegData) {
    constexpr size_t kMaxJpegBytes = 8U * 1024U * 1024U;
    if (exactPath.empty()) {
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：目标路径为空");
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }
    if (PathHelper::GetFileName(exactPath).empty()) {
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：目标路径不包含文件名，path=%s",
                  exactPath.c_str());
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }
    if (jpegData.empty() || !HasJpegSignature(jpegData)) {
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：JPEG数据无效，path=%s，bytes=%zu",
                  exactPath.c_str(), jpegData.size());
        return HZCYKJTHardWare_FRAME_DATA_INVALID;
    }
    if (jpegData.size() > kMaxJpegBytes) {
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：数据超过大小限制，path=%s，bytes=%zu，limit=%zu",
                  exactPath.c_str(), jpegData.size(), kMaxJpegBytes);
        return HZCYKJTHardWare_FRAME_TOO_LARGE;
    }

    const std::string parentDir = PathHelper::GetParentDir(exactPath);
    if (!parentDir.empty() && !PathHelper::CreateDirectoryRecursive(parentDir)) {
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：创建父目录失败，dir=%s",
                  parentDir.c_str());
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    const std::wstring targetPath = PathHelper::Utf8ToWide(exactPath);
    if (targetPath.empty()) {
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：路径转换失败，path=%s",
                  exactPath.c_str());
        return HZCYKJTHardWare_RET_INVALID_PARAM;
    }

    HANDLE tempHandle = INVALID_HANDLE_VALUE;
    std::wstring tempPath;
    for (unsigned int attempt = 0; attempt < 10; ++attempt) {
        tempPath = targetPath + L".tmp." +
            std::to_wstring(GetCurrentProcessId()) + L"." +
            std::to_wstring(GetCurrentThreadId()) + L"." +
            std::to_wstring(attempt);
        tempHandle = CreateFileW(tempPath.c_str(), GENERIC_WRITE, 0, nullptr,
                                 CREATE_NEW, FILE_ATTRIBUTE_TEMPORARY, nullptr);
        if (tempHandle != INVALID_HANDLE_VALUE) break;
        if (GetLastError() != ERROR_FILE_EXISTS &&
            GetLastError() != ERROR_ALREADY_EXISTS) {
            break;
        }
    }

    if (tempHandle == INVALID_HANDLE_VALUE) {
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：创建临时文件失败，path=%s，错误码=%lu",
                  exactPath.c_str(), GetLastError());
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    bool writeOk = true;
    size_t offset = 0;
    while (offset < jpegData.size()) {
        const DWORD chunkSize = static_cast<DWORD>(
            (std::min)(jpegData.size() - offset,
                       static_cast<size_t>(0x7ffff000U)));
        DWORD written = 0;
        if (!WriteFile(tempHandle, jpegData.data() + offset, chunkSize,
                       &written, nullptr) || written != chunkSize) {
            writeOk = false;
            break;
        }
        offset += written;
    }
    if (writeOk && !FlushFileBuffers(tempHandle)) writeOk = false;
    CloseHandle(tempHandle);

    if (!writeOk) {
        const DWORD error = GetLastError();
        DeleteFileW(tempPath.c_str());
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：写入临时文件失败，path=%s，错误码=%lu",
                  exactPath.c_str(), error);
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    if (!MoveFileExW(tempPath.c_str(), targetPath.c_str(),
                     MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        const DWORD error = GetLastError();
        DeleteFileW(tempPath.c_str());
        LOG_ERROR("ImageSaver", "保存最新车牌JPEG失败：原子替换目标文件失败，path=%s，错误码=%lu",
                  exactPath.c_str(), error);
        return HZCYKJTHardWare_RET_SAVE_FILE_FAILED;
    }

    LOG_DEBUG("ImageSaver", "最新车牌JPEG已保存：path=%s，bytes=%zu",
              exactPath.c_str(), jpegData.size());
    return HZCYKJTHardWare_RET_OK;
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

} // HZCYKJTHardWare 命名空间结束
