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

## DLL 到 Proxy HTTP 并发与初始化顺序优化（2026-07-09）

### 当前阶段

- [x] 完成不改变第三方 DLL 导出接口的最小改动。
- [x] 完成 DLL `Release|Win32` 编译验证。
- [ ] 真实终端环境验证人脸和指纹同时抓拍吞吐。

### 本次修改内容

1. `DelphiProxy` 改为复用 `HzsjkjtContext::http_client`，避免每次请求创建新的 WinHTTP session。
2. `HttpClient` 移除成员级 `CRITICAL_SECTION` 整段串行保护；每次请求仍独立创建 `Connect/Request` handle。
3. `InitSdk` 在首次 `/ping` Proxy 前创建全局 `HttpClient`，失败路径释放，避免空指针风险。
4. `DelphiProxy` 增加 `http_client == nullptr` 防护日志。
5. `HttpClient` 在 `INFO` 日志中记录 `elapsed_ms`、请求大小和响应大小，便于现场压测判断瓶颈是否仍在 DLL 到 Proxy 链路。

### 涉及文件

- `src/http_client.cpp`
- `src/http_client.h`
- `src/delphi_proxy.cpp`
- `src/exports.cpp`

### 兼容性说明

- DLL 导出函数、参数、`__stdcall` 调用约定、`.def` 导出名未改变。
- 第三方 Delphi 7 调用方式不变。
- DLL 到 Proxy HTTP 路由和 JSON 协议不变。
- C# Proxy 本次未修改，仍由 Proxy 端业务队列控制终端请求并发。

### 风险与注意事项

1. WinHTTP session 现在由 DLL 全局复用，多个请求并发共享 session，但 request handle 仍为每次调用独立创建。
2. Proxy 当前响应头仍为 `Connection: close`，本次没有改造 Proxy Keep-Alive 循环；收益主要来自取消 DLL 侧公共串行点和复用 session。
3. 如果现场合计吞吐仍稳定在 5 次/秒，应继续检查 Proxy 队列执行耗时、终端 HTTP 响应耗时和终端本身处理能力。
4. `INFO` 日志量会增加；完成现场验证后可按需要降回 `DEBUG` 或增加耗时阈值。

### 验证状态

- [x] DLL `Release|Win32` 编译通过：0 warning，0 error。
- [x] `HZCYKJTHardWare.def` 和 public header 未修改，ABI 面保持不变。
- [ ] Delphi 7 第三方程序加载验证：待验证。
- [ ] 两终端真实环境人脸/指纹同时抓拍 20 秒吞吐验证：待验证。
- [ ] 2 小时与 24 小时长稳内存、句柄、线程数观察：待验证。

### 下一步计划

- [ ] 现场对比修改前后人脸单抓、指纹单抓、人脸+指纹同时抓拍的成功数/秒。
- [ ] 重点查看 DLL 日志 `HTTP POST完成 ... elapsed_ms=...`，确认是否存在单次请求约 200ms 且串行等待的现象。
- [ ] 如仍合计 5 次/秒，继续在 Proxy `WorkerQueue` 和 `TerminalClient.PostJsonAsync` 增加分段耗时日志。

### 回退方式

- 恢复上述 4 个文件到本节修改前版本，重新编译 `Release|Win32` DLL。

## Proxy 车牌预览界面汉化与布局优化（2026-07-03）

- [x] 预览控制按钮移除内部函数名，统一改为中文业务名称。
- [x] CJ 显示为“出境车牌预览”，RJ2/RJ3 显示为“入境车牌预览 2/3”。
- [x] 占位提示和操作日志同步使用入境/出境中文名称。
- [x] 增加顶部控制卡片高度，并为上下两排预览画面增加间距。
- [x] C# Proxy `Release|x86|net46` 独立输出编译通过，0 警告、0 错误。
- [ ] 启动新 Proxy，在现场 DPI 和分辨率下确认按钮无重叠、日志区域高度可接受。

本次仅修改 Proxy 管理界面，不改变 DLL 导出函数、HTTP 路由、配置结构和第三方调用行为。

## 车牌 CJ/RJ2/RJ3 平铺接口（2026-07-03）

- [x] Proxy 不引入 `Direction`，由第三方调用方选择 CJ 或组合调用 RJ2、RJ3。
- [x] 删除旧 `StartPlatePreview/StopPlatePreview` 导出，新增 CJ、RJ2、RJ3 三组独立 `Start/Stop`。
- [x] 三路使用独立配置、RTSP URL、DLL 租约、Proxy 会话和 HWND，可分别启停。
- [x] Proxy 管理界面增加三组测试按钮和三块独立预览区域。
- [x] `preview.plate` 调整为 `cj`、`rj2`、`rj3` 三个平铺节点。
- [x] DLL `Release|Win32` 编译通过，导出表确认 24 个符号且旧车牌符号不存在。
- [x] C# Proxy/Test `Release|x86|net46` 独立输出编译通过；车牌配置测试 4/4 通过。
- [x] C# 第三方 Demo `Release|x86|net46` 编译通过，六个新 P/Invoke 声明有效。
- [ ] 使用三台真实相机验证 CJ 单路、RJ2+RJ3 双路并发、分别停止及 Proxy 重启恢复。
- [ ] 第三方程序同步改用新接口后执行完整回归和 2 小时/24 小时长稳测试。

详细修改、兼容性、风险、验证与回退方式见：
`demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。

## release/1.2.6 P0 修复清单（2026-07-10）

- [x] 创建并推送修复前基线 `release/1.2.6@283883a9`。
- [x] 终端切换成功响应等待真实路由提交。
- [x] SDK 不可逆释放失败进入 `Faulted`，不恢复半运行状态。
- [x] 回调终端信息使用 Session 快照并消除全局字符串并发读写。
- [x] 固定抓拍文件使用同目录临时文件和原子替换。
- [x] DLL 回调默认绑定 loopback，Proxy 严格校验终端来源 IP。
- [x] 日志保留、容量、磁盘预警和批量刷新；不做业务字段脱敏。
- [x] Native/C# VLC 不再调用进程级 `SetDllDirectory`。
- [x] DLL `Release|Win32` 编译通过，0 警告、0 错误。
- [x] Proxy/Tests `Release|net46` 编译通过，0 警告、0 错误。
- [x] 非 Integration 单元测试 75/75 通过。
- [ ] 确认真实终端 `/process/end` 协议和幂等行为。
- [ ] 正式 Windows 测试宿主运行 7 项 `HttpListener` Integration。
- [ ] 真实双终端、预览和 24～72 小时资源长稳验证。

## OCR ID 卡光学鉴伪兼容支持（2026-07-10）

### 当前阶段

- [x] 保持原 OCR 请求接口、DLL 导出函数、`__stdcall` 回调签名和公共结构体不变。
- [x] 支持 `data.card_type=30` 的 ID 卡人员字段和光学鉴伪字段解析。
- [x] ID 卡扩展字段已贯通“终端推送 → C# Proxy → C++ DLL → 第三方 eventJson”。
- [x] C# 第三方 Demo 已同步展示 ID 卡人员信息和光学鉴伪结果。
- [x] 新建 V1.5 DOCX 和 2026-07-10 日期版 Markdown，并保持 V1.4 文档的页面、样式和代码着色体系。
- [x] ID 卡通过现有 `mrz` 字段返回 `$证号^鉴伪分数^出生日期^签发日期^姓名^性别` 兼容串，同时保留独立 JSON 字段。
- [x] 其他证件类型继续使用原 OCR 回调字段，不增加 ID 卡专用字段。
- [ ] 使用真实 ID 卡终端完成现场联调。

### 本次修改内容

1. `CallbackParser.ParseOcrDocument` 仅在 `card_type=30` 时读取 `person_info[0]` 和 `optics_authen`。
2. ID 卡复用终端已有的 `name`、`sex`、`cardId`、`birthday`、`dateOfissue` 字段名和值。
3. `authen_score` 和 `optical_check_result` 缺失、为 `null`、类型非法或整数越界时使用 `-1`。
4. `optical_check_result` 仅接受 `0`（通过）和 `1`（不通过），其他整数统一转换为 `-1`，避免误判为通过。
5. Proxy 仅对 ID 卡向 DLL 增量发送人员和鉴伪字段；非 ID 卡继续发送原 `request_id/mrz/save_path` JSON。
6. DLL 仅对 ID 卡 OCR 成功事件向第三方 JSON 增加 `card_type`、人员字段和鉴伪字段。
7. 增加 ID 卡完整、失败、字段缺失、`null`、非 ID 卡、旧 OCR 和非法类型测试。
8. C# Demo 仅在 `ocr_document + card_type=30` 时展示人员字段、鉴伪分数和“通过/不通过/未知”状态；整数缺失默认值显式使用 `-1`。
9. C# Proxy 的 ID 卡 OCR 成功日志不再打印 MRZ，改为打印经过控制字符清理和长度限制的姓名、性别、证号，以及鉴权分数和中文鉴伪结果；其他证件类型继续打印 MRZ。
10. 接口文档增加 ID 卡字段表、`1601` 回调示例、默认值语义和 `HZCYKJTHardWare_RequestOCR` 说明；未修改 DLL 函数声明或回调签名。
11. Proxy 向 DLL 投递 `card_type=30` 结果时，将 `mrz` 改为 `$证号^鉴伪分数^出生日期^签发日期^姓名^性别`；文本缺失保留分隔槽位，鉴伪分数缺失使用 `-1`，原独立字段继续返回。

### 涉及文件

- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Parsing/CallbackParser.cs`：OCR ID 卡模型和容错解析。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/TerminalCallbackHandler.cs`：业务转换、鉴伪日志和 DLL 投递。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/DllCallbackSender.cs`：ID 卡兼容串和扩展 JSON，保留原公开重载。
- `src/event_dispatcher.cpp`：严格整数解析、ID 卡日志和第三方回调 JSON 扩展。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Core/OcrIdCardCallbackTests.cs`：7 个 OCR 解析场景。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Core/DllCallbackSenderTests.cs`：ID 卡兼容串、缺失槽位和旧回调兼容测试。
- `demo/CSharpThirdPartyDemo/HZCYKJTHardWare.CSharpDemo/MainForm.cs`：ID 卡兼容串、回调字段解析和日志展示。
- `demo/CSharpThirdPartyDemo/README.md`：ID 卡兼容串、演示字段及状态说明。
- `第三方接口调用说明.md`：ID 卡兼容串、字段和状态定义。
- `HZCYKJTHardWare接口调用说明V1.5_20260710.docx`：基于 V1.4 原格式补充 ID 卡回调说明。
- `第三方接口调用说明20260710.md`：2026-07-10 日期版第三方接口说明。
- `todo.md`：本次进度记录。

