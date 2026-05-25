# HZCYKJTHardWare Delphi 第三方调用示例

本目录用于模拟现版本第三方程序调用 `HZCYKJTHardWare.dll`。

## 角色

- 本示例只调用 DLL 对外导出接口。
- 本示例不直接调用终端 HTTP 服务。
- DLL 会根据 `HZCYKJTHardWare.json` 转发到 Delphi 服务程序；默认地址为 `http://127.0.0.1:8080`。
- 当 `delphi_server.auto_start=true` 时，`InitSdk` 可在服务未启动时自动启动与 DLL 同目录的 Delphi EXE。

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
