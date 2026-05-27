# HZCYKJTHardWare Delphi 第三方调用示例

本目录用于模拟现版本第三方程序调用 `HZCYKJTHardWare.dll`。

## 角色

- 本示例只调用 DLL 对外导出接口。
- 本示例不直接调用终端 HTTP 服务。
- DLL 会根据 `HZCYKJTHardWare.json` 转发到 Delphi 服务程序；默认地址为 `http://127.0.0.1:8080`。
- 当 `delphi_server.auto_start=true` 时，`InitSdk` 可在服务未启动时自动启动与 DLL 同目录的 Delphi EXE；如检测到同路径 EXE 已运行但通信服务在初始化 8 秒内仍不可用，DLL 会自动重启该 EXE。

## 使用顺序

1. 确认本目录下存在当前版本 `HZCYKJTHardWare.dll`、`HZCYKJTHardWare.exe` 和 `HZCYKJTHardWare.json`。
2. 编译并运行本示例。
3. 调用初始化；默认配置下 DLL 会自动启动 Delphi 服务程序并等待 `/ping` 成功。
4. 调用流程控制、抓拍、OCR、NFC、预览等按钮，验证现版本第三方调用链路。

## 预期链路

```text
DelphiThirdPartyDemo
  -> HZCYKJTHardWare.dll
  -> http://127.0.0.1:8080
  -> Delphi7Demo 服务端示例
  -> DLL callback_server:39091
  -> DelphiThirdPartyDemo 回调函数
```

## 端口配置

- `delphi_server.port`：Delphi 服务监听、DLL 请求，默认 `8080`。
- `terminal_callback_server.port`：Delphi 服务监听、终端回调，默认 `8081`。
- `callback_server.port`：DLL 监听、Delphi 服务回调，默认 `39091`。
- `terminal.port`：Delphi 服务请求终端设备的目标端口，默认 `9098`。

## 注意

- 本示例保留原第三方 DLL 调用方式，用于验证对外接口兼容性。
- 真实第三方程序只需要按原方式加载 DLL，不需要感知终端 HTTP、图片保存、VLC 渲染等内部迁移。
- 如需验证预览窗口，先初始化成功，确认服务端已被启动并可达，再从本示例调用相机预览接口。
- 摄像头和指纹抓拍传入带扩展名的路径（例如 `.\captures\111.jpg`）时，图片按该名称保存，不追加设备或请求后缀；未指定具体文件名时使用 JSON 的 `camera_default_path` / `fingerprint_default_path`。
- 预览画面视觉上按传入窗口区域居中裁切铺满；Delphi 服务端使用本进程覆盖容器承载 VLC，并随第三方窗口位置及自然遮挡关系更新。
- 本示例的摄像头和指纹启动按钮通过后台线程调用 DLL，结果通过窗口消息回到 UI 线程，避免第三方界面线程在预览启动期间被同步阻塞。
- Delphi 服务端自身的预览与本示例发起的第三方预览为独立会话；终端支持并发流时可同时显示同一种资源。