### 兼容性说明

- 外部函数：未修改函数名、参数、返回值、错误码或调用方式。
- 回调 ABI：仍为 `THZCYKJTHardWareEventCallback(const char* eventJson)`，调用约定仍为 x86 `__stdcall`。
- 公共结构体：`HZCYKJTHardWare_EVENT` 未修改，字段布局和大小不变。
- 第三方 JSON：仅 ID 卡 OCR 成功事件增加可选字段；其他 OCR 类型字段集合保持不变。
- ID 卡兼容：`mrz` 从空值改为以 `$` 开头的六字段兼容串；已确认保留 `card_type`、人员和鉴伪独立字段。
- Proxy：继续使用 `.NET Framework 4.6/x86`，未新增依赖。
- DLL：继续生成 Win32/x86，当前导出表仍为 24 项。

### 风险与注意事项

1. 第三方应按 JSON 字段名解析，不应依赖字段顺序；ID 卡字段为增量可选字段。
2. `cardId` 和 `dateOfissue` 沿用终端原始大小写，第三方解析时需保持一致。
3. 人员字段缺失或类型异常时返回空字符串；两个鉴伪整数字段返回 `-1`。
4. Proxy ID 卡日志包含姓名、性别和证件号码等敏感信息，需限制日志访问权限与留存周期。
5. C++ 旧 `ResultParser::ParseOcrResult` 当前位于停用的 `#if 0` 路径，本次未修改；如恢复旧原始报文处理，需要同步支持 ID 卡字段。
6. 自动测试使用模拟 JSON，真实终端是否存在字段大小写或协议版本差异仍需现场确认。
7. 兼容串使用 `^` 作为固定分隔符，字段内容如果自身包含 `^` 会产生解析歧义；当前按终端原值透传，不主动改写证件数据。
8. ID 卡兼容串包含证号、姓名、生日等敏感信息，第三方不得直接写入无访问控制的日志。

### 验证状态

- [x] C# Proxy/Test `Release|x86|net46` 编译：0 warning，0 error。
- [x] C# Proxy ID 卡日志调整使用隔离输出目录编译：0 warning，0 error；正式输出 EXE 被运行中的 Proxy 占用，本次未停止进程或覆盖文件。
- [x] C# 第三方 Demo `Release|x86|net46` 编译：0 warning，0 error。
- [x] 非集成自动测试：72/72 通过，0 失败、0 跳过。
- [x] 新增 OCR ID 卡解析测试：7/7 通过。
- [x] 新增 Proxy→DLL JSON 兼容测试：2/2 通过。
- [x] DLL `Release|Win32` 编译：0 warning，0 error。
- [x] DLL 架构：PE32/x86；24 项导出及 `__stdcall` 装饰保持不变。
- [x] 公共类型头文件和 `.def` 导出文件无修改。
- [x] V1.5 DOCX 结构检查：除 `word/document.xml` 外，其余文档包部件与 V1.4 内容一致；原 V1.4 文件未修改。
- [x] V1.5 DOCX 使用 Microsoft Word 导出并逐页检查：9/9 页通过，无内容截断、重叠或表格跨页断行。
- [x] ID 卡兼容串新增测试：完整字段和缺失字段固定槽位均通过。
- [x] Proxy/Test `Release|x86|net46` 隔离编译：0 warning，0 error；非集成测试 76/76 通过。
- [x] C# 第三方 Demo `Release|x86|net46` 隔离编译：0 warning，0 error。
- [x] 更新后的 V1.5 DOCX 使用 Microsoft Word 再次逐页检查：9/9 页通过。
- [ ] 既有 `HttpListener` 集成测试：当前运行环境在 `MockTerminalServer` 初始化时抛出 `PlatformNotSupportedException`，7 项未进入业务断言；需在支持 `System.Net.HttpListener` 的正式 Windows 测试宿主复验。
- [ ] 真实终端 `card_type=30`、鉴伪通过/不通过/缺失字段联调：待验证。
- [ ] 第三方 Delphi/C# 程序实际解析新增 JSON 字段：待验证。

### 下一步计划

- [ ] 部署最新 x86 DLL 和 C# Proxy，使用真实 ID 卡验证 `0/1/-1` 三种状态。
- [ ] 核对第三方回调中的 `name/sex/cardId/birthday/dateOfissue` 与终端原始数据一致。
- [ ] 使用真实 ID 卡核对 `mrz` 精确等于 `$证号^鉴伪分数^出生日期^签发日期^姓名^性别`。
- [ ] 使用原有护照等证件回归，确认回调 JSON 不出现 ID 卡专用字段。
- [ ] 进行重复识别、终端切换、字段异常和 2 小时稳定性验证。

### 回退方式

- 恢复上述 Proxy、DLL 和测试文件中本节对应的 ID 卡增量逻辑。
- 删除 `第三方接口调用说明.md` 和 `todo.md` 中本节新增记录。
- 公共结构体和导出接口未修改，无需第三方重新编译即可回退。

## 终端切换与预览后台恢复解耦（2026-07-09）

### 当前阶段

- [x] 保持第三方 Delphi 调用接口、DLL 导出函数、HTTP 路由和请求/响应格式不变。
- [x] Proxy 终端路由切换完成后立即清除 `SwitchingTerminal`，不再等待所有预览恢复。
- [x] 预览恢复改为后台执行，并在日志中区分“终端切换完成”和“预览后台恢复完成”。
- [x] 后台恢复按摄像头优先、指纹随后处理，避免当前指纹预览慢时阻塞人脸预览显示。

### 本次修改内容

1. `SwitchCoordinator` 在 `TerminalManager.SwitchTo()` 成功后记录终端切换完成日志，并启动后台预览恢复任务。
2. `PreviewManager` 增加预览恢复优先级排序，摄像头优先于指纹和虹膜。
3. `PreviewManager` 为每一路预览增加后台恢复开始、完成、未完成和失败日志。

### 涉及文件

- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/Coordinator/SwitchCoordinator.cs`：终端切换完成标志提前释放，预览恢复后台化。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Preview/PreviewManager.cs`：后台恢复日志和摄像头优先恢复顺序。
- `todo.md`：记录本次修改范围、兼容性和验证状态。

### 兼容性说明

- 外部接口：未改变。
- 第三方调用：未改变 DLL 函数名、参数、调用约定、错误码和 JSON 字段。
- 配置文件：未新增配置项。
- 部署方式：未改变。

### 风险与注意事项

1. 切换完成后预览可能短时间处于后台恢复中，人脸会优先显示，指纹慢恢复不再阻塞切换完成。
2. 如果指纹组件仍处于 `starting/device_not_ready`，指纹抓拍仍可能失败，需要继续排查终端侧指纹组件状态。
3. 预览生命周期仍保持全局 `_operationLock` 串行保护，避免过度并发引入 VLC/MJPEG 资源竞争。

### 验证状态

- [x] C# Proxy `Release|x86|net46` 隔离输出编译通过：0 warning，0 error。
- [ ] DLL `Release|Win32` 编译验证：本次未改 DLL，按需复验。
- [ ] 真实终端切换：待验证 `/process/start` 不再因预览恢复慢等待 5000ms。
- [ ] 真实终端预览：待验证人脸先恢复显示，指纹恢复慢时后台继续恢复。

### 下一步计划

- [ ] 编译 C# Proxy。
- [ ] 使用真实终端连续切换 20 次，检查日志中的“终端切换完成”和“预览后台恢复完成”耗时。
- [ ] 核对 DLL 日志不再出现 `/process/start elapsed_ms=5000` 且返回 `terminal_switching`。

### 回退方式

- 恢复 `SwitchCoordinator.SwitchToCoreAsync()` 中同步等待 `RestartPreviewsOnTerminalSwitch()` 的旧逻辑。
- 删除 `PreviewManager` 中本次新增的恢复优先级排序和后台恢复分路日志。

## 试验性回退 StartProcess 切换等待串行逻辑（2026-07-09）

### 当前阶段

- [x] 按用户要求回退 `SwitchTerminal -> StartProcess` 顺序等待逻辑，用于验证切换慢是否来自等待/串行路径。
- [x] 保留第三方 DLL 导出函数、调用约定、参数、错误码和 HTTP JSON 协议不变。
- [x] 保留终端切换后的预览后台恢复改动。
- [ ] 真实第三方现场复测待执行。

### 本次修改内容

