# HZCYKJTHardWare Delphi 第三方调用示例

本目录用于模拟现版本第三方程序调用 `HZCYKJTHardWare.dll`。

## 角色

- 本示例只调用 DLL 对外导出接口。
- 本示例不直接调用终端 HTTP 服务。
- DLL 会根据 `HZCYKJTHardWare.json` 转发到 Delphi 服务程序 `http://127.0.0.1:8080`。

## 使用顺序

1. 先启动 `demo\Delphi7Demo`，确认 Delphi 服务端监听 `127.0.0.1:8080`。
2. 确认本目录下存在当前版本 `HZCYKJTHardWare.dll` 和 `HZCYKJTHardWare.json`。
3. 编译并运行本示例。
4. 调用初始化、流程控制、抓拍、OCR、NFC、预览等按钮，验证现版本第三方调用链路。

## 预期链路

```text
DelphiThirdPartyDemo
  -> HZCYKJTHardWare.dll
  -> http://127.0.0.1:8080
  -> Delphi7Demo 服务端示例
  -> DLL callback_server:39091
  -> DelphiThirdPartyDemo 回调函数
```

## 注意

- 本示例保留原第三方 DLL 调用方式，用于验证对外接口兼容性。
- 真实第三方程序只需要按原方式加载 DLL，不需要感知终端 HTTP、图片保存、VLC 渲染等内部迁移。
- 如需验证预览窗口，先让服务端示例运行，再从本示例调用相机预览接口。
