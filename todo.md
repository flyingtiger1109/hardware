# HZCYKJTHardWare.DLL 椤圭洰杩涘害

## 宸插畬鎴?
- [x] 椤圭洰缁撴瀯鍒涘缓 (CDZD.slnx / CDZD.vcxproj)
- [x] DLL 瀵煎嚭鎺ュ彛瀹氫箟 (include/CDZD.h)
- [x] 浜嬩欢绫诲瀷涓庨敊璇爜瀹氫箟 (include/cdzd_types.h)
- [x] x86/x64 Win32/Release 鍥涢厤缃?- [x] HZCYKJTHardWare.json 榛樿閰嶇疆鏂囦欢
- [x] 閰嶇疆鏂囦欢璇诲彇妯″潡 (config_manager)
- [x] 鏃ュ織妯″潡 (logger) 鈥?绾跨▼瀹夊叏銆佹寜鏃ユ粴鍔?- [x] 缃戠粶妫€娴嬫ā鍧?(network_detector) 鈥?GetAdaptersAddresses
- [x] 缁堢鍒囨崲妯″潡 (terminal_manager) 鈥?auto_subnet/fixed_url/manual
- [x] HTTP 璇锋眰妯″潡 (http_client) 鈥?WinHTTP
- [x] CallbackServer 鍩虹妗嗘灦 鈥?winsock HTTP server
- [x] RequestSessionManager 鈥?request_id銆佽秴鏃躲€佺姸鎬佺鐞?- [x] 缁撴灉瑙ｆ瀽妯″潡 (result_parser) 鈥?浜鸿劯/鎸囩汗/OCR JSON 瑙ｆ瀽
- [x] 鍥剧墖淇濆瓨妯″潡 (image_saver) 鈥?Base64 瑙ｇ爜銆佹墿灞曞悕鍒ゆ柇銆佷腑鏂囪矾寰?- [x] EventDispatcher 鈥?worker 绾跨▼銆丼EH 淇濇姢
- [x] PreviewManager 鈥?鎽勫儚澶?鎸囩汗鍙岃矾棰勮
- [x] LibVlcRtspRenderer 妗嗘灦 鈥?鍔ㄦ€佸姞杞?libVLC
- [x] Base64 缂栬В鐮佹ā鍧?- [x] JSON 杈呭姪瑙ｆ瀽绫?- [x] 璺緞杈呭姪妯″潡 (path_helper)
- [x] 鍏ㄥ眬涓婁笅鏂?(cdzd_context)
- [x] 鎵€鏈夊鍑哄嚱鏁板疄鐜?(exports.cpp) 鈥?__stdcall + SEH 淇濇姢
- [x] 缁堢鐘舵€佹娴?(terminal_status_checker)
- [x] README.md
- [x] DemoCpp 娴嬭瘯绋嬪簭

## 寰呰仈璋?
- [ ] HZCYKJTHardWare.json 鍔犺浇鑱旇皟
- [ ] 鍥哄畾 IP 閰嶇疆妯″紡鑱旇皟
- [ ] 鑷姩璇嗗埆 192.168 缃戞鑱旇皟
- [ ] 鍙岀綉鍗?preferred_subnet_prefix 鑱旇皟
- [ ] 缁堢 1锛?92.168.x.10 鑱旇皟
- [ ] 缁堢 2锛?92.168.x.11 鑱旇皟
- [ ] CallbackServer 灞€鍩熺綉鍥炶皟鍦板潃鑱旇皟
- [ ] 榛樿淇濆瓨璺緞鑱旇皟
- [ ] 浜鸿劯棰勮 RTSP URL 鑱旇皟
- [ ] 鎸囩汗棰勮 RTSP URL 鑱旇皟
- [ ] 浜鸿劯鎶撴媿鍥炶皟鑱旇皟
- [ ] 鎸囩汗鎶撴媿鍥炶皟鑱旇皟
- [ ] OCR 鍥炶皟鑱旇皟
- [ ] x86 鐗堟湰鍦?32 浣嶇▼搴忎腑鍔犺浇楠岃瘉
- [ ] x64 鐗堟湰鍦?64 浣嶇▼搴忎腑鍔犺浇楠岃瘉
- [ ] Windows 11 鏈湴鐪熷疄纭欢鑱旇皟
- [ ] Windows 10 姝ｅ紡鐜楠岃瘉
- [ ] Windows 7 32 浣嶅吋瀹规€ч獙璇?
## 椋庨櫓璁板綍