1. DLL `StartProcess` 检测到本地 `switch_pending` 时立即返回 busy，不再最多等待 15 秒。
2. DLL 转发 `/process/start` 不再把内部 HTTP 超时提升到 22 秒，恢复使用默认请求超时。
3. Proxy DLL 入口 `/process/start` 不再绕过切换中快速拒绝逻辑，切换中立即返回 `terminal_switching`。
4. Proxy 管理界面 `StartProcess` 不再等待切换完成，拿不到控制门禁或稳定终端路由时立即返回 `Busy`。

### 涉及文件

- `src/exports.cpp`
- `src/delphi_proxy.h`
- `src/delphi_proxy.cpp`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/DllCommandHandler.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/Coordinator/BizOperationHandler.cs`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`
- `todo.md`

### 验证状态

- [x] C# Proxy `Release|x86|net46` 隔离输出编译通过：0 warning，0 error。
- [x] DLL `Release|Win32` 编译通过：0 warning，0 error。
- [ ] 现场连续执行“切换终端后立即开始流程”：待验证。

## 通道级车牌 RTSP 预览（2026-07-02）

- [x] 保持现有 `StartPlatePreview(HWND)` / `StopPlatePreview()` DLL ABI 不变。
- [x] 车牌 VLC 由 C# Proxy 加载，Proxy 直接将 libVLC 输出绑定到第三方传入的专用 HWND。
- [x] 车牌镜头按通道单实例管理，不绑定终端1/2，不参与终端切换。
- [x] Proxy 新增独立本地车牌调试预览，可与第三方预览同时运行并分别启停。
- [x] DLL 新增车牌 Proxy 路由、外部预览租约、Proxy 重启恢复和 ReleaseSdk 停止流程。
- [x] 新增 `preview.plate` 配置，默认主码流 `101`，可显式切换子码流 `102`。
- [x] 用户名和密码按 RTSP user-info 百分号编码，日志中的认证信息统一脱敏。
- [x] SDK 释放时通知 Proxy 停止外部车牌会话；Proxy 负责释放 VLC 资源。
- [x] DLL `Release|Win32` 编译通过，0 警告、0 错误，PE32/x86 与 20/20 导出保持不变。
- [ ] 配置现场车牌相机 IP、用户名、密码并将 `enabled` 设置为 `true`。
- [ ] 使用真实第三方 HWND 验证启动、停止、重复启动、窗口销毁和 SDK 释放。
- [ ] 使用真实相机验证第三方与 Proxy 调试双路预览、单路停止隔离及相机连接数上限。
- [ ] 使用真实主码流执行 2 小时/24 小时稳定性验证。

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

## 外部预览跨宿主与 Proxy 重启恢复（方案 B，2026-07-02）

- [x] 修改前提交当前 DLL、C# Proxy、测试和进度文件，创建 `v1.2.6` 标签（`d7b1fbb`），未包含 `.ico`。
- [x] Proxy `/ping` 增加服务实例标识，保持 `status=ok` 兼容。
- [x] Proxy 按 `HWND + PID + 进程启动时间` 清理退出宿主的旧外部预览。
- [x] DLL 在摄像头/指纹预览活动时检测 Proxy 中断和实例变化，并自动重发原预览请求。
- [x] `ReleaseSdk` 有界停止租约监控，并尽力停止 Proxy 端摄像头/指纹预览。
- [x] Proxy/Test `Release|x86|net46` 编译通过，非集成测试 46/46 通过。
- [x] DLL `Release|Win32` 编译通过，导出表 20/20、PE32/x86 保持不变。
- [x] 假 Proxy 生命周期验证通过：中断恢复后启动请求总数 2，Release 停止请求 1。
- [ ] 正式 Windows 环境执行 7 项 `HttpListener` 集成测试。
- [ ] 真实终端验证 Demo 关闭后第三方接管、Proxy 重启自动恢复和终端切换竞争。
- [ ] 执行连续 20 次重启以及 2 小时/24 小时长稳测试。

详细修改、兼容性、风险、验证与回退方式见：
`demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。
## DLL `/ping` 成功日志降级（2026-07-10）

### 当前阶段

- [x] 高频 `/ping` 成功日志由 `INFO` 调整为 `DEBUG`。
- [x] 其他 HTTP GET 成功日志继续使用 `INFO`，HTTP 失败日志继续使用 `ERROR`。

### 涉及文件

- `src/http_client.cpp`：按 URL 后缀识别 `/ping` 并调整成功日志级别。
- `todo.md`：记录本次修改和验证状态。

### 兼容性说明

- DLL 导出函数、参数、返回值、`__stdcall`、结构体和回调 JSON 均未改变。
- HTTP 请求频率、超时、响应处理和 `/ping` 健康检查逻辑均未改变。
- 默认 `INFO` 阈值下不再输出 `/ping` 成功日志；配置为 `debug` 时仍可查看。

### 验证状态

- [x] DLL `Release|Win32` 编译验证：0 warning，0 error。
- [ ] 运行验证：默认 `INFO` 下只隐藏 `/ping` 成功日志，失败日志仍正常输出。

### 回退方式

- 将 `src/http_client.cpp` 中 `/ping` 成功分支恢复为 `LOG_INFO`。

## P1 方案 B 压测结论与回退（2026-07-15）

- [x] 分析约 18 小时 15 分钟 x86 真实双终端压力测试日志。
- [x] 确认功能、终端隔离、队列、线程和句柄没有新增 P0 问题。
- [x] 确认抓拍响应流式解析未达到降低 GC 压力的目标。
- [x] 定向恢复人脸和指纹的 `PostJsonAsync + CallbackParser.ParseImageCapture` 路径。
- [x] 删除 `PostImageCaptureAsync`、`ImageCaptureStreamParser` 和 3 项专用测试。
- [x] 保留 P0、固定文件覆盖、x64/VLC 和 `ReleaseSdk active=1` 后续修复。
- [x] 回退后 Proxy + Tests x86/x64 Release 编译通过，非 Integration 回归均为 91/91。
- [ ] 真实终端 2 进行 30～60 分钟短压测，并比较指纹延迟与每次抓拍 GC。
- [ ] 回退版本完成 24～72 小时长稳验证。

详细实测数据、兼容性和回退边界见：
`demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。

## DLL 日志 BOM 与授权编码核对（2026-07-15）

- [x] 确认 DLL 日志主体是无 BOM UTF-8，授权姓名混入 GBK 字节导致文件不再是严格 UTF-8。
- [x] 确认 GBK 字节对应“港旅客二”，且 EXE 收到请求时字段已经损坏。
- [x] 确认仓库 C# Demo 授权参数使用 `Utf8NativeString`，源码无需修改。
- [x] Native Logger 仅为空白新日志写入 UTF-8 BOM，已有日志继续原样追加。
- [x] DLL Win32 Release 和 C# Demo x86 Release 编译通过，均为 0 warning、0 error。
- [x] 验证 Demo 将“港旅客二”编码为正确 UTF-8 字节。
- [x] 验证新日志文件头为 `EF BB BF`，两个进程连续追加后 BOM 总数仍为 1。
- [x] 记录 C# Demo/DLL SHA-256，供现场部署文件核对。
- [ ] 现场用“港旅客二”验证 DLL、EXE、终端及回调全链路编码。

注意：BOM 只解决查看器识别；后续已增加 `third_party_input_encoding`，DLL 可将 GBK 输入归一化为 UTF-8。

## DLL 第三方输入编码兼容（2026-07-15）

- [x] 根配置新增 `third_party_input_encoding`，支持 `auto`、`gbk`、`utf8`，默认 `auto`。
- [x] `auto` 对非 ASCII 输入先严格校验 UTF-8，失败后按 Windows CP936/GBK 转换。
- [x] `gbk` 强制将第三方输入转换成 UTF-8；`utf8` 严格校验 UTF-8。
- [x] 已覆盖流程、人脸、指纹、虹膜、OCR、NFC 的路径参数及授权 7 个字符串字段。
- [x] DLL 内部、日志、HTTP/JSON、Proxy 和第三方回调继续保持 UTF-8。
- [x] DLL `Release|Win32` 编译通过，0 warning、0 error。
- [x] x86 运行验证通过：`auto+GBK`、`auto+UTF-8`、`gbk+GBK`、`utf8+UTF-8` 以及无配置默认 `auto+GBK`。
- [ ] 使用正式 Delphi 第三方程序执行中文路径、授权姓名和回调全链路验证。

兼容性：未修改 DLL 导出名称、参数、`__stdcall`、返回值或回调签名；未修改 C# Proxy 和 Delphi 示例源码。

## Proxy x64 与真实 `/process/end`（2026-07-15）

- [x] Proxy/VLC x64 独立构建与 PE 架构校验。
- [x] DLL 保持 Win32/x86，导出 ABI 和第三方调用方式不变。
- [x] `/process/end` 同步转发当前终端，校验 HTTP 202、`status=accepted` 和一致的 `request_id`。
- [x] 按最终定义修正：Start/End 只控制终端是否推送，Proxy 不使用本地 Session 判断是否接收。
- [x] End 不再取消 OCR/NFC/虹膜/授权请求；End 后在途流程回调保留路由并正常转交。
- [x] DLL 删除 End 时 `CancelAll` 和 `process_active` 回调门禁，外部导出接口不变。
- [x] 路由缓存有界保留 10 分钟、最多 256 条；保留来源 IP 和当前终端隔离检查。
- [x] x64 Proxy/Tests 编译通过，104/104 测试通过；Win32 DLL 编译和 24 项导出检查通过。
- [ ] 设备方修正文档“请求体字段无/示例含 request_id”的矛盾。
- [ ] 真实双终端抓拍延迟回归、x64 VLC 预览、异常重启与 24～72 小时长稳。

