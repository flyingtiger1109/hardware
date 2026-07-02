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

## 长期稳定性优化记录（2026-06-01）

已完成事项：
- [x] 已先提交当前稳定版本：`0520e76 fix: stabilize terminal switching and callback handling`，便于回退。
- [x] Delphi7Demo 的 DLL HTTP 接入改为固定 8 个 HTTP worker + 有界队列，accept 线程不再同步执行业务。
- [x] 人脸抓拍、指纹抓拍、OCR、IC/NFC 读取拆分为固定业务 worker，每类 1 个执行中 + 1 个等待中。
- [x] 业务队列满时不再返回 busy，而是用新请求替换等待中的旧请求；旧请求仅内部失败返回并写中文日志。
- [x] 业务请求带终端切换 generation，切换后旧终端排队请求会被丢弃，避免旧终端数据回调。
- [x] DLL `SwitchTerminal` 不再同步等待虹膜预览 URL 获取和渲染恢复，改为固定 1 个后台恢复 worker。
- [x] DLL 后台虹膜预览恢复采用“最新请求替换旧等待请求”，避免频繁切换造成线程膨胀。
- [x] Delphi7Demo 编译通过。
- [x] DLL Release Win32 编译通过，0 warning / 0 error。
- [x] DelphiThirdPartyDemo 编译通过。
- [x] 已将最新 `Release/HZCYKJTHardWare.dll` 同步复制到 `demo/DelphiThirdPartyDemo/HZCYKJTHardWare.dll`，哈希一致。

修改文件：
- `demo/Delphi7Demo/DelphiProxyServer.pas`
- `src/exports.cpp`
- `framework.h`
- `demo/Delphi7Demo/HZCYKJTHardWare.exe`
- `demo/DelphiThirdPartyDemo/HZCYKJTHardWare.exe`
- `demo/DelphiThirdPartyDemo/HZCYKJTHardWare.dll`
- `todo.md`

待验证事项：
- [ ] 第三方程序每秒 5-8 次人脸/指纹抓拍请求连续运行 24 小时。
- [ ] 高频请求中连续点击切换终端，确认切换指令立即返回，视频跟随新终端恢复。
- [ ] 高频 OCR、IC/NFC 请求下确认旧等待请求被替换，不出现请求堆积。
- [ ] 第三方程序反复重启、重新注册回调，确认不会收到已成功清理的历史数据。
- [ ] 检查任务管理器或 Process Explorer 中线程数、句柄数、内存、CPU 是否长期稳定。
- [ ] 切换期间终端旧回调是否全部丢弃，不转发到第三方。

已知风险：
- HTTP 接入队列仍保留 64 上限；极端连接风暴下仍会快速返回 `server_busy`，这是保护进程稳定的最后防线。
- 同步抓拍接口如果被新等待请求替换，旧请求会失败返回，第三方外部仍保持 1/0 兼容。
- DLL 虹膜预览后台恢复会最多重试约 10 秒；如果 Delphi 切换长期未完成或终端不可达，需要查看中文日志定位。

下一步建议：
- 在真实终端环境做 2 小时快速压测，观察是否还有 12029。
- 再做 24 小时稳定性压测，并记录线程数、句柄数、内存曲线。
- 如仍有切换视频迟滞，继续把 Delphi 侧外部预览 start/stop 也改成固定预览 worker + 最新请求替换策略。

### 同步等待和预览回调最小化修复（2026-06-01）

已完成事项：
- [x] Delphi 业务队列同步等待统一降为 `4500ms`，低于 DLL 侧 5 秒 HTTP 超时。
- [x] 被新请求替换的等待任务立即设置 `request_replaced` 并唤醒等待中的 HTTP handler。
- [x] 超时任务通过 `StateLock` 标记为不再等待，由 worker 完成后释放，避免竞态泄漏。
- [x] 第三方外部预览成功回调后，Delphi 主窗口通过消息切回 UI 线程并最小化到任务栏。
- [x] Delphi7Demo 编译通过。

修改文件：
- `demo/Delphi7Demo/DelphiProxyServer.pas`
- `demo/Delphi7Demo/MainUnit.pas`
- `todo.md`

待验证事项：
- [ ] 第三方 UI 线程直接调用同步抓拍时，最长卡顿应压到约 4.5 秒以内。
- [ ] 高频替换等待请求时，旧请求应快速返回失败，不再等满 20 秒。
- [ ] 第三方启动摄像头/指纹/虹膜外部预览并收到成功回调后，Delphi 程序应自动最小化到任务栏。

## 长期运行稳定性修复记录（2026-06-01）