- HZCYKJTHardWare.DLL 涓嶈礋璐ｅ惎鍔ㄩ噰闆嗙粓绔湇鍔★紝鍥犳璋冪敤鍓嶅繀椤讳繚璇侀噰闆嗙粓绔湇鍔″凡杩愯銆?- 姝ｅ紡浜や粯鐗╀负 HZCYKJTHardWare.DLL + HZCYKJTHardWare.json銆?- HZCYKJTHardWare.json 闇€瑕佹斁鍦?HZCYKJTHardWare.DLL 鍚岀洰褰曘€?- 绗笁鏂圭數鑴戝鏋滄湁澶氫釜 192.168 缃戞锛岄渶瑕侀€氳繃 preferred_subnet_prefix 鎸囧畾姝ｇ‘缃戞銆?- callback_url 涓嶈兘浣跨敤 127.0.0.1锛屽繀椤讳娇鐢ㄧ粓绔彲璁块棶鐨勬湰鏈哄眬鍩熺綉 IP銆?- RTSP 娓叉煋渚濊禆 libVLC锛岄渶瑕佸尯鍒?x86/x64 渚濊禆銆?- 32 浣嶇涓夋柟绋嬪簭鍙兘鍔犺浇 x86/HZCYKJTHardWare.DLL銆?- 64 浣嶇涓夋柟绋嬪簭鍙兘鍔犺浇 x64/HZCYKJTHardWare.DLL銆?- 閲囬泦缁撴灉蹇呴』浣跨敤 request_id 涓ユ牸鍖归厤锛岀姝娇鐢ㄦ渶鍚庝竴娆＄紦瀛樸€?- EndProcess 鍚庣殑杩熷埌鍥炶皟涓嶅緱杞彂缁欑涓夋柟銆?- SwitchTerminal 鍚庣殑鏃у洖璋冧笉寰楄浆鍙戠粰绗笁鏂广€?- 濡傛灉缁堢鏈嶅姟鏃犳硶璁块棶锛孌LL 鍙兘杩斿洖 CDZD_RET_TERMINAL_UNREACHABLE锛屼笉寰楄嚜琛屽惎鍔?EXE銆?- Windows 7 32 浣嶇幆澧冨彲鑳藉瓨鍦ㄨ繍琛屽簱銆乀LS銆乴ibVLC 鐗堟湰鍏煎闂锛岄渶瑕佸崟鐙獙璇併€?- 濡傛灉 HZCYKJTHardWare.json 鏍煎紡閿欒锛孋DZD_InitSdk 搴旇繑鍥?CDZD_RET_CONFIG_INVALID銆?- 鏈」鐩笉鐢熸垚 Mock 妯″紡銆傛墍鏈夊姛鑳戒笌鐪熷疄缁堢鑱旇皟銆?
## Delphi 代理架构改造记录（2026-05-22）

### 阶段 1：配置层

已完成：
- [x] 已备份现有 DLL 产物和 Delphi 示例到 `backup_before_delphi_proxy_20260522_151729`。
- [x] `HZCYKJTHardWare.json` 新增 `delphi_server.host/port`，默认 `127.0.0.1:8080`。
- [x] `config_manager` 新增 Delphi 服务 host、port、URL 默认值和解析逻辑。
- [x] `hzsjkjt_context` 新增 `delphi_server_url`。
- [x] 预览上下文预留第三方 HWND、VLC HWND、Delphi 宿主 HWND 和 request_id 字段，后续支持“默认挂 Delphi 宿主窗口，第三方预览时挂入第三方 HWND，停止或异常时归还 Delphi 宿主窗口”。