详细范围、风险、验证和回退见：
`demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。

## x64 Proxy 句柄来源隔离验证（2026-07-16）

- [x] 压测脚本增加 `Idle`、`SwitchOnly`、`CaptureOnly` 三种隔离负载，默认 `FullFlow` 行为不变。
- [x] 静置 3 分钟：句柄未增长，线程由 31 降至 20。
- [x] 同终端重复切换 99 次且关闭预览：句柄未增长。
- [x] 双终端交替切换 99 次且关闭预览：句柄呈波动回收，没有按切换次数持续累积。
- [x] 双终端交替切换 99 次且开启相机/指纹预览：预热后句柄增加 230，趋势约 `+37.4/min`。
- [x] 固定终端高频抓拍 6 分钟：818 次调用全部成功，395 次人脸和 395 次指纹抓拍成功，句柄趋势约 `-0.75/min`。
- [x] 句柄类型对照确认：预览切换残留比抓拍基线多约 203 个 `Thread` 句柄，即约 `2.05/次切换`。
- [x] 代码交叉验证：每路 `VlcPreviewController` 创建一个专用线程，终端切换时两路预览控制器被销毁并重建；当前 `DisposeAsync` 等待停止动作完成，但不等待线程真正退出。
- [ ] 按确认后的方案修正预览线程生命周期，再执行相同 99 次切换 A/B 回归。

结论：当前句柄压力来自“预览开启时切换终端”的 VLC/预览线程生命周期，不来自 `Start/End`、高频抓拍或无预览的终端切换。尚未修改 Proxy 生产代码和任何第三方接口。

## 预览句柄释放方案 A 实施与验证（2026-07-16）

- [x] `VlcPreviewController` 增加线程退出完成信号、幂等有界释放及启动失败清理。
- [x] `_restartInfo` 改为不持有 `Player/Thread` 的轻量 `PreviewRestartInfo`。
- [x] 新增2项重启信息引用测试，2/2通过。
- [x] Proxy x64 Release 编译通过，0 warning、0 error。
- [x] 真实双终端相机+指纹预览交替切换89次，全部成功，线程退出超时0次。
- [ ] 句柄验收未通过：预热后约678升至峰值896，停止后862，空闲3分钟后约774；仍有211个 `Thread` 句柄、实际线程约30个。
- [ ] 方案 A 只能解决释放顺序和旧引用，不能消除反复创建 STA `Thread` 的 CLR 句柄滞留；等待确认进入固定 STA 线程复用方案。
- [ ] 方案 B 完成后按同口径执行约100次切换、2小时短稳和24～72小时长稳。

兼容性：未改变 DLL ABI、HTTP/终端协议、配置、回调格式和第三方调用方式；DLL仍为x86，本次Proxy验证为x64 Release。

详细验证数据与回退说明见：
`demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。

## 预览句柄释放方案 B VLC 实验与回退（2026-07-16，结论已更正）

- [x] B1 固定相机/指纹 STA worker，x64 Release 编译和真实双终端 89 次切换完成。
- [x] B2 继续复用 libVLC instance；B3 复用 media player；B4 增加 stopped 状态等待。
- [x] 四个变体功能均正常、DLL 调用 0 失败。
- [ ] 句柄验收均未通过：B1/B2/B3/B4 后半段趋势分别约 `+56.91`、`+54.22`、`+44.21`、`+65.34/min`。
- [x] 更正：真实人脸、指纹预览为 HTTP MJPEG，RTSP 使用为0；VLC 日志仅为启动预热，本轮实验没有覆盖实际泄漏链路。
- [x] 未达标的方案 B 实验代码已回退，仅保留方案 A；回退后 Proxy x64 Release 0 warning/0 error，定向测试 2/2，非 Integration 96/96。
- [x] 撤回4路 RTSP 常驻方案，改为复用当前终端的相机、指纹两个 MJPEG worker。

兼容性：本轮未改变 DLL ABI、HTTP/终端协议、回调格式或第三方调用方式；未达标实验实现没有留在生产代码中。

详细数据和各变体指标见：
`demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。

## MJPEG 长生命周期 worker 方案 B（2026-07-17）

- [x] 渲染线程和HTTP读取线程改为每个资源会话只创建一次，终端切换仅取消旧请求并替换URL。
- [x] 增加媒体代次隔离，防止旧终端延迟帧覆盖新终端画面。
- [x] 暂停等待旧请求脱离，最终释放分别等待渲染、读取线程退出。
- [x] 显式停止、窗口失效和Proxy关闭仍完整释放worker；VLC/RTSP兼容路径不变。
- [x] Proxy x64 Release编译通过，0 warning、0 error。
- [x] 定向测试3/3；非Integration完整重跑97/97。
- [x] 3分钟真实测试：88次切换、96次调用、0失败，结束后43个 `Thread` 句柄。
- [x] 10分钟短稳：297次切换、305次调用、0失败；句柄范围593～633，整体斜率 `-1.20/min`，后半段 `+0.57/min`。
- [x] 10分钟结束后总句柄581、`Thread`句柄43、实际线程26；旧版为211～234个 `Thread`句柄。
- [x] MJPEG暂停/停止超时、流恢复失败、VLC回退和RTSP使用均为0。
- [x] 2小时真实硬件短稳完成：120.07分钟、1437次切换、1445次DLL调用、0失败；后半程句柄斜率 `+0.0236/min`、`R²=0.0104`，最后30分钟 `+0.0578/min`。
- [x] 测试停止后句柄类型快照为47个 `Thread`句柄、26个实际线程；旧版为211～234个 `Thread`句柄，原按切换次数累积的泄漏特征已消失。
- [x] Private Memory后半程约58～62MB，单点89.3MB后立即回落；空闲后约59MB，2小时内未见单向累积。
- [x] 1436次后台预览恢复全部成功；Proxy警告、错误、MJPEG暂停/停止超时、恢复失败、RTSP实际使用均为0，VLC仅有启动预热记录。
- [ ] 通过后安排24～72小时长稳和设备断开/恢复验证。
- [ ] 补测外部预览HWND销毁/重建和第三方程序退出/重启后的worker最终释放。

兼容性：未修改DLL ABI、Proxy HTTP/终端协议、配置或第三方调用；仍只保持当前终端的两路MJPEG连接。
## MJPEG 16小时真实硬件长稳收尾核查（2026-07-18）

- [x] 仅核查 `scripts/stress_results/handle_release_mjpeg_scheme_b_16hour_real_restart_20260718_1750`；人工中断旧轮 `handle_release_mjpeg_scheme_b_16hour_real_20260718` 未混入结论。
- [x] 确认本轮未完成 960 分钟：关闭测试 Proxy 前有效片段为 17:51:32～18:04:13，共 152 次切换、12.70 分钟、0 失败；两终端各 76 次成功。
- [x] 关闭前切换耗时：P50/P95/P99/最大值为 3/18.45/52.64/72ms。
- [x] 核查正式目录：仅有 cycles、metrics；本轮没有 summary、calls、callbacks CSV。
- [x] 日志窗口无 warning/error、MJPEG pause/stop timeout 或流恢复失败；摄像头/指纹 MJPEG 预览各启动 153 次，VLC 仅 3 条预热日志且无实际预览证据，RTSP 使用为 0。
- [x] 预热后仅约 7.5 分钟有效指标：HandleCount 668～687，斜率 `+15.70 handles/hour`、`R²=0.0144`，线程 30～35，Private Memory 53.04～54.98MB；不足以判定 16 小时趋势，后半 8 小时和最后 4 小时无数据。
- [x] 关闭前采集 PID 47668 句柄类型快照（678 总句柄，Event 269、Thread 61、Key 46、File 31）；再次核对精确构建路径后，仅终止该测试 Proxy。
- [ ] 修正/确认测试宿主在 Proxy 退出后的自动停止和结果封存，避免继续追加被人为终止后的失败记录。
- [ ] 在独占、不重启、不人工中断的条件下，重新执行完整 960 分钟测试，并产出 summary/calls/cycles/metrics 完整数据后再决定是否修改生产代码。

## 同名抓拍文件一致性方案 A（2026-07-20）

- [x] 保留现有抓拍队列及同名覆盖语义，在 `FileSaver` 层增加有界的同路径互斥。
- [x] 同路径锁覆盖临时文件写入、刷盘和 `MoveFileEx` 原子提交完整过程。
- [x] 对 Win32 错误 5/32/33/1224 按 10/20/40/80ms 进行有限重试，最长额外等待 150ms。
- [x] 增加同路径 12 路并发写入和目标文件短时占用恢复测试。
- [x] Proxy + Tests x64/x86 Release 独立目录编译通过。
- [x] `FileSaverTests` x64 7/7、x86 7/7 通过；x64 非 Integration 回归 99/99 通过。
- [ ] 停止现有 Proxy、部署新构建后执行真实硬件人工抓拍核对。
- [ ] 执行 150 分钟 FullFlow，核查抓拍失败、原子替换日志、临时文件残留及返回文件内容。
- [ ] 真实硬件验证完成前，不将方案 A 标记为现场通过。

兼容性：未修改 DLL ABI、HTTP/终端协议、队列行为、配置或第三方调用；继续覆盖同名文件。详细实现、风险和回退见 `demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。
## MJPEG 16 小时真实硬件长稳收尾复核（2026-07-20）

