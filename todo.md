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

## UI 与日志中文化小范围修复记录（2026-05-25）

### 修改内容与原因

- [x] 修改前初始化 Git，并创建可回退基线提交 `ec88dff backup-before-ui-log-review`。
- [x] Delphi 日志文件改为按 UTF-8 字节追加写入，并增加写入锁，解决 ANSI/UTF-8 混写造成的中文乱码及并发写入风险。
- [x] Delphi 界面日志改为由主线程写入 `TMemo`，避免终端回调工作线程直接访问 UI 控件。
- [x] 修复 VLC 预览错误提示中的乱码文字，并将界面提示、状态提示和活动业务日志统一为中文描述。
- [x] Delphi 接收 DLL JSON 的 `save_dir` 时由 UTF-8 转为 ANSI/GBK，本地生成的 `save_path` 回传 JSON 时由 ANSI/GBK 转为 UTF-8，避免中文路径在 HTTP/JSON 边界乱码。
- [x] DLL 日志级别统一为 `调试/信息/警告/错误`，输出格式调整为 `[时间] [级别] [模块] 内容`，调试输出改用 Unicode 接口显示 UTF-8 中文。
- [x] DLL 活动日志明确区分第三方调用、DLL 下发 Delphi 程序、Delphi 程序回调 DLL、DLL 回调第三方等链路阶段，修复将 Delphi 回调误写为终端 HTTP 回调的描述。
- [x] 本轮仅修改界面、日志及中文编码边界；未改变 DLL 导出接口、终端调用协议、回调解析流程或预览窗口迁移逻辑。

### 修改文件列表

- Delphi 程序：`demo/Delphi7Demo/Logger.pas`、`MainUnit.pas`、`TerminalManager.pas`、`PreviewManager.pas`、`VlcPlayer.pas`、`DelphiProxyServer.pas`。
- DLL：`src/logger.cpp`、`delphi_proxy.cpp`、`callback_server.cpp`、`terminal_status_checker.cpp`、`event_dispatcher.cpp`、`exports.cpp`。
- 文档：`todo.md`。

### 中文编码处理方式

- Delphi 7 源文件继续使用 ANSI/GBK 保存，已抽查六个修改的 `.pas` 文件均可按 GBK 正确读取中文。
- Delphi 运行日志文件统一按 UTF-8 内容写入，日志行采用 `[时间] [级别] [模块] 中文内容` 格式。
- DLL C++ 源文件继续由项目 `/utf-8` 编译选项处理；DLL 文件日志保持 UTF-8。
- DLL 与 Delphi 程序之间的 HTTP/JSON 文本按 UTF-8 传输，仅在 Delphi 调用 ANSI 文件系统路径前后进行 UTF-8 与 ANSI/GBK 转换。
- `todo.md` 早期章节已有的历史乱码为本轮修改前状态，本轮未扩大范围重写历史记录，仅新增本节中文记录。

### 验证方法与结果

- `git diff --check`：通过，未发现新增空白错误；Git 仅提示后续检出时可能执行 LF/CRLF 转换。
- Delphi 7 编译验证：使用 `DCC32.EXE` 构建 `demo/Delphi7Demo/HZCYKJTHardWare.dpr`，生成到 `codex_build/delphi`，编译成功；存在原有的 `2` 个平台相关 warning 与 `3` 个未使用变量 hint，无 error。
- DLL 编译验证：使用 MSBuild 构建 `HZCYKJTHardWare.vcxproj` 的 `Release|Win32`，生成到 `codex_build/dll/Release/Win32`，结果为 `0` warning、`0` error。
- 静态日志检查：活动 DLL 日志中已不再出现原英文级别、英文链路提示或将 Delphi 程序回调误标为终端 HTTP 回调的文本。

### 回退方式与保留风险

- 修改前基线为 Git 提交 `ec88dff backup-before-ui-log-review`；提交本轮变更后，可使用 Git 对本轮提交执行反向提交回退。
- 如尚未提交本轮变更，可将工作区恢复到上述基线提交以撤销本轮 UI/日志修改。
- 已识别但本轮未处理的风险：摄像头预览启动路径中，DLL 保存 preview `request_id` 的时机可能晚于 Delphi 程序同步回调，存在预览就绪回调被判定为不匹配的竞态；该项涉及已验证的预览流程，需单独确认后处理。