未完成：
- [x] 阶段 2：新增 `DelphiProxy`。
- [x] 阶段 3：`InitSdk/ReleaseSdk` 改为 Delphi 代理流程。
- [x] 阶段 4：业务导出函数改为转发 Delphi。
- [x] 阶段 5：Delphi 精简回调映射到第三方事件。
- [x] 阶段 6：实现预览窗口 SetParent/MoveWindow 和归还 Delphi 宿主窗口。

验证方法：
- 编译 `HZCYKJTHardWare.vcxproj`，确认配置层新增字段不破坏现有构建。
- 后续 InitSdk 改造时确认缺失 `delphi_server` 配置仍默认连接 `http://127.0.0.1:8080`。

回退说明：
- 使用 `backup_before_delphi_proxy_20260522_151729` 中的 DLL 产物和 Delphi 示例恢复运行版本。
- 删除 `HZCYKJTHardWare.json` 中 `delphi_server` 节点。
- 移除 `ConfigManager` 的 Delphi 服务字段和 getter。
- 移除 `HzsjkjtContext` 新增 Delphi URL 和预览 HWND 字段。

### 阶段 2：转发层

已完成：
- [x] 新增 `src/delphi_proxy.h` / `src/delphi_proxy.cpp`。
- [x] 封装 `GET /ping`、`POST /process/start`、`POST /process/end`。
- [x] 封装同步抓拍 `/capture/face`、`/capture/fingerprint`，成功时提取 Delphi 返回的 `save_path`。
- [x] 封装异步 `/capture/iris`、`/ocr`、`/nfc`，以 `accepted=true` 作为受理成功。
- [x] 封装 `/preview/camera/start`、`/preview/camera/stop`，请求体保留第三方 HWND 和 callback_url。
- [x] 统一处理 Delphi 错误格式 `{ "error": true, "code": "...", "message": "..." }`。
- [x] 增加 JSON 字符串转义，避免 Windows 路径和 callback_url 破坏请求体。
- [x] `HZCYKJTHardWare.vcxproj` 已加入新增文件编译项。

未完成：
- [x] 阶段 3：`InitSdk/ReleaseSdk` 调用 `DelphiProxy.Ping()` 并清理旧初始化依赖。
- [x] 阶段 4：`exports.cpp` 业务函数实际改为调用 `DelphiProxy`。
- [x] 阶段 5：Delphi 回调结果映射到第三方事件。
- [x] 阶段 6：预览窗口收到 `preview-ready` 后执行 SetParent/MoveWindow；停止或异常时归还 Delphi 宿主窗口。

验证方法：
- `Release|Win32` 编译通过，0 warning / 0 error。
- 后续阶段 3 使用 Delphi `/ping` 联调验证 `DelphiProxy.Ping()`。
- 后续阶段 4 使用 Delphi mock/真实服务逐项验证代理端点。

回退说明：
- 从 `HZCYKJTHardWare.vcxproj` 移除 `src\delphi_proxy.h` 和 `src\delphi_proxy.cpp` 编译项。
- 删除 `src/delphi_proxy.h` 和 `src/delphi_proxy.cpp`。
- 阶段 2 未替换业务调用点，删除新增代理层即可恢复到阶段 1 行为。

### 阶段 3：InitSdk / ReleaseSdk

已完成：
- [x] `InitSdk` 不再强制调用 `NetworkDetector`。
- [x] `InitSdk` 不再强制初始化 `TerminalManager`。
- [x] `InitSdk` 读取 `delphi_server` 并写入 `ctx.delphi_server_url`。
- [x] `InitSdk` 启动 `CallbackServer` 和 `EventDispatcher` 后调用 `DelphiProxy.Ping()`。
- [x] Delphi 程序未启动或 `/ping` 失败时，`InitSdk` 返回失败并停止已启动的 callback/event 资源。
- [x] `ReleaseSdk` 不再调用旧 `PreviewManager::StopAll()`。
- [x] `ReleaseSdk` 预留预览清理逻辑：若已有 VLC HWND 和 Delphi 宿主 HWND，则尝试归还；若已有预览 request_id，则通知 Delphi 停止预览。

未完成：
- [x] 阶段 4：业务导出函数已从旧终端直连改为 `DelphiProxy`。
- [x] 阶段 5：回调解析已改为 Delphi 精简 JSON 映射。
- [x] 阶段 6：实际保存 preview session、处理 `preview-ready`、执行 SetParent/MoveWindow。