- [x] 仅复核 `scripts/stress_results/handle_release_mjpeg_scheme_b_16hour_real_restart_20260718_1750`；旧的人工中断轮未混入结论。
- [x] 确认本轮未完成 960 分钟。有效片段为 17:51:32～18:04:13（12.70 分钟），152 次切换、两终端各 76 次、0 失败；P50/P95/P99/Max 为 3/19/71/72 ms（nearest-rank）。
- [x] 确认目录实际有 `calls`、`cycles`、`metrics` CSV，但没有 summary/callbacks CSV。104.40 分钟的 cycles 尾段含 546 次失败，发生在 Proxy 退出后测试宿主仍继续运行期间，不能作为有效长稳数据。
- [x] 预热后可用 Proxy 指标仅 8 分钟：HandleCount 668～687，斜率 `+13.5226/h`、`R²=0.0128`，线程 30～35，PrivateMemory 53.04～54.98 MB；不足以判定长时趋势，后半 8 小时和最后 4 小时没有数据。
- [x] 测试窗口 Proxy 日志无 warning/error、MJPEG pause/stop timeout 或流恢复失败；VLC 只有测试前预热记录，测试窗口 VLC/RTSP 均无实际预览证据。
- [x] 本次检查时 PID 47668 已不存在，未关闭任何进程。
- [ ] 修复测试宿主的失败/Proxy 退出联动停止、结果封存与 summary 生成，防止无效尾段继续追加。
- [ ] 在独占、不中断条件下重新完成完整 960 分钟，产出 summary、calls、cycles、metrics 后再评价句柄趋势和是否需要修改生产代码。

## Proxy 日志可靠性方案 B（2026-07-22）

- [x] 将 `Logger.Flush()` 改为后台写线程确认的刷新屏障，消除“队列已空但文件尚未刷新”的竞态。
- [x] 主日志显式使用 `FileShare.ReadWrite`，支持 Proxy 运行期间读取日志和文件长度。
- [x] 增加写失败、最后刷新时距、当前文件长度、停止状态指标，以及限频应急日志。
- [x] Proxy 正常关闭时停止接收、排空队列，并有界等待 Logger 写线程退出。
- [x] x64/x86 Release 编译；Logger 定向测试各 3/3；非 Integration 回归各 101/101。
- [ ] 在新构建上人工验证运行中 tail、文件长度持续增长及正常关闭后的末尾日志完整性。
- [ ] Integration 10 项需在支持 `HttpListener` 的 Windows 测试宿主重跑；当前环境未验证。
- [ ] 需要长稳复核时再执行真实硬件测试；修复前的 36 小时日志不能作为方案 B 现场通过证据。

风险：5 秒退出上限遇到极端磁盘阻塞时仍可能丢失尾部少量日志；应急日志也依赖程序目录可写。兼容性：未修改 DLL ABI、HTTP/终端协议、配置、回调或第三方调用方式。

## MJPEG 16 小时真实硬件长稳收尾复核（2026-07-22）

- [x] 仅使用 `scripts/stress_results/handle_release_mjpeg_scheme_b_16hour_real_restart_20260718_1750`；人工中断旧轮未混入结论。
- [x] 正式目录有 `calls`、`cycles`、`metrics` CSV，无 `summary`/`callbacks` CSV；本轮只运行 12.69 分钟有效片段，未完成 960 分钟。
- [x] 有效片段 152 次切换、0 失败；终端 1/2 各 76 次成功；P50/P95/P99/Max 为 3/19/71/72 ms（nearest-rank）。
- [x] 停机后原始 `cycles` 尾段为 704 成功、546 失败，`calls` 为 576 成功、419 失败；测试宿主继续运行造成的无效尾段不作为长稳结论。
- [x] 预热后可用 Proxy 资源样本仅约 8 分钟：HandleCount 668～687、斜率 `+13.5226/h`、`R²=0.0128`；线程 30～35；PrivateMemory 53.04～54.98 MB。后半 8 小时与最后 4 小时无数据，不能判定长期趋势。
- [x] 测试窗口日志 warning/error、MJPEG pause/stop timeout、流恢复失败均为 0；VLC/RTSP 无实际预览记录（VLC 仅测试前预热，RTSP 为 0）。PID 47668 已不存在，未关闭任何进程。
- [ ] 先修复测试宿主在 Proxy 退出/关键切换失败后的联动停止、结果封存与 summary 生成。
- [ ] 在独占、不重启、不人工中断条件下重新执行完整 960 分钟后，再决定是否需要生产 MJPEG 代码修改。

结论：本轮不足以作为 16 小时长稳通过证据，也不足以支持继续修改生产句柄释放逻辑；优先处理测试宿主收尾链路。兼容性：本次仅更新文档，未修改 DLL ABI、HTTP/终端协议、配置、回调或第三方调用。

## Proxy UI 预览区比例与日志布局方案 B（2026-07-24）

- [x] 修改前实时获取 Gitee 远程引用；`release/1.2.6` 的 HEAD、`origin/release/1.2.6` 和 `FETCH_HEAD` 均为 `ecf6c93b`，已提交版本确认已上传。
- [x] 保留工作区已有 `PROGRESS.md`、`todo.md` 长稳复核记录及构建/压测产物，不清理、不覆盖。
- [x] 顶部运行信息、硬件健康检测和三组操作卡片完成垂直紧凑化，为预览区释放高度；健康检测刷新按钮仍保留至少 40px 实际高度。
- [x] 预览区改为按可用宽高居中的 3×2 响应式网格，6 个卡片固定 16:9，并使用独立横纵间距。
- [x] 预览区与日志区改为横向 `SplitContainer`；日志默认占可用内容区 22%，支持拖拽和折叠/展开。
- [x] 摄像头、指纹、虹膜使用 `Contain`，黑边补齐但不拉伸；三路车牌使用 `Cover`。
- [x] VLC 仅在现有专用线程内每 250ms 检查宿主尺寸并刷新布局，避免 UI 线程跨线程操作播放器。
- [x] `Release|x64` Proxy 独立输出编译通过：0 warning、0 error；按要求未执行 x86。
- [x] x64 Preview/UI 定向测试 24/24 通过；x64 非 Integration 回归 106/106 通过。
- [x] 修复健康检测卡片文字挤压：面板高度由 112px 调整为 132px，设备名称和状态说明分别在各自行内垂直居中。
- [x] 将终端请求失败提示统一改为“终端连接失败”，刷新按钮左侧同步显示“检测失败 · 终端连接失败或超时”。
- [x] 健康检测/终端检测/主窗体布局定向测试 15/15、x64 非 Integration 回归 107/107 通过；本轮未执行 x86。
- [ ] 真实视频验证摄像头/指纹/虹膜的 Contain 黑边、车牌 Cover 裁剪及拖拽日志时的实时刷新效果。
- [ ] 在 100%/150%/200% DPI、1920×1080 和 2560×1440 下进行人工视觉回归。

兼容性：未修改 DLL ABI、导出函数、调用约定、HTTP/终端协议、回调、配置结构或第三方 HWND 参数；新增内容仅涉及 Proxy 内部 UI 和预览绘制。回退时撤销本节对应的 MainForm、HardwareHealthPanel、Preview 内部布局代码及测试即可。

## 全部预览统一拉伸填满 HWND（2026-07-29）

- [x] 按方案 A 将摄像头、指纹、虹膜、车牌 CJ、车牌 RJ2、车牌 RJ3 全部统一为 `Stretch`。
- [x] HTTP MJPEG 画面拉伸到渲染子窗口完整客户区，并移除每帧绘制前的 `Clear(Black)`，避免清黑与绘制之间出现可见闪屏。
- [x] VLC 子窗口拉伸到宿主 HWND 完整客户区；VLC 直接绑定调用方 HWND 时使用宿主宽高比，不移动调用方窗口。
- [x] VLC 尺寸缓存提前到直接绑定分支之前，宿主尺寸未变化时不再每 250ms 重复设置缩放和宽高比。
- [x] `Release|x64` 隔离编译：0 error；首次构建仅有 1 条 NuGet 漏洞源不可达的 `NU1900` 环境警告。
- [x] `Release|x86` 隔离编译：0 warning、0 error。
- [x] x64/x86 预览布局与 MJPEG worker 定向测试各 7/7 通过。
- [x] x64/x86 非 Integration 回归各 109/109 通过。
- [ ] 真实硬件复核六类预览均完整覆盖第三方及本地 HWND，并连续观察至少 10 分钟确认无黑屏闪烁。
- [ ] 复核 100%/150%/200% DPI、拖拽日志分隔条、折叠/展开日志以及终端切换后的预览尺寸。

兼容性：未修改 DLL ABI、导出函数、调用约定、HTTP/终端协议、回调、配置结构或第三方 HWND 参数。行为变化仅为所有预览统一拉伸，因此非 16:9 视频源会产生比例变形。回退时恢复 `PreviewLayoutMath`、`PreviewManager`、`MjpegPreviewController`、`VlcPreviewController`、`VlcPreviewPlayer` 及对应测试中的本节差异。

## MJPEG 30fps 节拍补偿与 v1.2.9 版本标识（2026-07-29）

- [x] 实时查询 Gitee 远程引用，确认最高正式标签为 `v1.2.8`，不存在 `v1.2.9` 标签或 `release/1.2.9` 分支。
- [x] 保持目标渲染间隔 `33ms`，将固定“本帧处理完成后再休眠33ms”改为“33ms减去本帧实际处理耗时”。
- [x] 本帧处理超过33ms时不再额外固定等待，仅主动让出一次线程时间片，避免无界忙等。
- [x] 增加 MJPEG 节拍计算边界测试，覆盖正常补偿、零耗时、刚好超时和超过目标间隔。
- [x] Proxy Assembly/File Version 设置为 `1.2.9.0`，Product/Informational Version 设置为 `1.2.9`。
- [x] 主窗口标题、页面标题和程序启动日志显示 `v1.2.9`。
- [x] x64/x86 Release 隔离编译均为 0 warning、0 error；两端产物 FileVersion=`1.2.9.0`、ProductVersion=`1.2.9`。
- [x] x64/x86 MJPEG、版本和预览布局定向测试各 9/9 通过。
- [x] x64/x86 非 Integration 回归各 111/111 通过。
- [ ] 真实摄像头连续观察流畅度、CPU/GDI 占用和画面延迟，确认源端帧率足以达到接近30fps。
- [ ] 正式部署前在目标目录核对 EXE 文件属性、窗口标题和启动日志版本号一致。

