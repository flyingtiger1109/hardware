# HZCYKJTHardWare C# 第三方调用示例

本目录提供与 Delphi 第三方 Demo 对齐的 C# WinForms 示例程序，目标框架为 `.NET Framework 4.6`，平台为 `x86`。

## 功能

- 初始化 / 释放 SDK
- 注册 DLL 事件回调
- 终端 1 / 终端 2 切换
- 开始流程 / 结束流程
- 摄像头预览 / 停止摄像头预览
- 指纹预览 / 停止指纹预览
- 人脸抓拍
- 指纹抓拍
- OCR 请求
- NFC/IC 请求
- 虹膜抓拍
- 授权模拟
- 日志窗口显示 DLL callback 事件摘要

## 部署

运行目录需要和 DLL、C# Proxy 后端放在一起：

```text
HZCYKJTHardWare.CSharpDemo.exe
HZCYKJTHardWare.dll
HZCYKJTHardWare.Proxy.exe
HZCYKJTHardWare.Proxy.exe.config
HZCYKJTHardWare.json
Newtonsoft.Json.dll
System.ValueTuple.dll
vlc\
```

业务配置统一修改 `HZCYKJTHardWare.json`。

## 构建

```text
dotnet build demo\CSharpThirdPartyDemo\HZCYKJTHardWare.CSharpDemo\HZCYKJTHardWare.CSharpDemo.csproj -c Release
```

注意：必须使用 `x86` 运行，避免 32 位 DLL 加载失败。