### 已完成事项
- [x] 已建立修改前备份：`.codex_backups/pre_terminal_switch_fix_20260601_104557`，备注当前版本仍存在终端切换问题。
- [x] 第 1 项：终端切换改为高优先级同步控制，切换期间阻止普通请求继续进入旧终端，旧 generation 的预览结果会被丢弃。
- [x] 第 2 项：回调成功后立即清理 Delphi7Demo 请求上下文，DLL 层记录已完成 request_id，第三方重新注册回调时清空待推送队列。
- [x] 第 3 项：网络请求增加连接/发送/接收超时，修复 WinInet 响应读取方式，DLL 回调队列增加上限，避免请求无限堆积。
- [x] 第 4 项：视频预览停止流程加锁，切换终端时先停止旧预览并清理旧 HWND，降低并发 Stop/Start 导致卡顿或句柄残留的风险。
- [x] 第 5 项：Delphi7Demo 日志改为 ANSI/GBK 兼容写入；DLL 日志跨天自动关闭旧文件并切换到新日期文件。
- [x] Delphi7Demo 已通过 Delphi 7 `DCC32.EXE HZCYKJTHardWare.dpr` 编译。
- [x] DLL 已通过 Release Win32 MSBuild 编译，0 警告 0 错误。
- [x] 第三方 Delphi 示例 `HZCYKJTDemo.dpr` 已通过 Delphi 7 编译，示例目录 DLL 已同步为最新 Release DLL。

### 修改文件
- `demo/Delphi7Demo/DelphiProxyServer.pas`
- `demo/Delphi7Demo/TerminalClient.pas`
- `demo/Delphi7Demo/PreviewManager.pas`
- `demo/Delphi7Demo/Logger.pas`
- `src/exports.cpp`
- `src/delphi_proxy.cpp`
- `src/event_dispatcher.cpp`
- `src/hzsjkjt_context.h`
- `src/hzsjkjt_context.cpp`
- `src/request_session_manager.h`
- `src/request_session_manager.cpp`
- `src/logger.h`
- `src/logger.cpp`
- 编译产物：`demo/Delphi7Demo/HZCYKJTHardWare.exe`、`Release/HZCYKJTHardWare.dll`
- 第三方示例产物：`demo/DelphiThirdPartyDemo/HZCYKJTHardWare.dll`、`demo/DelphiThirdPartyDemo/HZCYKJTDemo.exe`

### 待验证事项
- [ ] 第三方程序每秒 5-8 次请求连续运行 24 小时，观察 12029、线程数、句柄数、内存是否持续增长。
- [ ] 高频抓拍期间连续切换终端，确认切换命令优先响应，旧终端回调不会再推送给第三方。
- [ ] 第三方程序反复退出、重启并重新注册回调，确认不会收到已处理过的历史数据。
- [ ] 切换终端期间视频预览停止、重建、SetParent/MoveWindow 均有中文日志且无窗口句柄残留。
- [ ] 模拟网络断开、终端超时、Delphi7Demo 短暂无响应，确认 DLL 外部接口仍按 1/0 兼容返回，内部中文日志记录详细错误。
- [ ] 跨 0 点运行，确认 Delphi7Demo 与 DLL 均自动生成新日期日志文件。

### 已知风险
- DLL 外部接口按要求仍保持 1/0 兼容，详细错误码只写入中文日志；第三方如果需要读取详细错误码，需要后续新增不破坏兼容的查询接口。
- 当前只完成编译验证，真实 24 小时压力测试、真实终端切换、视频预览和第三方反复重启场景仍需现场联调。
- `demo/Delphi7Demo/HZCYKJTDemo.exe` 在本次开始前已处于删除状态，未在本次修复中恢复。

### 下一步建议
- 先做 30-60 分钟短压测，确认终端切换、预览、回调去重日志正常，再扩大到 24 小时压力测试。
- 压测期间每 5 分钟记录一次进程内存、线程数、句柄数、日志文件大小和最近错误码。
- 如现场仍出现 12029，优先对照日志中的请求阶段、终端切换 generation、队列满丢弃日志和 WinInet 错误码定位。

## 第三方切换终端卡顿专项修复（2026-06-01）

### 问题定位
- [x] DLL 日志显示第三方 `SwitchTerminal` 等待 `/terminal/switch` 超时，错误码为 12002。
- [x] Delphi 日志显示卡点不在终端 HTTP，而在停止 `resource=fingerprint, session=external` 的 VLC 预览，单次阻塞约 30-31 秒。
- [x] 根因是切换线程同步等待 `libvlc_media_player_stop/release`，并通过 `TThread.Synchronize` 占用主线程，导致 DLL HTTP 响应和第三方调用同时卡住。