兼容性：未改变 DLL ABI、导出函数、调用约定、HTTP/终端协议、回调、配置、第三方 HWND、MJPEG 请求频率或目标最大帧率。预期实际显示帧率更接近30fps，CPU/GDI 占用可能小幅上升。回退时恢复 `MjpegPreviewController`、`AssemblyInfo`、`MainForm` 和对应测试中的本节差异。

## Proxy/DLL 长稳生命周期加固方案 A（2026-08-03）

- [x] `IrisPreviewRestoreWorker` 改为按需启动、可显式停止/重启，`ReleaseSdk` 在删除共享 `HttpClient` 前先等待 Worker 退出。
- [x] 移除 Worker 在 DLL 卸载静态析构期间等待线程的 loader-lock 风险。
- [x] `TerminalHealthChecker` 改为可取消、可等待 Task，停止后不再访问 `TerminalClient` 或发送 UI 通知。
- [x] 健康检测器先于 `TerminalClient` 释放；新增停止幂等测试。
- [x] `TransportLayer` 使用 Handler 快照排空，待 Handler 完成后再释放 `SemaphoreSlim`。
- [x] `ProxyRuntime` 显式观察 Transport 停止异常，关键异常写入 `ERROR` 级别和完整堆栈，UI 保留精简摘要。
- [x] Native DLL `Release|Win32` 编译：0 warning、0 error，`MACHINE:X86`。
- [x] Proxy + Tests `Release|x64` 编译：0 warning、0 error；本轮未编译 x86 Proxy。
- [x] x64 非 Integration 112/112、Mock Integration 10/10 通过。
- [ ] 修改后使用 Delphi 7 Demo 验证重复 Init/Release、终端切换后立即 Release 和多日真实设备长稳。

兼容性：DLL 导出、`__stdcall`/C ABI、参数、结构体、错误码、回调、HTTP/终端协议和配置均未改变；性能热路径未改变。详细风险、验证与回退见 `demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`。

## 第三方操作指南编写记录（2026-08-07）

### 当前阶段

文档初稿已完成，待项目负责人确认交付对象、现场配置、权限边界并补充真实截图和硬件验证。

### 已完成内容

- [x] 核对 C# Proxy 主窗口、服务与通道、业务操作、预览、健康检测和日志区域。
- [x] 核对 Proxy 启动、单实例、自动启动服务、托盘和正常退出逻辑。
- [x] 核对终端切换、流程开始/结束、抓拍、OCR、IC 卡、虹膜和授权测试流程。
- [x] 核对默认 Proxy 配置、保存目录、日志目录和主要监听端口。
- [x] 核对 Proxy 与终端、DLL、第三方程序之间的职责边界。
- [x] 生成 `docs/第三方操作指南.md`。
- [x] 生成 `docs/第三方操作指南_待确认项.md`。
- [x] 生成 `docs/功能按钮清单.md`。
- [x] 生成 `docs/功能边界清单.md`。
- [x] 现有业务代码和程序逻辑未修改。

### 待补充截图

- [ ] 图 1：程序主界面，标注顶部状态、服务与通道、业务操作、预览、健康检测和日志。
- [ ] 图 2：终端选择区域，标注左通道、右通道和当前终端。
- [ ] 图 3：预览操作区域，标注设备下拉框、开始/停止预览和六类预览窗口。
- [ ] 图 4：抓拍或识别结果区域，使用脱敏测试数据。
- [ ] 图 5：硬件健康检测和日志区域，遮盖敏感信息。

当前工作环境未取得可直接作为交付证据的可靠真实运行截图；文档使用明确占位符，未使用虚构界面图片。

### 待确认信息

- [ ] 正式交付的操作程序是 C# Proxy、C# ThirdParty Demo、Delphi Demo 还是其他程序。
- [ ] 正式发布版本、Proxy/DLL 位数组合和最低 Windows 版本。
- [ ] 正式监听端口、终端 IP、终端左右通道对应关系和防火墙规则。
- [ ] 是否需要管理员权限及正式启动/关闭顺序。
- [ ] 开始流程是否是所有业务操作的强制前置条件。
- [ ] `授权测试` 是否仅限测试环境，是否允许连接生产终端。
- [ ] 三路车牌预览的正式配置和现场验收结果。
- [ ] 识别数据、日志和图片的保留、备份、删除及访问权限。
- [ ] 操作人员、技术维护人员和管理员的职责边界。
- [ ] Windows 10/11、x86/x64、高 DPI 和真实设备长稳测试结果。

### 高风险事项

1. `授权测试` 使用程序内置测试参数，容易被误解为正式授权入口。
2. 终端切换会改变后续业务目标，切换期间旧请求或旧回调可能被保护逻辑丢弃。
3. 界面显示车牌预览资源，但默认配置未配置三路车牌地址，可能出现按钮可见而功能不可用。
4. 日志和结果文件可能包含敏感身份信息，导出和反馈前必须脱敏。

### 下一步计划

- [ ] 由项目负责人确认待确认项并修正文档中的现场参数。
- [ ] 使用正式发布包在真实终端环境完成截图和基本操作回归。
- [ ] 验证正常退出、异常关闭、终端断开/重连、预览黑屏、请求超时和重复点击场景。
- [ ] 确认文档版本与交付包版本一致后再对外发布。

### 回退方式

本次仅新增文档并追加本节记录，不涉及业务代码、DLL ABI、HTTP/终端协议或配置结构。若不采用本次文档，删除本节及 `docs/` 下本次生成的四个文件即可，不影响程序运行。

## 车牌预览终端切换保持与第三方 HWND 铺满（方案 A，2026-08-11）

- [x] 终端切换前仅停止 `TerminalBound=true` 的摄像头、指纹和虹膜预览。
- [x] CJ、RJ2、RJ3 车牌会话不再被终端切换误停，保持原 RTSP、会话和第三方 HWND。
- [x] 车牌 VLC 直接绑定第三方 HWND 时使用宿主客户区宽高比，画面拉伸铺满且不移动调用方窗口。
- [x] VLC 播放线程每 250ms 跟随宿主客户区尺寸，兼容第三方窗口缩放和布局变化。
- [x] 增加终端绑定会话筛选、直接渲染宽高比及刷新周期定向测试。
- [x] Proxy + Tests `Release|x64` 编译通过，0 error、1 条 NuGet 漏洞源不可达的 `NU1900` 环境警告；x64 非 Integration 回归 110/110 通过。
- [x] Proxy + Tests `Release|x86` 编译通过，0 error、1 条 NuGet 漏洞源不可达的 `NU1900` 环境警告；x86 非 Integration 回归 110/110 通过。
- [ ] 使用真实车牌相机和第三方 HWND 验证终端 1/2 连续切换、窗口缩放及 100%/150%/200% DPI。
- [ ] 连续预览至少 2 小时，观察 VLC 线程、句柄、CPU 和内存是否稳定。

兼容性：未改变 DLL ABI、`__stdcall`、参数、错误码、回调、HTTP/终端协议、配置或第三方调用方式。车牌画面改为无黑边拉伸铺满，源比例与 HWND 比例不一致时会产生比例变形。回退时恢复本节对应的 `PreviewManager`、`SwitchCoordinator`、`VlcPreviewPlayer`、`VlcPreviewController` 及预览测试差异。

## DeviceMode 设备能力模式（2026-08-12）

- [x] 共用 `HZCYKJTHardWare.json` 顶层增加 `device_mode`，默认值为 `1`。
- [x] 配置缺失、非法或损坏时记录日志并回退 Mode 1，程序继续启动。
- [x] 增加统一 `DeviceCapabilityManager`；Mode 1 开放全部能力，Mode 2 仅开放 `PlateRJ2`、`PlateRJ3`。
- [x] Proxy UI、HTTP 命令分发、直接业务入口、健康检测、回调监听和工作队列共用能力模型。
- [x] Mode 2 UI 仅保留服务/配置/日志/资源监控与 RJ2/RJ3 两路预览，不启动非镜头工作线程、终端健康轮询和终端回调监听。
- [x] 不支持请求快速返回 `code=not_supported`，DLL 对外仍返回公共失败值 `0`；内部映射到 `HZCYKJTHardWare_RET_UNSUPPORTED (-18)`。
- [x] OCR、NFC、虹膜、授权误调用通过现有事件回调发送 `status=-18`、`error_code=not_supported`，不创建业务会话、不入队、不重试。
- [x] 不支持调用 WARN 按 `(Interface, Capability)` 60 秒限频，并在下一条日志记录抑制次数。
- [x] Native DLL `Release|Win32`：0 warning、0 error；导出 24 项与 v1.3.1 `.def` 完全一致。
- [x] Proxy + Tests `Release|x86`、`Release|x64`：0 error（仅 NuGet 漏洞源不可达 `NU1900` 环境警告）。
- [x] x86、x64 非 Integration 回归各 116/116 通过；`git diff --check` 通过。
- [ ] 真实 RJ2/RJ3 相机独立启停、第三方 Delphi7 误调用回调及无终端流量现场验证。
- [ ] RJ2/RJ3 抓拍接口当前不存在，按既定范围不新增，后续单独设计并验收。

