# 基线快照 — v1.2

**日期：** 2026-06-29
**Git 标签：** v1.2
**远程仓库：** git@gitee.com:go-on-duty-to-touch-big-fish/HZCYJKTHardWare.git

## 项目状态

| 项目 | 目标框架 | 平台 | 语言版本 |
|------|---------|------|---------|
| HZCYKJTHardWare.Proxy | net46 | x86 | C# 7.3 |
| HZCYKJTHardWare (DLL) | vcxproj | x86 | C++ (MSVC) |

## DLL 导出表（23 个函数）

来源：`HZCYKJTHardWare.def`（基线副本：`.baseline/exports.def`、`.baseline/exports_v1.2.def`）

```
HZCYKJTHardWare_InitSdk
HZCYKJTHardWare_ReleaseSdk
HZCYKJTHardWare_SwitchTerminal
HZCYKJTHardWare_StartProcess
HZCYKJTHardWare_EndProcess
HZCYKJTHardWare_StartCameraPreview
HZCYKJTHardWare_StopCameraPreview
HZCYKJTHardWare_StartFingerprintPreview
HZCYKJTHardWare_StopFingerprintPreview
HZCYKJTHardWare_StartIrisPreview
HZCYKJTHardWare_StopIrisPreview
HZCYKJTHardWare_StartPlatePreview
HZCYKJTHardWare_StopPlatePreview
HZCYKJTHardWare_CaptureCameraImage
HZCYKJTHardWare_CaptureFingerprintImage
HZCYKJTHardWare_CaptureIrisImage
HZCYKJTHardWare_RequestOCR
HZCYKJTHardWare_RequestNfcCard
HZCYKJTHardWare_RequestAuthorize
HZCYKJTHardWare_RegisterEventCallback
```

全部使用 `__stdcall` 调用约定，返回 `int`（1=成功/0=失败）。

## 回调 JSON 样例

### OCR 结果回调（ocr_result.json）
- 保存位置：`.baseline/ocr_callback_sample.json`
- 关键字段：`request_id`, `data.MRZ1/MRZ2/MRZ3`, `data.person_info`, `data.evidence_images`

### MRZ 信息（MRZ.json）
- 保存位置：`.baseline/mrz_sample.json`
- 关键字段：`request_id`, `mrz_lines`, `person_info`

## C# Proxy 核心模块代码行数

| 模块 | 文件 | 行数 |
|------|------|------|
| ProxyServer | `Server/ProxyServer.cs` | ~980 |
| WorkerQueue | `Core/WorkerQueue.cs` | ~283 |
| QueueManager | `Core/QueueManager.cs` | ~224 |
| RequestRegistry | `Core/RequestRegistry.cs` | ~266 |
| DllCommandHandler | `Server/DllCommandHandler.cs` | ~701 |
| TerminalCallbackHandler | `Server/TerminalCallbackHandler.cs` | ~496 |
| PreviewManager | `Preview/PreviewManager.cs` | ~610 |
| MainForm | `MainForm.cs` | ~1375 |

## DLL 核心模块

| 模块 | 文件 | 备注 |
|------|------|------|
| exports.cpp | `src/exports.cpp` | ~1960 行，含 6 个 DllTaskQueue + BusyGuard + 23 个导出函数 |
| request_session_manager | `src/request_session_manager.cpp` | ~270 行，CRITICAL_SECTION + std::map |
| delphi_proxy | `src/delphi_proxy.cpp` | HTTP 客户端 → C# Proxy |
| callback_server | `src/callback_server.cpp` | DLL 内部 HTTP 回调接收 |
| event_dispatcher | `src/event_dispatcher.cpp` | 事件 → 第三方 callback |

## 测试基线（阶段 0 新建）

| 测试套件 | 文件 | 测试数 | 状态 |
|---------|------|--------|------|
| WorkerQueueTests | `Tests/Core/WorkerQueueTests.cs` | 5 | ✅ 全部通过 |
| RequestRegistryTests | `Tests/Core/RequestRegistryTests.cs` | 17 | ✅ 全部通过 |
| ProxyServerIntegrationTests | `Tests/Integration/ProxyServerIntegrationTests.cs` | 7 | ⚠️ 需 URL 预留（`netsh http add urlacl`） |

**总计：** 22 个核心单元测试通过，7 个集成测试待环境配置。

## 下一步

进入阶段 1：Runtime 基础 — ActiveTasksTracker + TransportLayer 提取