### 已完成修复
- [x] `/terminal/switch` 改为快速受理：Delphi 设置切换状态后立即返回 `status=ok, accepted=true`，后台线程执行停止旧预览、切换终端、恢复新预览。
- [x] `TVlcPlayer` 新增 `DetachVideoWindow`，先解除 VLC 窗口绑定、隐藏并销毁视频子窗口，第三方窗口不再等待 VLC 完整释放。
- [x] `TPreviewManager.StopPreview` 改为先从活动状态摘除旧 `TVlcPlayer`，再把 VLC 对象投递到后台清理队列。
- [x] VLC 后台清理使用固定 2 个 worker 线程，队列上限 32，避免频繁切换时无限创建线程或无限堆积句柄。
- [x] 队列满时改为同步释放并写日志，优先保证资源不无限增长。
- [x] DLL 取消 `/terminal/switch` 30 秒特殊超时，恢复普通短超时，避免 Delphi 异常无响应时长时间卡住第三方。

### 修改文件
- `demo/Delphi7Demo/DelphiProxyServer.pas`
- `demo/Delphi7Demo/PreviewManager.pas`
- `demo/Delphi7Demo/VlcPlayer.pas`
- `src/delphi_proxy.cpp`
- 编译产物：`demo/Delphi7Demo/HZCYKJTHardWare.exe`、`Release/HZCYKJTHardWare.dll`、`demo/DelphiThirdPartyDemo/HZCYKJTHardWare.dll`、`demo/DelphiThirdPartyDemo/HZCYKJTDemo.exe`

### 验证结果
- [x] Delphi7Demo 编译通过。
- [x] DLL Release Win32 编译通过，0 警告 0 错误。
- [x] 第三方 Delphi 示例编译通过。

### 待现场验证
- [ ] 第三方连续调用终端 1/2 切换，确认 DLL 调用快速返回，不再出现 30 秒无响应。
- [ ] 切换时观察第三方摄像头/指纹预览窗口，确认旧画面立即消失，新终端预览尽快重建。
- [ ] 高频切换 10-30 分钟，观察线程数、句柄数、内存是否稳定，确认后台清理队列不会持续堆积。
- [ ] 如果日志出现“VLC后台释放队列已满”，说明现场切换频率已超过释放能力，需要降低切换频率或进一步替换 VLC 停止策略。

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

### Delphi 服务不可达时立即重启

- [x] 用户确认取消“已存在 Delphi EXE 但 `/ping` 不可用时等待 8 秒”的恢复策略。
- [x] 建立修改前快照目录 `backup_before_immediate_delphi_restart_20260527_111052`。
- [x] `src/exports.cpp` 在发现同路径 Delphi EXE 已运行但通信服务不可用时立即终止旧进程并重启。
- [x] 保留 `delphi_server.start_wait_ms`，用于启动新 Delphi 进程后等待 `/ping` 就绪；该配置不再表示旧进程观察等待。
- [x] 更新 Delphi 服务端及第三方示例 README 中的初始化自恢复说明。
- [x] 重新构建 Win32 DLL（`Release|Win32`，`0 warning / 0 error`），并将最新产物同步到第三方部署目录。

### OCR 回调与人脸队列稳定性修复

- [x] 根据 OCR 日志确认终端已返回 `ocr_document`，但 Delphi 侧没有 `[OCR] 已完成` 和 `/callback/ocr`，问题位于服务端回调解析/转发链路。
- [x] `CallbackParser.pas` 移除带有托管字符串/动态数组记录上的 `FillChar(Result, SizeOf(Result), 0)`，避免破坏 Delphi 字符串和动态数组引用计数。
- [x] `CallbackParser.pas` 修正 OCR 证据图片解析参数，传入完整动态数组，不再传第一个元素地址，避免大包证据图片解析时内存越界。
- [x] `CallbackParser.pas` 增加 `mrz`、`MRZ`、`mrz_code`、`MRZCode` 等直接字段兜底，再回退到 `MRZ1/MRZ2/MRZ3` 拼接。
- [x] `CallbackParser.pas` 增加扫描全部同名字段并取第一个非空值的 MRZ 解析逻辑，补充 `mrzCode`、`MRZ_CODE` 等字段名，避免第一个空字段挡住真实 MRZ。
- [x] `DelphiProxyServer.pas` 为终端回调处理增加外层异常保护；OCR/NFC/虹膜解析异常时写中文日志，并尽量向 DLL 回调明确 `callback_exception`。
- [x] `DelphiProxyServer.pas` 增加 DLL 回调成功/失败日志，便于确认是否已送达 DLL 回调服务。
- [x] `DelphiProxyServer.pas` 修复业务队列任务完成竞态：工作线程不再在持有任务锁时触发 `DoneEvent`，避免 HTTP 线程唤醒后提前释放任务对象导致单个业务 worker 异常退出。
- [x] 使用 Delphi 7 `DCC32.EXE` 重新编译 `demo/Delphi7Demo/HZCYKJTHardWare.dpr` 成功；仅有原有 hint/platform warning，无 error。
- [x] 将新编译的 `HZCYKJTHardWare.exe` 同步到 `demo/DelphiThirdPartyDemo`，便于第三方示例直接验证。
- [ ] 使用带 OCR 的第三方调用复测，确认日志出现 `[OCR] 已完成`、`DLL回调成功`，第三方收到 MRZ。
- [ ] 使用无 OCR 高频抓拍复测，确认人脸和指纹队列均持续响应，人脸不再在一次请求后长期只返回 timeout。

