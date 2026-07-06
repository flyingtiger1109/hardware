# HZCYKJTHardWare

面向 Windows 采集终端的硬件 SDK。项目提供原生 C++ DLL，并通过本地 C# Proxy 与采集终端、预览服务通信；第三方程序可通过稳定的 C ABI 调用 SDK 并接收异步事件。

## 运行架构

```text
第三方程序（C++ / C# / Delphi / Python）
                │  __stdcall C ABI
                ▼
       HZCYKJTHardWare.dll
                │  HTTP（本机）
                ▼
   HZCYKJTHardWare.Proxy.exe
                │  HTTP / RTSP / 回调
                ▼
            采集终端设备
```

DLL 和 Proxy 通过同一份 `HZCYKJTHardWare.json` 协同工作。默认情况下，DLL 可按配置自动启动 `HZCYKJTHardWare.Proxy.exe`。

## 兼容性要求

- Windows 10 / Windows 11；部署到 Windows 7 前需单独完成现场验证。
- DLL 支持 Win32 和 x64 构建；调用方、DLL、依赖库的位数必须一致。
- 当前 C# Proxy 的目标框架为 `.NET Framework 4.6`，平台为 `x86`。
- DLL 对外导出使用 `extern "C"` 和 `__stdcall`；不得自行改变导出名、参数顺序或调用约定。
- 向 DLL 传入的路径和字符串参数使用 UTF-8 `const char*`。

## 部署

将以下文件部署到同一运行目录：

- `HZCYKJTHardWare.dll`
- `HZCYKJTHardWare.json`
- `HZCYKJTHardWare.Proxy.exe` 及其 .NET、VLC 和其他运行依赖

默认配置中 DLL 通过 `http://127.0.0.1:8089` 访问 Proxy；Proxy 的终端回调服务监听 `8088`。端口、终端地址、保存目录和超时均可在 `HZCYKJTHardWare.json` 中配置。

运行前应确认：

1. 调用方、DLL 和 Proxy 同为 x86，或全部切换为 x64；
2. 配置文件与 DLL 位于同一目录；
3. Proxy 的 VLC 依赖完整且未被安全软件隔离；
4. 终端网络、端口和本机防火墙策略允许通信。

## 标准调用流程

```text
InitSdk
  → RegisterEventCallback
  → StartProcess
  → 终端切换 / 预览 / 抓拍 / OCR / NFC / 授权
  → EndProcess
  → ReleaseSdk
```

- 所有接口返回 `1` 表示成功或请求已受理；返回非 `1` 表示失败，具体错误码见 `include/HZCYKJTHardWare_types.h`。
- 人脸、指纹抓拍为同步接口。
- 虹膜、OCR、NFC 和授权为异步接口，最终结果通过已注册事件回调返回。
- 预览启动请求被受理后，运行结果也通过事件回调通知。

## 导出接口

头文件：[`include/HZCYKJTHardWare.h`](include/HZCYKJTHardWare.h)

| 类别 | 接口 |
|---|---|
| 生命周期 | `HZCYKJTHardWare_InitSdk`、`HZCYKJTHardWare_ReleaseSdk` |
| 事件回调 | `HZCYKJTHardWare_RegisterEventCallback` |
| 终端和流程 | `HZCYKJTHardWare_SwitchTerminal`、`HZCYKJTHardWare_StartProcess`、`HZCYKJTHardWare_EndProcess` |
| 预览 | `Start/StopCameraPreview`、`Start/StopFingerprintPreview`、`Start/StopIrisPreview`、`Start/StopPlatePreviewCJ`、`Start/StopPlatePreviewRJ2`、`Start/StopPlatePreviewRJ3` |
| 图像采集 | `HZCYKJTHardWare_CaptureCameraImage`、`HZCYKJTHardWare_CaptureFingerprintImage`、`HZCYKJTHardWare_CaptureIrisImage` |
| 异步识别 | `HZCYKJTHardWare_RequestOCR`、`HZCYKJTHardWare_RequestNfcCard`、`HZCYKJTHardWare_RequestAuthorize` |

预览接口的 `hwnd` 必须是仍然有效的目标窗口句柄。调用方关闭窗口前应先停止对应预览，再释放 SDK。

