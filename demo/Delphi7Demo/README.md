# HZCYKJTHardWare Delphi 服务端示例

本示例程序现在作为 DLL 的 Delphi 后端服务示例使用，DLL 通信监听地址由 `HZCYKJTHardWare.json` 的 `delphi_server.host/port` 配置。

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

预览接口返回实际目标 `render_hwnd`。DLL 传入外部有效 HWND 时，本程序让 libVLC 直接绑定该窗口，并通过原生裁切参数按视频比例居中裁切铺满；不会回退到窗体内 Panel。服务启动时不自动开启预览。

## 使用顺序

1. 将本程序、`HZCYKJTHardWare.dll` 与两者共用的 `HZCYKJTHardWare.json` 放在同一目录。
2. 启动 `demo\DelphiThirdPartyDemo` 或其他第三方 Demo。
3. 第三方调用 DLL 的 `InitSdk`；当 `delphi_server.auto_start=true` 时，DLL 会在 `/ping` 不可用时自动启动本程序；若同路径程序已存在但通信服务在初始化 8 秒内仍不可用，DLL 会重启本程序后重新等待 `/ping`。
4. 确认日志显示 JSON 所配置的 DLL 通信服务和终端回调监听地址均启动成功。
5. 调用抓拍、OCR、NFC、预览等接口验证转发链路。

## 保存路径与日志

- 摄像头和指纹同步抓拍收到带扩展名的路径时，按该完整路径覆盖保存，不追加请求编号或设备后缀。
- 相对保存路径以本程序 EXE 所在目录为基准。
- 后端日志文件位于 `HZCYKJTHardWareExe_Logs\HZCYKJTHardWareExe_Logs_yyyyMMdd.log`。

## 端口配置

- `delphi_server.port`：本程序监听，DLL 访问本程序 `/ping` 和业务端点，默认 `8080`。
- `terminal_callback_server.port`：本程序监听，终端回调本程序，默认 `8081`。
- `callback_server.port`：DLL 监听，本程序向 DLL 回传异步结果，默认 `39091`。
- `terminal.port`：本程序主动请求终端设备使用的目标端口，默认 `9098`，不是本机监听端口。

## 注意

- 该示例使用 WinSock 实现最小 HTTP 服务，目的是兼容 Delphi 7 并演示协议。
- `terminal_callback_server.public_host` 留空时，终端回调地址自动使用检测到的本机局域网 IP；不要把 `0.0.0.0` 作为发送给终端的回调地址。
- 示例保存的是 `.txt` 占位文件，不是真实图片。
- 真实服务端建议补充 UTF-8 路径处理、终端异常码映射、VLC 句柄生命周期管理和更完整的日志。