## C# Proxy / DLL 长期稳定性与授权适配优化（2026-06-02）

### 已完成事项

- [x] 修改前确认可回退备份：git 已存在 `0ca47fe`，并额外建立 `.codex_backups/review_opt_20260602_090210` 文件快照。
- [x] C# Proxy 日志改为后台有界队列写入，业务线程和 UI 线程不再逐条同步写文件。
- [x] C# Proxy 抓拍/预览/授权等终端 HTTP 请求增加超时参数，并释放 `HttpResponseMessage`、`HttpContent` 等资源。
- [x] C# Proxy DLL 请求入口增加连接并发上限，终端回调入口改为快速返回 `202 accepted` 后后台处理。
- [x] C# Proxy 请求队列取消二次 `Task.Run().Wait()`，避免 handler 超时后旧 ThreadPool 任务继续堆积。
- [x] C# Proxy UI 日志刷新改为批量裁剪，预览和最小化操作改为异步 UI 投递，降低 UI 主线程被后台线程同步等待的风险。
- [x] DLL 默认代理端口和根配置从 `8080` 改为 `18080`，避免缺配置或根配置场景继续走旧 Delphi 模拟授权链路。
- [x] DLL 事件分发线程加入异步会话超时扫描，OCR/NFC/虹膜/授权超时会主动清理并回调失败事件。
- [x] DLL 异步请求在终端切换、busy、HTTP 失败等提前返回路径清理会话，避免长期残留后误触发超时回调。
- [x] DLL 授权回调 JSON 增加字符串转义，`auth_result` 按数字输出，第三方回调增加 SEH 保护。
- [x] 修复 `StartProcess` 创建无法匹配的假会话问题，并补充流程级虹膜回调兜底处理。
- [x] C# Proxy `Release|x86` 编译通过：0 warning / 0 error。
- [x] DLL `Release|Win32` 编译通过：0 warning / 0 error。
- [x] 根据 `F:\HZCYKJTHardWare-20260602` 现场日志定位：高频抓拍叠加频繁终端切换时，Exe 日志停止在 VLC 新预览启动过程中，DLL 随后出现 `error=12029`，说明 C# Proxy 已无响应或退出。
- [x] C# Proxy 预览启停增加串行锁，终端切换时等待 VLC 旧播放器真实释放后再切换和重启预览，避免旧播放器释放与新播放器启动交错。
- [x] 外部预览后台启动增加终端世代检查，切换中或过期请求不再继续用旧终端地址拉流。
- [x] C# Proxy UI 日志改为 250ms 批量刷新，并限制待刷 UI 日志队列，降低高频抓拍日志压垮 UI 消息队列的风险。
- [x] 本轮 C# Proxy `Release|x86` 编译通过：0 warning / 0 error。
- [x] 根据现场闪退模块 `libsftp_plugin.dll` 定位到 VLC SFTP 访问插件风险；该插件与 RTSP 预览无关，已从 C# Proxy 输出包排除，并在运行时检测到现场残留插件时尝试改名禁用。
- [x] 根据 `20:55:50` 现场日志定位：切回 Terminal 1 时日志停在 `VLC loaded from C:\BJ\exe\vlc`，未输出 `VLC播放成功/切换完成`，说明切换流程被 VLC native 播放启动阻塞，导致 UI 无响应且 `terminal_switching` 长时间不清。
- [x] VLC 预览启动/停止改为后台 STA 线程执行，并增加 2500ms 启动超时、1500ms 停止超时；VLC 卡住时不再阻塞 UI 线程，也不再阻塞终端切换完成。
- [x] VLC 播放启动增加分步骤中文日志：创建实例、创建媒体、创建播放器、创建视频窗口、开始播放，便于后续定位具体 native 卡点。

### 修改文件

- C# Proxy：`Core/WorkerQueue.cs`、`Infrastructure/Logger.cs`、`MainForm.cs`、`Preview/PreviewManager.cs`、`Preview/VlcPreviewController.cs`、`Preview/VlcPreviewPlayer.cs`、`Server/DllCallbackSender.cs`、`Server/DllCommandHandler.cs`、`Server/ProxyServer.cs`、`Server/TerminalCallbackHandler.cs`、`Terminal/TerminalClient.cs`、`HZCYKJTHardWare.Proxy.csproj`。
- DLL 工程：`HZCYKJTHardWare.json`、`src/config_manager.cpp`、`src/config_manager.h`、`src/event_dispatcher.cpp`、`src/event_dispatcher.h`、`src/exports.cpp`、`src/request_session_manager.cpp`。
- 本次未主动修改第三方示例程序源码；工作区中已有 Delphi 示例和第三方示例改动属于既有未提交变更。