车牌 RTSP 由 C# Proxy 中的 libVLC 播放，CJ、RJ2、RJ3 分别绑定第三方提供的独立 HWND。Proxy 不判断业务方向：方向 1 调用方自行调用 CJ，方向 2 调用方自行组合调用 RJ2 和 RJ3。三路会话可独立启停，也可并发显示。

三个镜头分别配置在 `preview.plate.cj`、`preview.plate.rj2`、`preview.plate.rj3`。每个节点独立包含 `enabled`、`host`、`port`、`username`、`password` 和 `stream_channel`。

## C# 调用示例

```csharp
using System;
using System.Runtime.InteropServices;

internal static class Native
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void EventCallback(IntPtr eventJsonUtf8);

    [DllImport("HZCYKJTHardWare.dll", CallingConvention = CallingConvention.StdCall)]
    internal static extern int HZCYKJTHardWare_InitSdk();

    [DllImport("HZCYKJTHardWare.dll", CallingConvention = CallingConvention.StdCall)]
    internal static extern int HZCYKJTHardWare_RegisterEventCallback(EventCallback callback);

    [DllImport("HZCYKJTHardWare.dll", CallingConvention = CallingConvention.StdCall)]
    internal static extern int HZCYKJTHardWare_StartProcess([MarshalAs(UnmanagedType.LPUTF8Str)] string saveDir);

    [DllImport("HZCYKJTHardWare.dll", CallingConvention = CallingConvention.StdCall)]
    internal static extern int HZCYKJTHardWare_ReleaseSdk();
}
```

请将回调委托保存在长期有效的字段中，避免被 GC 回收。回调线程不是 UI 线程；WinForms/WPF 更新界面时必须切回 UI 线程。

## 配置要点

`HZCYKJTHardWare.json` 的主要配置段：

| 配置段 | 用途 |
|---|---|
| `delphi_server` | Proxy 的地址、端口、自动启动及可执行文件名；名称为历史兼容字段。 |
| `terminal_callback_server` | Proxy 提供给采集终端的回调监听地址、端口和路径。 |
| `terminal` | 终端发现模式、固定地址或自动网段地址。 |
| `callback_server` | DLL 接收 Proxy 回调的监听配置。 |
| `timeout` | HTTP、抓拍、OCR 和授权超时。 |
| `save` | 默认保存路径与目录创建规则。 |
| `preview` | VLC、RTSP 缓冲和预览恢复策略。 |
| `log` | DLL 日志目录和最低级别。 |

修改端口或终端网络配置后，应同步检查 DLL、Proxy 和终端三端的地址是否一致。

## 构建

### DLL

- Visual Studio 工具集：`v145`
- 语言标准：C++20
- 主要项目：`HZCYKJTHardWare.vcxproj`
- 建议优先验证：`Release|Win32`

### C# Proxy

```powershell
dotnet build .\demo\CSharpProxy\HZCYKJTHardWare.Proxy\HZCYKJTHardWare.Proxy.csproj -c Release -p:Platform=x86
```

若正式输出目录中的 Proxy 正在运行，VLC 文件可能被锁定。此时应先停止 Proxy，或使用独立输出目录完成编译验证。

## 故障排查

- `InitSdk` 失败：确认 DLL 同目录存在有效的 `HZCYKJTHardWare.json`，并检查 DLL 日志。
- Proxy 无法启动：检查 `delphi_server.executable`、x86/x64 一致性和运行依赖。
- 预览失败：确认传入的 `HWND` 有效、VLC 依赖完整、终端可返回预览地址。
- OCR/NFC 未收到结果：检查终端回调地址、端口、防火墙及 DLL 事件回调注册顺序。
- 终端切换失败：检查 `terminal.mode`、网段配置和两台终端的可达性。

## 验证建议

发布前至少完成以下验证：

- DLL `Release|Win32` 与 C# Proxy `x86 Release` 编译；
- 第三方 Demo 初始化、释放和重复初始化；
- 双终端切换后的预览、抓拍、OCR、NFC 与授权；
- 终端断开、重连、超时和 Proxy 重启；
- 长时间运行下的内存、句柄、线程和日志目录监控。
