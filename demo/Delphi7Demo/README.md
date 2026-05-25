# HZCYKJTHardWare Delphi 服务端示例

本示例程序现在作为 DLL 的 Delphi 后端服务示例使用，监听 `127.0.0.1:8080`。

## 角色

- DLL 调用本程序的 HTTP 端点。
- 本程序示例化返回同步抓拍结果、异步 OCR/NFC/虹膜回调、预览 ready 回调。
- 真实项目中需要把 `DelphiProxyServer.pas` 中的模拟逻辑替换为终端 HTTP 调用、终端回调解析、文件保存和 VLC 预览渲染。

## 已实现端点

- `GET/POST /ping`
- `POST /process/start`
- `POST /process/end`
- `POST /capture/face`
- `POST /capture/fingerprint`
- `POST /capture/iris`
- `POST /ocr`
- `POST /nfc`
- `POST /preview/camera/start`
- `POST /preview/camera/stop`

## 回调 DLL

异步接口使用 DLL 传入的 `callback_url` 回调：

- `/HZCYKJTHardWare/callback/ocr`
- `/HZCYKJTHardWare/callback/iris`
- `/HZCYKJTHardWare/callback/nfc-card`
- `/HZCYKJTHardWare/callback/preview-ready`

预览示例返回窗体内黑色 Panel 的 HWND 作为 `vlc_hwnd`，同时返回宿主 Panel 的 `delphi_host_hwnd`，便于 DLL 在停止预览或 ReleaseSdk 时把窗口还回 Delphi 程序。

## 使用顺序

1. 编译并启动本程序。
2. 确认日志显示 `服务已启动：http://127.0.0.1:8080`。
3. 启动 `demo\DelphiThirdPartyDemo` 或其他第三方 Demo。
4. 第三方调用 DLL 的 `InitSdk`，DLL 会访问本程序 `/ping`。
5. 调用抓拍、OCR、NFC、预览等接口验证转发链路。

## 注意

- 该示例使用 WinSock 实现最小 HTTP 服务，目的是兼容 Delphi 7 并演示协议。
- 示例保存的是 `.txt` 占位文件，不是真实图片。
- 真实服务端建议补充 UTF-8 路径处理、终端异常码映射、VLC 句柄生命周期管理和更完整的日志。
