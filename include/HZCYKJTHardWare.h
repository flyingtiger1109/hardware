#ifndef HZCYKJTHARDWARE_H
#define HZCYKJTHARDWARE_H

#include "HZCYKJTHardWare_types.h"

#ifdef __cplusplus
extern "C" {
#endif

/*
 * Public APIs for third-party integration.
 *
 * Return value: 1 = success / accepted, 0 = failure (see DLL logs for details).
 *
 *   [sync]    1 = operation completed, 0 = failed.
 *   [async]   1 = request submitted, final result delivered by callback event.
 *   [preview] 1 = request accepted, runtime state reported by callback events.
 *
 * Typical sequence:
 *   InitSdk -> RegisterEventCallback -> StartProcess -> business APIs ->
 *   EndProcess -> ReleaseSdk.
 */

/* SDK lifecycle [sync] */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_InitSdk(void);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_ReleaseSdk(void);

/* Event callback [sync] */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_RegisterEventCallback(
    THZCYKJTHardWareEventCallback callback
);

/* Preview control [preview] */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartCameraPreview(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopCameraPreview(void);

extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartFingerprintPreview(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopFingerprintPreview(void);

extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartIrisPreview(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopIrisPreview(void);

extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartPlatePreview(void* hwnd);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StopPlatePreview(void);

/* Capture [sync: face/fingerprint] [async: iris] */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureCameraImage(const char* saveDir);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureFingerprintImage(const char* saveDir);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_CaptureIrisImage(const char* saveDir);

/* OCR and IC card [async] */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestOCR(const char* saveDir);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_RequestNfcCard(const char* saveDir);

/* Process control [sync] */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_StartProcess(const char* saveDir);
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_EndProcess(void);

/* Terminal selection [sync] */
extern __declspec(dllexport) int __stdcall HZCYKJTHardWare_SwitchTerminal(int terminalIndex);

#ifdef __cplusplus
}
#endif

#endif /* HZCYKJTHARDWARE_H */