## 切换终端卡顿诊断（2026-05-27）

### 已确认现象与证据

- [x] 第三方跨进程预览场景下，切换终端请求已进入 `TDelphiProxyServer.SwitchTerminalDirect`。
- [x] 现有日志显示 `20:43:24.286` 开始停止活动预览，`20:44:00.077` 才记录首路预览停止完成，首个 `StopPreview` 阶段阻塞约 `35.8s`。
- [x] 两路预览停止完成后才执行终端索引更新，因此当前卡顿优先定位于 `StopPreview` / VLC 释放 / 视频窗口销毁链路，而非 `TTerminalManager.SwitchTo`。
- [x] 已确认 DLL HTTP 请求由 `TDelphiHttpServerThread` 执行；第三方按钮调用 `SwitchTerminal` 仍为同步调用。

### 本轮诊断改动

- [x] 修改前建立快照目录 `backup_before_switch_terminal_diagnostics_20260527_005447`。
- [x] 在 `DelphiProxyServer.pas` 的切换、停止活动预览及恢复预览阶段增加逐资源耗时和线程 ID 日志。
- [x] 在 `PreviewManager.pas` 的 `StopPreview` 增加资源类型、`Vlc.Stop`、`Vlc.Free` 前后耗时日志。
- [x] 在 `VlcPlayer.pas` 的 `Stop` / `Destroy` 增加 VLC stop/release、`DestroyWindow`、`UnloadLibVlc` 前后耗时以及 HWND 所属进程/线程日志。
- [x] 本轮不恢复 `TLayoutThread`，不改变 DLL 接口、第三方调用方式、释放顺序或窗口架构。

### 待验证

- [x] 构建 `demo/Delphi7Demo/HZCYKJTHardWare.dpr`，确认诊断日志改动可由 Delphi 7 编译。
- [ ] 使用第三方 Demo 启动摄像头/指纹预览后切换终端，采集新增 `[Diag]` 日志。
- [ ] 根据日志确认阻塞具体位于 `libvlc_media_player_stop`、`libvlc_media_player_release`、`libvlc_release`、`DestroyWindow` 或析构卸载阶段。
- [ ] 定位明确后再制定最小修复方案；未确认卡点前不引入线程迁移、防重入状态或顶层覆盖窗口改造。

### 复现定位结果

- [x] 单摄像头切换：VLC stop/release 约 `94ms`，`DestroyWindow` 阻塞约 `19.1s`。
- [x] 单指纹切换：VLC stop/release 约 `110ms`，`DestroyWindow` 阻塞约 `21.9s`。
- [x] 双路预览切换：先停止的指纹在 `DestroyWindow` 阻塞约 `38.4s`；随后摄像头释放快速完成。
- [x] 另一次摄像头停止场景中，`DestroyWindow` 阻塞约 `62.5s`。
- [x] 根因确认：主程序创建的 VLC 子窗口被挂到第三方进程 Panel 下，跨进程子窗口销毁会同步阻塞；VLC release 并非主要耗时点。

### 同进程覆盖容器修复

- [x] 建立修复前快照目录 `backup_before_overlay_container_fix_20260527_012538`。
- [x] `VlcPlayer.pas` 对跨进程目标 HWND 改为创建主程序自有的无边框覆盖容器，VLC 视频子窗口只挂在本进程容器中。
- [x] 第三方传入的 HWND 保持为定位锚点，覆盖容器根据其屏幕客户区保持原有 cover 铺满效果。
- [x] 使用主程序 UI 定时器跟随外部目标位置和可见状态；不恢复后台布局线程，不对第三方 Panel 进行 `SetParent`、`MoveWindow` 或 `DestroyWindow`。
- [x] `PreviewManager.pas` 将 VLC `Play` / `Stop` / `Free` 统一通过 Delphi 主线程执行，保证本进程窗口的创建、移动和销毁线程一致。
- [x] 使用 Delphi 7 编译主程序通过；仅存在原有未使用变量 hint，无 error。
- [ ] 验证跨进程启动预览、单路切换、双路切换、停止预览和第三方窗体移动/最小化场景。

### 覆盖窗口可见性策略调整