验证方法：
- `Release|Win32` 编译通过，0 warning / 0 error。
- 联调时先启动 Delphi 程序并监听 `127.0.0.1:8080`，`GET /ping` 返回 `{"status":"ok"}`。
- 调用 `HZCYKJTHardWare_InitSdk()`，预期返回 1；关闭 Delphi 后调用，预期返回 0 且日志提示 `/ping` 失败。

回退说明：
- 恢复 `exports.cpp` 中旧 `InitSdk` 的 network_detector + terminal_manager 初始化流程。
- 恢复 `ReleaseSdk` 中旧 `PreviewManager::StopAll()` 调用。
- 保留阶段 1/2 文件时不影响旧流程，但可按阶段 1/2 回退说明继续移除。

### 阶段 4：业务函数转发

已完成：
- [x] `StartProcess` / `EndProcess` 改为调用 `DelphiProxy.ProcessStart/ProcessEnd`。
- [x] `StartCameraPreview` 改为调用 `DelphiProxy.StartCameraPreview`，保存 request_id 和第三方 HWND，等待后续 `preview-ready` 回调完成嵌入。
- [x] `StopCameraPreview` 改为调用 `DelphiProxy.StopCameraPreview`，并尝试归还 VLC HWND 到 Delphi 宿主窗口。
- [x] `CaptureCameraImage` 改为调用 `/capture/face`，成功时只接收 Delphi 返回的 `save_path`。
- [x] `CaptureFingerprintImage` 改为调用 `/capture/fingerprint`，成功时只接收 Delphi 返回的 `save_path`。
- [x] `CaptureIrisImage` 改为调用 `/capture/iris`，Delphi 返回 `accepted=true` 后立即返回成功。
- [x] `RequestOCR` 改为调用 `/ocr`，Delphi 返回 `accepted=true` 后立即返回成功。
- [x] `RequestNfcCard` 改为调用 `/nfc`，Delphi 返回 `accepted=true` 后立即返回成功。
- [x] `SwitchTerminal` 在代理模式下只记录第三方选择，不再直连或切换终端；实际终端由 Delphi 程序管理。
- [x] `SetTerminalBaseUrl` / `SwitchTerminalByUrl` 不再调用旧 `TerminalManager`，代理模式下返回 unsupported 日志。
- [x] `CheckTerminalStatus` 改为 `DelphiProxy.Ping()`。
- [x] `exports.cpp` 中已清除对 `TerminalManager`、`PreviewManager`、`ResultParser`、`ImageSaver` 的直接调用。

未完成：
- [ ] 指纹预览、虹膜预览、车牌预览当前没有定义 Delphi HTTP 端点，代理模式下返回 unsupported；如需支持，需要补充 Delphi 协议。
- [x] 阶段 5：回调解析已改为 Delphi 精简 JSON 映射。
- [x] 阶段 6：实际处理 `preview-ready` 并执行 SetParent/MoveWindow。

验证方法：
- `Release|Win32` 编译通过，0 warning / 0 error。
- 联调 Delphi 服务，依次验证 `/process/start`、`/process/end`、`/capture/face`、`/capture/fingerprint`、`/capture/iris`、`/ocr`、`/nfc`、`/preview/camera/start`、`/preview/camera/stop` 是否收到 DLL 请求。
- 同步抓拍验证 DLL 不再解析 base64、不再保存图片，只根据 Delphi `save_path` 返回成功。
- 异步接口验证 Delphi 返回 `accepted=true` 后 DLL 立即返回 1。

回退说明：
- 恢复 `exports.cpp` 中业务函数旧实现：`TerminalManager::BuildUrl` + `HttpClient` + `ResultParser` + `ImageSaver` + `PreviewManager`。
- 阶段 4 仅修改 `exports.cpp`，可从 `backup_before_delphi_proxy_20260522_151729` 对照恢复旧业务逻辑。

### 阶段 5：回调路由