### 待现场验证

- [ ] 第三方 Demo 正常 `InitSdk`，确认 DLL 实际连接 `http://127.0.0.1:18080` 的 C# Proxy。
- [ ] 人脸/指纹抓拍按每秒 5 到 8 次连续运行 24 小时，记录 P50/P95/P99 响应时间。
- [ ] 长时间视频预览叠加高频抓拍，确认 UI 可移动、可点击、日志继续刷新且不假死。
- [ ] 复测“高频人脸/指纹抓拍 + 每 0.5 到 2 秒反复切换终端 + 外部摄像头/指纹预览”，确认预览不出现旧画面延迟、切换不卡顿、Exe 不再闪退。
- [ ] 复测中重点观察 Exe 日志是否继续输出、DLL 是否不再出现连续 `error=12029` 连接 C# Proxy 失败。
- [ ] 复测中重点观察终端切换是否还会卡在 `VLC loaded from ...`；若仍有 VLC 卡住，应看到新的 `VLC启动步骤` 最后一条日志，并且 UI 应保持可操作、切换状态应在超时后清除。
- [ ] 现场部署后确认 `C:\BJ\exe\vlc\plugins\access\libsftp_plugin.dll` 已不存在，或已被改名为 `libsftp_plugin.dll.disabled`；复测 Windows 事件查看器不再出现该模块导致的闪退。
- [ ] 授权请求真实链路验证：第三方调用 DLL，DLL 转发 C# Proxy，C# Proxy 调终端真实接口，授权结果回调第三方。
- [ ] 第三方反复启动、退出、重新注册回调，确认旧会话被取消，旧数据不再回调给新注册方。
- [ ] 断网、终端断开、终端超时、C# Proxy 停止等异常场景，确认 DLL 有中文失败日志和超时回调。
- [ ] 用任务管理器或 Process Explorer 连续观察内存、线程数、句柄数、GDI 对象数、日志文件句柄是否持续增长。

### 已知风险和下一步建议

- [ ] DLL 导出 HTTP 请求仍由全局 `BusyGuard` 串行化；当前可防止无限堆积，但并发第三方请求会快速返回 busy，后续可按资源类型拆分独立限流。
- [ ] C# Proxy 抓拍同步接口仍需要等待终端 HTTP 返回；若终端自身响应变慢，建议结合现场数据继续调低超时并增加慢请求分桶统计。
- [ ] 日志系统已从业务线程移走，但高频写盘仍建议观察磁盘 IO；必要时可改为批量 flush。
- [ ] 如果现场仍有 VLC native 崩溃，需要补充 Windows 事件查看器中 `Faulting module`，确认是否为 `libvlc.dll/libvlccore.dll` 或显卡/窗口句柄相关模块。
- [ ] 当前编译验证已完成，尚未做真实终端 24 小时压测，性能结论需以现场压测数据确认。

## 授权超时调整（2026-06-17）

### 已完成事项

- [x] 授权异步等待超时从复用 `ocr_ms` 改为独立配置 `authorize_ms`。
- [x] 默认授权超时调整为 `60000ms`，旧配置缺少 `authorize_ms` 时也按 60 秒处理。
- [x] 根配置 `HZCYKJTHardWare.json` 新增 `"authorize_ms": 60000`。
- [x] C# 第三方 Demo 项目补充复制 `Release\HZCYKJTHardWare.dll`，便于 Demo 输出目录直接运行最新 DLL。

### 修改文件

- `HZCYKJTHardWare.json`
- `src/config_manager.h`
- `src/config_manager.cpp`
- `src/hzsjkjt_context.h`
- `src/hzsjkjt_context.cpp`
- `src/exports.cpp`
- `demo/CSharpThirdPartyDemo/HZCYKJTHardWare.CSharpDemo/HZCYKJTHardWare.CSharpDemo.csproj`

### 兼容性说明

- DLL 导出函数、回调结构、调用约定、错误码均未改变。
- OCR/NFC 仍使用原 `ocr_ms=10000`，不受本次授权超时调整影响。
- 部署时需要确保 DLL 同目录的 `HZCYKJTHardWare.json` 包含或默认支持 `authorize_ms=60000`。

### 验证状态

- [x] DLL `Release|Win32` 编译通过：0 warning，0 error。
- [x] C# 第三方 Demo `Release|x86` 编译通过：0 warning，0 error。
- [x] Demo 输出目录已包含最新 `HZCYKJTHardWare.dll` 和带 `authorize_ms=60000` 的 `HZCYKJTHardWare.json`。
- [ ] 真实终端授权“同意/不同意/不操作等待 60 秒超时”现场验证。

## 虹膜异步采集协议适配（2026-06-29）

### 已完成事项