回退：恢复本节涉及的配置、Capability、Proxy 分发/UI/队列与 Native 内部判断代码即可；未修改 `.def`、公开头文件、DLL ABI、部署位数或第三方调用方式，不需要 Delphi7 版本适配。

## 主窗口版本号展示调整（2026-08-12）

- [x] Windows 边框标题仅保留“五合一车道硬件平台”，不再附带版本号。
- [x] 主界面左上产品名称下方增加小号灰色 `v1.3.1`，与主标题垂直居中组合，不增加顶部横向占用。
- [x] 增加窗口标题、主标题、版本标签、字体层级和父容器关系自动化断言。
- [x] Proxy + Tests `Release|x86` 编译通过，0 error；定向 UI 测试 3/3 通过。
- [ ] Windows 10/11、100%/150%/200% DPI 实机视觉检查待执行。

兼容性：仅调整 WinForms 标题展示，不修改程序集版本、DLL ABI、配置、协议或业务行为。回退 `AssemblyInfo.cs`、`MainForm.cs` 和对应 UI 测试差异即可。

## 统一 JSON 配置有效性清理（2026-08-12）

- [x] 审计根目录统一 `HZCYKJTHardWare.json` 在 Native DLL 与 C# Proxy 中的解析和实际使用点。
- [x] 从统一 JSON 删除仅被解析、未参与当前业务的历史字段：`terminal.mode`、`terminal.check_on_init`、`terminal.fixed_terminals`、`callback_server.auto_bind_lan_ip`、`preview.renderer`、`preview.auto_reconnect`、`preview.stop_preview_on_end_process`。
- [x] 保留旧字段解析代码，兼容既有部署配置，不因多余字段导致启动失败。
- [x] 新增 `device_mode_names`，Mode 2 顶部名称不再由 UI 代码写死。
- [x] `terminal.default_index` 和 `auto_subnet_devices[].name` 已接入 Proxy 默认终端、日志及顶部终端名称。
- [x] 确认工程目录 `HZCYKJTHardWare.Proxy.json` 为历史样例，不参与当前构建和运行加载。
- [x] Proxy + Tests `Release|x86`、`Release|x64` 编译成功，0 error；两套非 Integration 回归各 118/118 通过。

兼容性：未修改 DLL 导出、`__stdcall`、参数、结构体、错误码、回调或 HTTP/终端协议。回退时恢复统一 JSON、`AppConfig.cs`、`TerminalManager.cs`、`MainForm.cs` 和配置测试的本节差异即可。

## C# 第三方 Demo 接口覆盖同步（2026-08-12）

- [x] 对照 `.def` 审计 C# Demo：24/24 导出均已 P/Invoke 声明并具备界面调用路径。
- [x] 新增虹膜、CJ、RJ2、RJ3 预览启停入口，六路预览改为统一下拉选择。
- [x] Demo 读取共用 `device_mode` 与 `device_mode_names`；Mode 2 仅显示 SDK 服务和 RJ2/RJ3 预览。
- [x] 配置缺失、非法或损坏时记录警告并回退 Mode 1。
- [x] 回调日志补充事件名称、`request_id`、`error_code/code`，明确显示 `status=-18` 不支持结果。
- [x] C# Demo `Release|x86` 编译通过，0 warning、0 error；静态导出覆盖 24/24。
- [ ] Mode 1 六路真实预览、Mode 2 RJ2/RJ3 独立启停和配置异常界面待现场验证。

兼容性：未修改 Native DLL、`.def`、公开头文件、回调签名或 Proxy 协议。回退 C# Demo 的 `MainForm.cs`、`MainForm.Designer.cs` 和 README 本节差异即可。

## C# Demo Mode 2 高 DPI 顶部布局修复（2026-08-12）

- [x] 确认重叠原因为 Mode 2 在 `AutoScaleMode.Dpi` 下使用运行时固定坐标和固定高度。
- [x] 使用单行、自适应、可横向滚动的 `FlowLayoutPanel` 承载 Mode 2 工具栏。
- [x] 删除 Mode 2 的 `panelTop.Height=42` 以及各控件固定 `Location`。
- [x] Mode 1 原布局和全部 DLL 调用逻辑保持不变。
- [x] C# Demo `Release|x86` 编译通过，0 warning、0 error。
- [ ] Windows 100%/150%/200% DPI 视觉验证待执行。

回退：恢复 `MainForm.cs` 中 `ConfigureMode2Toolbar` 相关差异即可；不涉及 DLL、Proxy、JSON 格式或第三方接口。

## C# Proxy 与 Demo 日志中文化（2026-08-12）

- [x] 汉化 Proxy 配置、能力检查、请求登记、队列、预览、切换协调、运行时、任务跟踪和网络检测中的英文叙述日志。
- [x] 汉化 C# Demo 的设备模式、回调摘要、保存结果和界面日志标点。
- [x] 保留 `DeviceMode`、`Capabilities`、`request_id`、`resource_type`、错误码、URL、HTTP/MJPEG/VLC/SDK/DLL/HWND 等专有名词与协议字段。
- [x] 纯英文运行日志静态扫描：0 行。
- [x] Proxy、Tests、Demo `Release|x86` 编译通过；Proxy 和 Demo 0 warning/0 error，Tests 仅有 NuGet 源不可达 `NU1900`。
- [x] Proxy + Tests `Release|x64` 编译通过，0 error，仅有 `NU1900`。
- [x] x86、x64 非 Integration 回归各 118/118 通过。
- [ ] 现场运行日志可读性和异常日志信息完整性待人工复核。

兼容性：仅修改日志文案，不修改 DLL ABI、JSON/HTTP 协议字段、错误码、回调内容或设备行为。回退本节涉及的 C# 日志字符串与文档差异即可。

- [x] 日志分类名称按现场习惯调整：`[能力检查]` 改为 `[硬件检测]`，`[切换协调]` 改为 `[终端切换]`。

## IC 卡第三方回调配置开关（2026-08-31）

### 本次修改内容

1. 统一 JSON 新增 `enable_ic_card_callback`，默认值为 `true`；同时兼容现场可能使用的 `EnableIcCardCallback` 键。
2. 配置仅在 Proxy 启动时读取，缺失、`null`、空字符串、非法类型或配置文件读取失败时回退为 `true`，不因该配置导致 Proxy 启动失败。
3. 沿现有链路 `ProxyServer.HandleTerminalCallback` → `TerminalCallbackHandler.HandleAsync` → `HandleNfcCardAsync` 审查后，在 IC 卡第三方回调出口、`DllCallbackSender.SendNfcResult` 之前增加判断。
4. 关闭时仍保留终端数据接收、解析、路由和内部日志；一次性请求标记完成，流程型事件只保留现有去重消费记录，不建立待补发队列。
5. 增加启动状态日志；关闭期间的 IC 卡跳过日志按 5 秒限频，避免终端高频读卡刷屏。

### 涉及文件

- `HZCYKJTHardWare.json`：新增配置示例。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Infrastructure/AppConfig.cs`：读取、默认回退和启动日志。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/TerminalCallbackHandler.cs`：第三方回调出口拦截、请求收尾和限频日志。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Infrastructure/IcCardCallbackConfigurationTests.cs`：配置值边界测试。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Core/TerminalCallbackRoutingTests.cs`：关闭不发送、重新启用不补发历史请求的回归测试；同时移除测试对默认终端序号的硬编码依赖。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Core/SwitchCoordinatorTests.cs`：移除测试对默认终端序号的硬编码依赖。

### 兼容性与生效逻辑

- `enable_ic_card_callback=true` 或配置缺失/异常：保持原有 IC 卡第三方回调行为。
- `enable_ic_card_callback=false`：Proxy 继续接收并处理终端 IC 卡数据，但不调用第三方 IC 卡回调，不修改 DLL ABI，也不影响 OCR、授权、人脸、指纹、预览和终端切换。
- 当前配置模型不支持运行时热更新；修改配置后重启 Proxy 生效，进程内不保存关闭期间的历史数据用于补发。

### 验证状态

- [x] Proxy + Tests `Release|x86` 编译：0 错误；仅有 NuGet 漏洞源不可达的 `NU1900` 环境警告。
- [x] IC 卡配置与回调定向测试：6/6 通过。
- [x] x86 非 Integration 回归：136/137 通过。
- [ ] 剩余版本号测试 `ProductVersion_IsEmbeddedAndVisibleInMainWindow` 待单独处理：测试期望 `1.3.1.0`，当前程序集为 `1.3.5.0`，与本次修改无关。
- [ ] 真实终端刷卡、第三方 DLL 回调和高频读卡现场验证待执行。

## C# Demo 兼容带注释统一 JSON（2026-08-31）

- [x] 确认 C# Demo 使用 `JavaScriptSerializer`，严格 JSON 解析不接受统一配置中的 `//` 注释。
- [x] `LoadDeviceModeSettings` 增加安全注释扫描，支持 `//` 和 `/* */`，不会误删字符串中的 `http://` 等内容。
- [x] 不新增 JSON 库或 DLL 运行时依赖，DLL 调用接口和车牌镜头参数 `1=CJ`、`2=RJ2`、`3=RJ3` 不变。
- [x] C# Demo `Release|x86` 编译：0 警告、0 错误。
- [x] 使用 x86 .NET Framework 宿主解析实际输出配置：`device_mode=1`、`enable_ic_card_callback=false`，注释剥离后可正常读取。
- [ ] Windows 界面实际启动和现场 DLL 联调待验证。

兼容性：仅修复 C# Demo 对 JSONC 配置的读取，不修改统一配置字段含义、Native DLL ABI、Proxy 行为或第三方调用方式。