已完成：
- [x] `EventDispatcher::ProcessCallback` 改为只识别 Delphi 回调路径：`/ocr`、`/iris`、`/nfc-card`、`/preview-ready`。
- [x] OCR 回调从精简 JSON 读取 `mrz` 和 `save_path`，映射到现有 OCR 成功事件。
- [x] 虹膜回调从精简 JSON 读取 `save_path`，映射到现有虹膜成功事件。
- [x] NFC/IC 卡回调从精简 JSON 读取 `card_text`，映射到现有 `ic_number` 字段。
- [x] Delphi 错误格式 `{ "error": true, "code": "...", "message": "..." }` 映射到对应失败事件。
- [x] 新增 `preview-ready` 路由，读取 `vlc_hwnd` 和可选 `delphi_host_hwnd`，保存到上下文并发送摄像头预览已就绪事件。
- [x] 旧终端原始结果解析/图片保存逻辑保留在 legacy 分支中，新 `ProcessCallback` 不再主动调用。

未完成：
- [x] 阶段 6：`preview-ready` 后执行 `IsWindow`、`SetParent`、`GetClientRect`、`MoveWindow`，并完善停止/Release 时归还 Delphi 宿主窗口。

验证方法：
- `Release|Win32` 编译通过，0 warning / 0 error。
- Delphi POST `/HZCYKJTHardWare/callback/ocr`，body 包含 `request_id`、`mrz`、`save_path`，第三方应收到 OCR 成功事件。
- Delphi POST `/HZCYKJTHardWare/callback/iris`，body 包含 `request_id`、`save_path`，第三方应收到虹膜成功事件。
- Delphi POST `/HZCYKJTHardWare/callback/nfc-card`，body 包含 `request_id`、`card_text`，第三方应收到 NFC 成功事件且 `event.ic_number=card_text`。
- Delphi POST `/HZCYKJTHardWare/callback/preview-ready`，body 包含 `request_id`、`vlc_hwnd`，DLL 应记录 HWND 并发送预览就绪事件。

回退说明：
- 恢复 `EventDispatcher::ProcessCallback` 旧路由逻辑，并恢复 OCR/虹膜/NFC 的 `ResultParser` + `ImageSaver` 处理。
- 当前旧处理代码仍保留在文件内的 legacy 分支，便于人工回退。

### 阶段 6：预览窗口迁移

已完成：
- [x] `preview-ready` 回调后校验 `vlc_hwnd` 和第三方 HWND：`IsWindow(vlc_hwnd)`、`IsWindow(thirdPartyHwnd)`。
- [x] 执行 `SetParent(vlc_hwnd, thirdPartyHwnd)`。
- [x] 执行 `GetClientRect(thirdPartyHwnd)` 获取第三方客户区。
- [x] 执行 `MoveWindow(vlc_hwnd, 0, 0, width, height, TRUE)`。
- [x] 日志记录 request_id、thirdPartyHwnd、vlc_hwnd、delphi_host_hwnd、SetParent 结果、MoveWindow 结果、GetLastError。
- [x] `StopCameraPreview` / `ReleaseSdk` 时优先 `SetParent(vlc_hwnd, delphi_host_hwnd)` 归还 Delphi 宿主窗口。
- [x] 如果 Delphi 未提供宿主 HWND 或宿主窗口无效，则退回 `SetParent(vlc_hwnd, NULL)`，避免继续挂在第三方窗口下。

未完成：
- [x] 阶段 7：整理最终协议文档、测试步骤和完整回退方案。

验证方法：
- `Release|Win32` 编译通过，0 warning / 0 error。
- 调用 `StartCameraPreview(hwndA)` 后，Delphi 回调 `preview-ready`，body 至少包含 `request_id`、`vlc_hwnd`，建议同时包含 `delphi_host_hwnd`。
- DLL 日志应出现 SetParent/MoveWindow 结果，第三方 hwndA 中应显示 Delphi/VLC 预览窗口。
- 调用 `StopCameraPreview()` 后，DLL 应优先将 VLC 窗口挂回 `delphi_host_hwnd`，然后通知 Delphi `/preview/camera/stop`。
- 调用 `ReleaseSdk()` 时，如仍有预览窗口，也应先尝试归还再释放资源。