- [x] DLL 现有 `/capture/iris` 请求在 C# Proxy 中改为实际转发终端 `POST /resources/iris/request`。
- [x] 逐笔异步虹膜请求全链路保留 DLL 传入的 `request_id`，避免回调会话无法匹配。
- [x] `POST /process/start` 的 `callbacks` 增加 `iris_image` 完整回调地址，支持流程内自动虹膜推送。
- [x] 逐笔请求和流程登记的虹膜回调地址使用协议路径 `/iris-image`，不影响 OCR/NFC 公共回调路径。
- [x] `/iris-image` 回调支持双眼、仅左眼、仅右眼及兼容 `data.image_base64` 报文。
- [x] `/iris-image` 合法推送返回 HTTP 202、`status=accepted` 和原 `request_id`；非法虹膜回调返回 HTTP 400。
- [x] 按 `image_mime_type` 分别保存 `iris_left`、`iris_right` 图像，并向 DLL 返回请求保存目录。
- [x] 终端采集失败、无有效图像或文件保存失败时，使用现有 DLL 虹膜失败事件链路返回错误。
- [x] 虹膜请求在终端切换后过期时完成等待任务，避免无结果等待完整超时。
- [x] 最终虹膜回调处理后清理该笔保存路径和回调地址映射。

### 修改文件

- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Core/QueueManager.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Parsing/CallbackParser.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/DllCommandHandler.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/ProxyServer.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/TerminalCallbackHandler.cs`

### 兼容性说明

- DLL 导出函数、`__stdcall` 调用约定、参数、事件编号和第三方回调 JSON 既有字段未改变。
- `HZCYKJTHardWare_CaptureIrisImage` 继续保持异步受理语义。
- 终端同步端点 `/resources/iris/sync-request` 本次未新增 DLL 导出入口，避免扩大第三方接口范围。

### 验证状态

- [x] C# Proxy `Compile|Release|x86` 通过。
- [x] 双眼虹膜回调解析验证通过。
- [x] 单右眼虹膜回调解析验证通过。
- [x] 兼容 `data.image_base64` 回调解析验证通过。
- [x] `error_code` 失败回调解析验证通过。
- [ ] 真实终端逐笔异步请求、HTTP 202 受理及 `/iris-image` 回调联调。
- [ ] 流程开始登记 `callbacks.iris_image` 后的自动采集回调联调。
- [ ] 图片落盘失败、终端 HTTP 400/503、超时和终端切换场景验证。

### 回退方式

- 恢复上述 5 个 C# Proxy 文件至本次修改前版本，并删除本节进度记录。

## C# Proxy 最新等待任务队列模型（2026-06-29）

### 当前阶段

- [x] 完成 `Single Runner + Latest Pending` 队列模型实现。
- [x] 虹膜和授权迁移到独立业务队列。
- [ ] 真实终端并发请求和拥塞场景联调。

### 本次修改内容

1. 队列容量改为包含正在执行任务的总在途容量；业务队列容量为 2，即 1 个执行、1 个等待。
2. 新请求到达且已有等待任务时，只替换等待任务，不中断正在执行或即将执行的首个任务。
3. 被替换任务立即完成为内部错误 `queue_replaced`，避免继续等待到超时。
4. 队列停止时，所有尚未执行的任务立即完成为 `service_stopping`。
5. 虹膜从 `MiscQueue` 迁移到独立 `IrisQueue`；授权从直接执行迁移到独立 `AuthorizeQueue`。
6. 终端切换导致任务 generation 过期时，统一完成为 `terminal_switching`。

### 涉及文件

- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Core/WorkerQueue.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Core/QueueManager.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/DllCommandHandler.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/ProxyServer.cs`

### 兼容性说明

- DLL 导出函数、调用约定、参数、第三方回调 JSON 和终端 HTTP 协议未改变。
- 正常请求处理逻辑未改变；仅拥塞时由最新请求替换唯一等待任务。
- `queue_replaced`、`service_stopping` 为 DLL 与 Proxy 之间的内部错误，不新增第三方 DLL 错误码。
- C# Proxy 继续保持 `net46`、`x86`，未新增依赖。

### 风险与注意事项

1. 正在执行的旧请求仍正常返回；队列模型只保证等待执行的任务始终为最新请求。
2. 虹膜、授权改为独立队列后不再互相占用 `MiscQueue`。
3. 真实终端响应接近内部等待超时时，仍需联调确认超时边界。

### 验证状态

- [x] C# Proxy `Compile|Release|x86`：通过。
- [x] 队列顺序：A 执行时 B 等待，C 替换 B，D 替换 C，最终仅执行 A、D。
- [x] 被替换任务：B、C 均立即返回 `queue_replaced`。
- [x] 队列状态：验证执行中 `Count=2`、`PendingCount=1`。
- [ ] 真实终端虹膜、OCR、NFC、授权并发联调。
- [ ] Proxy 停止时正在执行任务的 3～5 秒释放验证。

