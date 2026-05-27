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
- `POST /preview/fingerprint/start`
- `POST /preview/fingerprint/stop`
- `POST /preview/iris/start`
- `POST /preview/iris/stop`

## 回调 DLL

异步接口使用 DLL 传入的 `callback_url` 回调：

- `/HZCYKJTHardWare/callback/ocr`
- `/HZCYKJTHardWare/callback/iris`
- `/HZCYKJTHardWare/callback/nfc-card`
- `/HZCYKJTHardWare/callback/preview-ready`

预览接口返回第三方提供的目标锚点 `render_hwnd`。DLL 传入外部有效 HWND 时，本程序创建自身进程内的无边框覆盖容器，按外部窗口客户区位置进行跟随，并将 libVLC 子窗口挂载到该本地容器中。该方式保持视觉上的居中裁切铺满，同时避免跨进程子窗口销毁导致的阻塞。Delphi 界面预览和第三方预览维护独立播放会话，可并发显示同一资源。服务启动时不自动开启预览。

## 使用顺序

1. 将本程序、`HZCYKJTHardWare.dll` 与两者共用的 `HZCYKJTHardWare.json` 放在同一目录。
2. 启动 `demo\DelphiThirdPartyDemo` 或其他第三方 Demo。
3. 第三方调用 DLL 的 `InitSdk`；当 `delphi_server.auto_start=true` 时，DLL 会在 `/ping` 不可用时自动启动本程序；若同路径程序已存在但通信服务不可用，DLL 会立即重启本程序，再按 `start_wait_ms` 等待新服务就绪。
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
- 覆盖容器仅在锚点不可见或所属顶层窗口最小化时隐藏，并遵循第三方窗口的自然遮挡层级。