回退说明：
- 移除 `EventDispatcher::ProcessPreviewReadyCallback` 中的 SetParent/GetClientRect/MoveWindow 逻辑，仅保留记录 `vlc_hwnd`。
- 恢复 `exports.cpp` 中 `TryRestoreCameraPreviewToDelphiHost` 为仅清理状态或旧 `PreviewManager::StopAll()`。

### 阶段 7：文档和 Delphi 示例同步

已完成：
- [x] `demo/Delphi7Demo` 改为 Delphi 后端服务示例，监听 `127.0.0.1:8080`。
- [x] 新增 `demo/Delphi7Demo/DelphiProxyServer.pas`，用 WinSock 实现 Delphi 7 可用的最小 HTTP 服务端。
- [x] 服务端示例已实现 `/ping`、`/process/start`、`/process/end`、`/capture/face`、`/capture/fingerprint`、`/capture/iris`、`/ocr`、`/nfc`、`/preview/camera/start`、`/preview/camera/stop`。
- [x] 服务端示例异步接口会按 DLL 传入的 `callback_url` 回调 OCR、虹膜、NFC、preview-ready。
- [x] 预览服务端示例初始化时保留 Delphi 自身预览宿主 Panel，用户调用预览接口后将示例 VLC Panel HWND 返回给 DLL；DLL 停止预览或 ReleaseSdk 时可归还到 Delphi 宿主窗口。
- [x] 新增 `demo/DelphiThirdPartyDemo`，用于模拟现版本第三方调用当前版本 DLL。
- [x] `demo/DelphiThirdPartyDemo` 已包含当前 `HZCYKJTHardWare.dll` 和 `HZCYKJTHardWare.json`，并补充 README 说明调用链路。
- [x] `demo/Delphi7Demo/README.md` 已说明 Delphi 服务端角色、端点、回调和测试顺序。

- [ ] `DelphiProxyServer.pas` 仍是协议演示 mock，真实项目需替换为终端 HTTP、终端回调解析、真实图片保存和 VLC 渲染。
- [ ] 指纹预览、虹膜预览、车牌预览仍未定义 Delphi HTTP 端点，当前 DLL 代理模式下返回 unsupported。

验证方法：
- 已使用 Delphi 7 `DCC32.EXE` 编译 `demo/Delphi7Demo/HZCYKJTDemo.dpr` 和 `demo/DelphiThirdPartyDemo/HZCYKJTDemo.dpr`；服务端示例存在 1 个平台 warning 和 1 个未使用变量 hint，无编译错误。
- 已使用 MSBuild 编译 DLL `Release|Win32`，0 warning / 0 error。
- 启动 `demo/Delphi7Demo`，确认日志显示 `服务已启动：http://127.0.0.1:8080`。
- 请求 `/ping`，预期返回 `{"status":"ok"}`。
- 启动 `demo/DelphiThirdPartyDemo`，按原第三方方式调用 DLL。
- 调用 `InitSdk`，预期 DLL 启动 callback_server 后访问 Delphi `/ping` 并返回 1。
- 调用 `StartCameraPreview(hwnd)`，预期 DLL POST `/preview/camera/start`，Delphi 回调 `preview-ready`，DLL 将 `vlc_hwnd` 挂到第三方 hwnd。
- 调用 `StopCameraPreview()` 或 `ReleaseSdk()`，预期 DLL 将 `vlc_hwnd` 优先归还给 Delphi `delphi_host_hwnd`。
- 调用同步抓拍接口，预期 DLL 只接收 Delphi 返回的 `save_path`，不解析 base64、不保存图片。
- 调用 OCR/NFC/虹膜异步接口，预期 Delphi 返回 `accepted=true` 后 DLL 立即返回 1，随后 Delphi 回调 DLL 并触发第三方回调。

回退说明：
- 使用 `backup_before_delphi_proxy_20260522_151729` 恢复原 `demo/Delphi7Demo`。
- 删除新增目录 `demo/DelphiThirdPartyDemo` 即可移除第三方调用示例副本。
- DLL 回退按阶段 1-6 的回退说明执行，或直接使用备份目录中的旧 DLL 产物。
