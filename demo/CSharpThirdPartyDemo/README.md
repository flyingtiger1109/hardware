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
- ID 卡 OCR 人员信息与光学鉴伪结果展示
- NFC/IC 请求
- 虹膜抓拍
- 授权模拟
- 日志窗口显示 DLL callback 事件摘要

当 OCR 回调中的 `card_type` 为 `30` 时，`mrz` 字段使用兼容格式 `$证号^鉴伪分数^出生日期^签发日期^姓名^性别`，日志窗口标记为“ID卡兼容串”；同时继续显示 `name`、`sex`、`cardId`、`birthday`、`dateOfissue`、`authen_score` 和 `optical_check_result`。光学鉴伪结果中 `0` 表示通过、`1` 表示不通过、`-1` 表示未知或未检测。

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