### 下一步计划

- [ ] 使用终端或 Mock 连续提交同类请求，核对 request_id、受理结果及回调归属。
- [ ] 单独实施 Proxy 生命周期和 `ReleaseSdk` 释放优化。

### 回退方式

- 恢复上述 4 个 C# Proxy 文件中本节相关修改，并删除本节进度记录；不涉及 DLL 文件回退。

## C# Proxy 统一请求注册表与状态机（阶段 2，2026-06-29）

### 当前阶段

- [x] 完成阶段 2：统一请求上下文、状态迁移和回调去重。
- [x] OCR/NFC 全链路透传 DLL 原始 `request_id`。
- [ ] 真实终端异步受理与回调时序联调。

### 本次修改内容

1. 新增以 `(request_id, resource_type)` 为键的 `RequestRegistry`，统一保存路径、DLL 回调地址、terminal generation、TTL 和请求状态。
2. 请求状态统一为 `Created → Queued → Submitting → Accepted → CallbackReceived → Completed`，并支持 `Failed/Cancelled/TimedOut`。
3. OCR、NFC 队列任务携带 DLL 原始 `request_id`，Proxy worker 不再重新生成 GUID。
4. 流程开始保留 DLL 传入的流程 `request_id`，并为 OCR、NFC、虹膜分别登记上下文，允许同一个流程 ID 对应三种资源回调。
5. 虹膜、授权、Proxy UI 直调和流程内自动回调统一接入注册表。
6. 回调通过原子状态迁移认领；重复、迟到、终端切换后或未登记回调不再重复转发 DLL。
7. 终端切换按 generation 取消旧请求；服务停止和流程结束统一取消活动请求。
8. 回调早于受理响应时允许先完成，后到的受理响应不会覆盖完成状态；取消后的迟到受理不会重新激活请求。

### 涉及文件

- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Core/RequestRegistry.cs`：新增统一请求上下文、状态机、TTL 和完成记录。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/DllCommandHandler.cs`：透传 request_id、登记请求、处理队列失败和流程资源上下文。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/ProxyServer.cs`：worker 提交、UI 直调、终端切换和停止接入注册表。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/TerminalCallbackHandler.cs`：移除分散字典和独立去重表，统一认领并完成回调。

### 兼容性说明

- DLL 导出函数、`__stdcall`、参数、第三方回调 JSON、终端路径和报文字段未改变。
- C# Proxy 继续保持 `net46`、`x86`，未新增第三方依赖。
- DLL 与 Proxy 现有内部 HTTP 路由和成功响应结构保持不变。
- 行为修正仅涉及错误链路：重复、过期和终端切换后的旧回调不再转发。

### 风险与注意事项

1. Proxy 重启后内存注册表不会恢复，重启前终端遗留回调会按未登记请求跳过。
2. 逐笔请求默认保留 10 分钟，流程资源上下文保留 8 小时；流程结束会提前取消。
3. `DllCallbackUrl` 已纳入请求上下文，但当前 `DllCallbackSender` 仍按统一配置地址发送，保持现有行为。

### 验证状态

- [x] C# Proxy `Compile|Release|x86`：通过。
- [x] 同一流程 request_id 的 OCR/NFC/虹膜三资源上下文并存测试：通过。
- [x] 首次回调认领、重复回调拒绝、完成后迟到回调拒绝：通过。
- [x] 回调早于受理响应的状态竞争测试：通过。
- [x] terminal generation 取消和取消后迟到受理测试：通过。
- [x] `authorization` 与 `protocol` 资源类型归一化测试：通过。
- [x] `git diff --check`：通过，仅存在工作区既有 LF/CRLF 提示。
- [ ] 真实终端 OCR/NFC 原 request_id 受理和回调联调。
- [ ] 流程内 OCR/NFC/虹膜自动回调联调。
- [ ] Proxy 重启、终端迟到回调和 8 小时流程 TTL 场景验证。

### 下一步计划

- [ ] 阶段 3：实现 `ProxyRuntime` 生命周期、活动任务跟踪和可等待停止。
- [ ] 验证 Proxy 停止时端口、线程、HTTP 请求和队列在 3～5 秒内释放。

### 回退方式

- 删除 `Core/RequestRegistry.cs`，恢复上述 3 个 Server 文件使用原保存目录、回调地址和去重字典；不涉及 DLL 文件回退。

## 方案 C 内部结构重构收口（2026-06-29）