- [x] 运行验证确认预览启动和终端切换不再卡顿。
- [x] 发现覆盖容器原逻辑在第三方窗口失去前台时主动隐藏预览，导致被其他程序遮挡后预览不可见。
- [x] 建立修复前快照目录 `backup_before_overlay_visibility_fix_20260527_092110`。
- [x] `VlcPlayer.pas` 移除“目标窗体必须为前台”的显示条件；仅在锚点失效、不可见或目标顶层窗体最小化时隐藏覆盖预览。
- [x] 覆盖容器不再使用 `HWND_TOPMOST`；改为排列在第三方顶层窗体之上且位于其他更高层窗口之下，使其随第三方窗口自然被遮挡。
- [ ] 复测其他程序部分/完全遮挡第三方窗体后移开遮挡，确认预览仍存在且不会覆盖其他应用。
- [ ] 复测第三方窗体最小化/恢复、移动，以及切换终端/停止预览仍不卡顿。

### 覆盖窗口层级更新优化

- [x] 日志确认点击返回第三方窗口时未产生新的 `PreviewUi Play`、`VLC预览已启动` 或 `Vlc.Stop`，视觉刷新并非重启预览。
- [x] 定位视觉刷新来源为覆盖容器定时执行 `SetWindowPos(..., SWP_SHOWWINDOW)` 进行层级修正与显示更新。
- [x] 建立修复前快照目录 `backup_before_overlay_zorder_optimization_20260527_095228`。
- [x] `VlcPlayer.pas` 缓存覆盖窗口显示状态与上次屏幕矩形，层级、位置和可见状态均未改变时不再重复调用 `SetWindowPos`。
- [x] 仅在首次显示、隐藏后恢复、Panel 移动/缩放或 Z-order 确实改变时更新覆盖窗口；仅尺寸变化时不重新调整层级。
- [ ] 复测在其他窗口之间反复切换并点击回第三方窗体时的视觉闪变程度，确认不存在持续或重复刷新。

### 本地与第三方并发预览会话拆分

- [x] 确认终端支持并发流，问题根因是 Delphi 本地按钮与 DLL 外部请求共用每类资源的单一 `TVlcPlayer` 和活动状态。
- [x] 建立修改前快照目录 `backup_before_independent_preview_sessions_20260527_100557`。
- [x] `PreviewManager.pas` 新增 `local` / `external` 会话维度；摄像头、指纹、虹膜分别维护本地与第三方独立播放器和目标窗口状态。
- [x] `DelphiProxyServer.pas` 将活动预览状态拆为本地集合与第三方集合；主程序按钮仅操作本地会话，DLL HTTP 预览接口仅操作第三方会话。
- [x] 终端切换流程同时停止并恢复切换前处于活动状态的本地和第三方会话，第三方覆盖容器方案及 DLL 对外接口保持不变。
- [x] 日志中的预览启动、停止和终端切换阶段增加 `camera/local`、`camera/external` 等会话标识。
- [x] 使用 Delphi 7 `DCC32.EXE` 编译 `demo/Delphi7Demo/HZCYKJTHardWare.dpr` 成功；仅有原有未使用变量 hint，无 error。
- [ ] 验证主程序和第三方同时预览同一摄像头/指纹时，两路画面均持续显示且互不影响停止和重启。
- [ ] 验证本地及第三方会话同时运行时切换终端，两类会话均可在新终端恢复且不产生卡顿。

### 最终收尾与日志清理

- [x] 用户运行确认同进程覆盖容器与独立会话方案当前无明显问题，确定作为本版本方案。
- [x] 建立收尾前快照目录 `backup_before_final_log_cleanup_20260527_103449`。
- [x] 移除 `DelphiProxyServer.pas`、`PreviewManager.pas`、`VlcPlayer.pas` 中为定位卡顿临时增加的 `[Diag]` 耗时与线程日志。
- [x] 保留覆盖容器启用的业务日志并改为中文；不恢复 `TLayoutThread`，不改变覆盖窗口或独立会话结构。
- [x] 检查 DLL 源码运行日志，并将遗留的英文调试信息改为中文；字段名、API 名和配置值保留技术原文。
- [x] 更新 Delphi 服务端与第三方示例 README，说明覆盖容器、独立会话和第三方启动按钮后台调用方式。
- [x] 收尾后重新编译 Delphi 主程序、第三方示例和 DLL；Delphi 主程序仅有原有 hint，DLL 为 `0 warning / 0 error`。
- [x] 将最新 Delphi 服务 EXE 和 DLL 同步到第三方示例部署目录，并纳入最终提交。
