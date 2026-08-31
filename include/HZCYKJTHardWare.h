#ifndef HZCYKJTHARDWARE_H
#define HZCYKJTHARDWARE_H

#include "HZCYKJTHardWare_types.h"

#ifdef __cplusplus
extern "C" {
#endif

/*
 * 面向第三方集成的公共 API。
 *
 * 既有接口返回值：1 表示成功或已受理，0 表示失败；详细原因参见 DLL 日志。
 * HZCYKJTHardWare_SaveLatestPlateFrame 是新增的同步例外，成功返回 1，
 * 失败直接返回负数错误码。
 *
 *   [同步] 1 表示操作完成，0 表示操作失败。
 *   [异步] 1 表示请求已提交，最终结果通过回调事件返回。
 *   [预览] 1 表示请求已受理，运行状态通过回调事件返回。
 *
 * 典型调用顺序：
 *   InitSdk -> RegisterEventCallback -> StartProcess -> 业务 API ->
 *   最后调用 EndProcess -> ReleaseSdk。
 */

/* SDK 生命周期［同步］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_InitSdk(void);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_ReleaseSdk(void);

/* 事件回调［同步］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_RegisterEventCallback(
    THZCYKJTHardWareEventCallback callback
);

/* 预览控制［预览］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartCameraPreview(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopCameraPreview(void);

extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartFingerprintPreview(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopFingerprintPreview(void);

extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartIrisPreview(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopIrisPreview(void);

/* 车牌相机通过相互独立的扁平 API 对外提供。业务组合由调用方选择，
 * DLL 和 Proxy 不解析 Direction。 */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreviewCJ(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreviewCJ(void);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreviewRJ2(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreviewRJ2(void);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreviewRJ3(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreviewRJ3(void);

/* 获取已经运行的车牌预览链路中的最新完整 JPEG，并原子保存到指定文件。［同步］
 * 返回值：成功返回 HZCYKJTHardWare_RET_OK；失败直接返回负数错误码。
 * cameraType 使用 HZCYKJTHardWare_PLATE_CAMERA_CJ/RJ2/RJ3。
 * 调用方不需要申请或释放图片 Buffer；对应车牌预览必须已先启动。 */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_SaveLatestPlateFrame(
    const char* savePath,
    int cameraType
);

/* 图像采集［人脸/指纹：同步；虹膜：异步］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureCameraImage(const char* saveDir);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureFingerprintImage(const char* saveDir, const char* saveDirHk);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureIrisImage(const char* saveDir);

/* OCR 与 IC 卡［异步］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestOCR(const char* saveDir);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestNfcCard(const char* saveDir);

/* 流程控制［同步］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartProcess(const char* saveDir);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_EndProcess(void);

/* 授权请求［异步］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestAuthorize(
    const char* ZJHM,
    const char* ZJLB,
    const char* GJDQDM,
    const char* XM,
    const char* XB,
    const char* CSRQ,
    const char* KADM
);

/* 终端选择［同步］ */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_SwitchTerminal(int terminalIndex);

#ifdef __cplusplus
}
#endif

#endif /* HZCYKJTHARDWARE_H 结束 */