- [x] Proxy 后台任务改为真正有界启动，容量满时不创建未追踪任务。
- [x] Proxy 停止、活动连接、队列线程和 IDisposable 资源统一收口。
- [x] UI 与 DLL 终端切换统一进入 `SwitchCoordinator`。
- [x] DLL 新增 `SdkRuntime` 和导出调用租约，保护 Init/Release 并发。
- [x] `ReleaseSdk` 增加在途调用等待、回调线程限时停止和失败重试语义。
- [x] 删除 DLL 重复业务队列实现，C# Proxy 为唯一业务队列所有者。
- [x] C# Proxy `Release|x86` 编译通过，24/24 核心单元测试通过。
- [x] DLL `Release|Win32` 编译通过，导出表 20/20 与基线一致。
- [x] x86 生命周期验证连续 3 次 Init/Release 成功，Release 最大 1ms。
- [ ] 正式 Windows 环境执行 7 项 Proxy 集成测试。
- [ ] 真实终端执行并发、切换、异常断开、2 小时及 24 小时长稳验证。

详细修改、兼容性、风险和回退方式见：
`demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。

## DLL 回调响应与 INFO 日志（2026-07-01）

### 当前阶段

- [x] 确认当前 DLL 源码已使用响应体实际字节数生成 `Content-Length`。
- [x] DLL 回调接收、流程回调处理和第三方回调成功日志调整为 `INFO`。
- [x] 普通请求与流程模式均保留 MRZ、IC卡号明文日志，并增加 `request_id`。
- [ ] 使用真实终端完成 OCR、NFC 回调联调。

### 本次修改内容

1. 回调服务器在 `INFO` 级别记录回调路径、来源地址和请求体大小；本次新增日志不记录完整 JSON 或 Base64。
2. 流程模式回调在 `INFO` 级别记录 `request_id` 和资源类型。
3. OCR、NFC 成功日志保留 MRZ、IC卡号，并补充 `request_id` 便于跨 DLL/Proxy 日志关联。
4. 第三方回调成功后记录事件类型、`request_id`、资源类型和状态。
5. 确认 C# Proxy 的 HTTP 响应按 UTF-8 字节数设置 `Content-Length`，回调发送端按 2xx 判断投递成功；本次未修改其接口或业务逻辑。

### 涉及文件

- `src/callback_server.cpp`：回调接收日志提升为 `INFO`；保留动态 `Content-Length` 响应逻辑。
- `src/event_dispatcher.cpp`：回调处理、MRZ/IC卡号和第三方派发成功日志完善。
- `todo.md`：记录修改范围与验证结果。

### 兼容性说明

- DLL 导出函数、参数、`__stdcall` 调用约定和回调 JSON 未改变。
- C# Proxy 继续保持 `net46`、`x86`，未新增依赖。
- HTTP 成功响应仍为 `202 Accepted` 和 `{"status":"ok"}`，仅确保 `Content-Length` 与响应体一致。

### 风险与注意事项

1. MRZ、IC卡号为敏感信息，需限制日志访问权限和留存周期。
2. 每次回调增加约 2～3 行 `INFO` 日志，日志量会小幅增加。
3. 本次新增的 `INFO` 日志不记录完整回调 JSON 和 Base64，避免进一步增加大报文及证件图片日志。

### 验证状态

- [x] DLL `Release|Win32`：编译通过，0 警告、0 错误。
- [x] C# Proxy `Release|x86`：编译通过，0 警告、0 错误。
- [x] C# Proxy 测试项目 `Release|x86`：编译通过，0 警告、0 错误。
- [x] `DllCallbackSenderTests`：3/3 通过。
- [x] DLL 导出表：20/20 与 v1.2 基线一致，目标架构为 x86。
- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [ ] 真实终端 OCR/NFC 回调、第三方回调内容及新日志格式：待验证。

### 下一步计划

- [ ] 部署 `Release/HZCYKJTHardWare.dll` 与 C# Proxy x86 Release 生成物进行联调。
- [ ] 核对 C# 日志不再出现“将内容复制到流时出错”。
- [ ] 核对 DLL 日志包含回调路径、`request_id`、MRZ/IC卡号和第三方派发结果。

### 回退方式

- 恢复 `src/callback_server.cpp`、`src/event_dispatcher.cpp` 本节对应的日志语句，并删除本节进度记录。
- 动态 `Content-Length` 修复在本次修改前已存在于工作区，不应随本次日志级别回退而撤销。

## 第三方预览方案 B（2026-06-29）

- [x] HTTP MJPEG 断流由播放器上报 `PreviewManager`，不再永久重试失效旧 URL。
- [x] 串行释放旧播放器、刷新 URL，并在原 HWND 自动重建预览。
- [x] 增加会话代次、单恢复任务约束及 1/2/5/10 秒有界退避。
- [x] 禁止 HTTP MJPEG 临时 URL 的 60 秒后台主动刷新，保留 RTSP 校验。
- [x] C# Proxy/Test `Release|x86` 编译通过，非集成单元测试 26/26 通过。
- [ ] 真实终端验证断流恢复、HTTP 服务恢复、主动停止和终端切换竞争。
- [ ] 记录恢复黑屏时长并执行 2 小时/24 小时长稳测试。