## 阶段3：日志体系优化（新增功能，不增加版本号，2026-08-31）

### 当前阶段

阶段3 L1-L5 已完成代码修改，现场硬件、第三方调用和长稳验证待执行。

### 已完成内容

- [x] L1：Native DLL、C# Proxy 增加统一日志模块、级别和业务上下文字段；业务入口/出口补充 `Operation`、`RequestId`、`Result`、`ErrorCode`、`DurationMs` 等上下文。
- [x] L2：增加 JSON 标量摘要、Base64/原始正文过滤、URL 用户信息和凭据 query 脱敏；非法正文仅记录长度及 `RequestId`，不回退打印完整正文。
- [x] L3：调整 HTTP、队列、路由、VLC/MJPEG 内部步骤为 DEBUG；保留流程、预览状态、最终成功/失败和永久故障的 INFO/WARN/ERROR 语义。
- [x] L4：增加 60 秒限频和窗口汇总，覆盖 HTTP 故障、终端离线、MJPEG/VLC 恢复、回调忙/无效/重复、事件队列以及日志写入异常。
- [x] L5：Native DLL 与 C# Proxy 均使用有界异步队列、ERROR 保留队列、应急输出、按天/按大小滚动、保留期/总容量治理、低磁盘 DEBUG 抑制和运行指标；当前文件也计入容量治理。
- [x] 修改前已提交并推送基线：`89435ae5 feat: 新增功能阶段3实施前版本基线（不增加版本号)`，分支为 `feature/overlay-container-preview`。

### 涉及文件

- Native 日志与调用点：`src/logger.h`、`src/logger.cpp`、`src/callback_server.cpp`、`src/delphi_proxy.cpp`、`src/event_dispatcher.cpp`、`src/exports.cpp`、`src/http_client.cpp`、`src/libvlc_rtsp_renderer.cpp`、`src/preview_manager.cpp`、`src/request_session_manager.cpp`、`src/terminal_manager.cpp`、`src/terminal_status_checker.cpp`。
- C# Proxy 日志与调用点：`demo/CSharpProxy/HZCYKJTHardWare.Proxy/Infrastructure/Logger.cs`、`demo/CSharpProxy/HZCYKJTHardWare.Proxy/Infrastructure/LogRateLimiter.cs`、`demo/CSharpProxy/HZCYKJTHardWare.Proxy/Infrastructure/PingLogAggregator.cs`、预览、HTTP、请求队列、回调和服务文件。
- 测试：`demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Infrastructure/LoggerTests.cs`、`demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Infrastructure/PingLogAggregatorTests.cs`。

### 兼容性说明

- DLL ABI、导出函数名与参数、调用约定、结构体布局、回调签名和错误码未修改；公开头文件及 `.def` 未修改。
- HTTP/回调请求响应格式、第三方调用流程和设备协议未修改；仅改变日志级别、日志内容摘要和日志落盘策略。
- 不新增依赖；版本号保持修改前基线值，本次未增加版本号。
- 现有第三方业务载荷仍按原链路传输，日志不再记录完整 Base64/原始正文。

### 风险与注意事项

1. 日志目录不可写、主日志文件写入失败或低磁盘时将转入应急文件/`OutputDebugString`，ERROR 仍尽力保留；现场需确认临时目录权限和磁盘空间。
2. 同设备/接口/错误类别的重复故障在 60 秒内会被聚合，窗口结束记录 `Count`、`FirstTime`、`LastTime`、`LastError`；首次故障及恢复/最终故障仍即时记录。
3. 异步日志可能在进程异常终止时来不及完整落盘；需要结合 `log_pending`、`log_dropped_total`、`log_write_failures`、`log_last_flush_age_ms` 和当前文件大小观察。
4. 工作区中的 `_codex_build`、测试 `obj`、抓拍/压力输出、Office 临时文件和示例凭据文件为生成物或既有未跟踪内容，本次未纳入阶段3范围。

### 验证状态

- [x] Native `Release|Win32`（x86）编译：0 警告、0 错误，生成 `Release\HZCYKJTHardWare.dll`。
- [x] C# Proxy `Release|x86` 编译：0 警告、0 错误。
- [x] C# Tests `Release|x86` 编译：0 错误；存在既有 NuGet 漏洞源不可达警告 `NU1900`。
- [x] 阶段3定向 VSTest：11/11 通过，覆盖日志队列健康、级别分类、载荷/URL 脱敏、限频汇总和 Ping 聚合。
- [x] 完整 x86 VSTest：151 个，139 通过，12 失败。10 个 Proxy 集成测试因当前 VSTest/.NET 环境的 `System.Net.HttpListener` `PlatformNotSupportedException` 在类初始化阶段失败；`ProductVersion_IsEmbeddedAndVisibleInMainWindow` 仍为既有版本期望 `1.3.1.0`、实际 `1.3.5.0`；`ProcessCallback_AfterSwitchAtoBtoA_IsDeliveredAgain` 仍为既有路由测试失败，日志差异未改变其路由逻辑。
- [x] `git diff --check`：未发现空白错误；仅有 Git 关于 LF/CRLF 的提示。
- [x] 相对阶段3基线核对：公开头文件/`.def` 未变更，版本资源与 `AssemblyInfo.cs` 未新增变化。
- [ ] `dotnet test` 有返回码但当前测试项目未配置 `Microsoft.NET.Test.Sdk`，未形成有效测试执行，不能视为通过。
- [x] `dumpbin` 导出表：生成 DLL 导出 25 个符号，与现有 `.def` 导出集合一致。
- [ ] 第三方 Delphi Demo、Windows 10/11 x86 Release 真实设备调用待验证。
- [ ] 真实终端断开/重连、第三方 callback、低磁盘/不可写目录、长时间（2/24 小时）稳定性待验证。

### 下一步计划

- [ ] 在 Windows 10/11 x86 Release 环境执行真实硬件启停、抓拍/OCR/NFC/授权、预览恢复和第三方回调回归。
- [ ] 验证日志滚动、保留期、总容量、低磁盘应急输出和长稳指标。
- [ ] 单独处理或确认上述既有测试环境/版本号/路由测试问题，不将其混入阶段3日志改动。

### 回退方式

阶段3改动当前仍在本地、未形成新的提交；修改前基线 `89435ae5` 已推送到 Gitee。需要回退时，仅按阶段3涉及文件相对 `89435ae5` 的差异逐文件恢复，保留工作区其他用户改动及生成物；不修改版本号、不执行宽范围重置。

## 阶段3 Task R1：MJPEG Recovery Episode / RenderTarget Failure 修复（2026-09-01）

### 完成状态

- [x] 确认 `PublishFrame()` 在真实 `DrawImage()` 前设置运行状态，原恢复流程仅凭 `replacement.IsRunning` 宣布恢复，且绘制失败会被重新送入 Stream Recovery。
- [x] 增加 `StreamFailure`、`DecodeFailure`、`RenderTargetFailure` 三类 MJPEG 故障区分。
- [x] 增加当前媒体代次的真实绘制就绪信号；恢复只有在至少一帧成功绘制到当前 HWND 后才提交新的 Session generation 并记录恢复成功。
- [x] RenderTarget 临时失败使用有限退避重绘；`GetClientRect` 客户区为 0、短暂失败或 `GetDC` 失败不重建 MJPEG 流。
- [x] HWND 销毁/宿主身份失效时结束外部预览 Session，复用既有最终失败通知路径；Proxy 不销毁第三方 HWND。
- [x] 增加按 Session Key 维持的轻量 `RecoveryEpisode`，恢复尝试不再因短生命周期 generation 改变而清零；稳定绘制成功后才清理 Episode。

### 涉及文件

- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Preview/MjpegPreviewController.cs`：故障分类、绘制就绪、RenderTarget 诊断与退避。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Preview/PreviewManager.cs`：RecoveryEpisode、稳定恢复判定、RenderTarget 最终失败处理。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Preview/MjpegWorkerReuseTests.cs`：真实绘制就绪、无效 HWND、外部 Session 结束、零客户区退避测试。

### 兼容性、风险与回退

- DLL ABI、导出函数、调用约定、错误码、终端 HTTP、callback 协议、第三方 HWND 所有权均未修改；`IPreviewController` 公共成员未修改。
- 风险集中在渲染线程、恢复线程和 HWND 销毁的竞态，以及延迟释放期间的 Worker 身份校验；均通过 Session generation、Player 身份和既有释放路径约束。
- 回退方式：回退 Task R1 独立提交即可；保留当前阶段3基线、用户已有方案文档改动和其他生成物，不执行宽范围重置。

### 验证状态

- [x] Proxy `Release|x86` 编译：0 警告、0 错误。
- [x] Tests `Release|x86` 编译：0 错误；仅有既有 NuGet 漏洞源不可达 `NU1900` 环境警告。
- [x] Native DLL `Release|Win32/x86` 编译：0 警告、0 错误。
- [x] R1 定向 VSTest：9/9 通过，覆盖 MJPEG Worker 复用、真实绘制就绪、RenderTarget 销毁、PreviewManager 外部 Session 清理、零客户区退避、窗口身份和恢复策略。
- [ ] DLL 导出表、Delphi7 第三方调用、真实终端断流/恢复、Windows 10/11 实机及长稳压测：待验证。
- [ ] x64 Release 编译和测试：在 Task L6 完成后统一执行最终验收，当前待验证。

### 下一步

- [ ] 将 R1 作为独立提交推送到 `feature/overlay-container-preview`。
- [ ] R1 独立验证完成后执行 Task L6 日志分级、中文化、限频及 INFO 收口。
