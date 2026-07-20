# 项目进度记录

# release/1.2.6 P0 架构与长稳修复（2026-07-10）

## 当前阶段

- [x] 修复前基线已提交并推送到 `origin/release/1.2.6`，提交 `283883a9`。
- [x] P0-1：DLL `/terminal/switch` 仅在 Proxy 实际提交终端路由后返回成功。
- [x] P0-2：不可逆释放失败后不再恢复为 Running，进入 `Faulted` 并拒绝继续调用。
- [x] P0-3：事件终端信息优先使用请求 Session 快照，无 Session 时加锁读取全局上下文。
- [x] P0-5：人脸、指纹固定文件改为同目录临时写入和 `MoveFileEx` 原子替换。
- [x] P0-6：DLL 回调默认绑定 loopback，Proxy 严格校验终端回调来源 IP。
- [x] P0-7：日志增加保留天数、总容量、磁盘阈值和批量刷新；按用户要求不修改日志业务字段、不做脱敏。
- [x] P0-8：Native/C# VLC 改用绝对路径 `LoadLibraryEx`，不再修改进程级 DLL 搜索路径。
- [ ] P0-9：真实终端是否要求 `/process/end` 待协议确认；当前不擅自改变终端行为。
- [ ] P0-10：真实双终端 24～72 小时资源长稳测试待现场执行。

## 本次修改内容

1. `/terminal/switch` 复用 `SwitchCoordinator.SwitchToAsync()`，等待预览停止和 `TerminalManager.SwitchTo()` 完成；预览恢复仍在后台执行。
2. SDK 生命周期新增 `Faulted`。只有尚未进入不可逆清理的失败才能恢复 Running；CallbackServer 退出超时后完成其余清理并进入故障态。
3. `EventDispatcher` 根据 `request_id` 解析请求创建时的终端快照，避免切换过程中并发读取 `std::string` 和跨终端标记。
4. `FileSaver` 使用同目录临时文件、`Flush(true)` 和 `MoveFileEx(REPLACE_EXISTING | WRITE_THROUGH)` 发布完整图片。
5. `callback_server.listen_any` 默认改为 `false`；无法映射到两台配置终端的回调来源直接拒绝。
6. DLL 和 Proxy 日志新增兼容配置：`retention_days`、`max_total_size_mb`、`disk_warning_free_mb`、`flush_interval_ms`、`flush_batch_size`。
7. Native 和 C# VLC 使用 `LOAD_WITH_ALTERED_SEARCH_PATH` 加载绝对路径，失败分支释放已加载模块。

## 涉及文件

- `src/exports.cpp`、`src/sdk_runtime.*`：同步切换配合与 SDK 生命周期。
- `src/event_dispatcher.cpp`：终端 Session 快照和线程安全读取。
- `src/config_manager.*`、`src/logger.*`、`HZCYKJTHardWare.json`：监听及日志治理配置。
- `src/libvlc_rtsp_renderer.cpp`：Native VLC 安全加载。
- `Server/DllCommandHandler.cs`、`Server/TerminalCallbackHandler.cs`：同步切换和来源 IP 校验。
- `Storage/FileSaver.cs`：图片原子替换。
- `Infrastructure/AppConfig.cs`、`Infrastructure/Logger.cs`：Proxy 日志治理。
- `Preview/VlcPreviewPlayer.cs`：C# VLC 安全加载。
- `HZCYKJTHardWare.Proxy.Tests`：切换、回调来源和文件保存回归测试。

## 兼容性说明

- DLL 导出函数名、参数顺序、`__stdcall`、结构体、错误码和回调 JSON：未修改。
- C# Demo UTF-8 封送和 Delphi 示例：未修改。
- `/terminal/switch` 响应 JSON 不变，但成功响应改为表示“路由已提交”，调用耗时会反映真实切换耗时。
- 日志内容不脱敏；只改变保留、容量和刷新策略。
- 新日志配置均有默认值，旧配置文件仍可读取。

## 风险与注意事项

1. DLL 回调仅绑定 loopback，符合当前 DLL 与 Proxy 同机部署；如果未来改为跨机器部署，需要显式恢复 LAN 监听配置。
2. Proxy 终端回调要求来源 IP 与终端配置一致；DHCP、NAT 或错误网卡配置会被明确拒绝。
3. INFO 日志最多延后约 500ms 或 50 条刷新；Error 立即刷新。异常断电可能丢失最后少量普通日志。
4. SDK 进入 `Faulted` 后必须重启宿主进程，这是为了避免半释放状态继续接单。
5. VLC 编译已通过，但 plugins 加载、实际 RTSP 出画面和重复启停仍需现场验证。

## 验证状态

- [x] C# Proxy + Tests `Release|net46`：0 警告、0 错误。
- [x] DLL `Release|Win32`：0 警告、0 错误。
- [x] DLL 产物：PE32/x86，24 个导出函数名称和 `__stdcall` 装饰保持不变。
- [x] 非 Integration 单元测试：75/75 通过。
- [x] 新增文件原子替换、无效 Base64 保留旧文件和 BMP 覆盖测试：通过。
- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [ ] 当前测试宿主的 7 项 `HttpListener` Integration：环境不支持，待正式 Windows 测试宿主执行。
- [ ] 真实双终端切换、VLC 预览、终端回调来源和 24～72 小时长稳：待现场验证。

## 下一步计划

- [ ] 获取真实终端协议，确认 `/process/end` 是否必须下发及幂等语义。
- [ ] 执行连续终端切换后立即 `StartProcess`、抓拍和回调联调。
- [ ] 采集 Private Bytes、GC、句柄、线程、TCP、日志目录和队列指标完成长稳门禁。

## 回退方式

- 完整回退基线：`release/1.2.6` 分支提交 `283883a9`。
- 分项回退时按本节“涉及文件”恢复对应模块；不得只回退 DLL 或只回退 Proxy 的终端切换语义。

# 试验性回退：StartProcess 切换等待串行逻辑（2026-07-09）

## 当前阶段

- [x] 按用户要求回退上次 `SwitchTerminal -> StartProcess` 顺序等待逻辑，用于验证切换慢是否来自等待/串行路径。
- [x] 保留第三方 DLL 导出函数、调用约定、参数、错误码和 HTTP JSON 协议不变。
- [x] 保留终端切换后的预览后台恢复改动，不回退人脸优先恢复日志。
- [ ] 真实第三方现场复测待执行。

## 本次修改内容

1. DLL `StartProcess` 检测到本地 `switch_pending` 时立即返回 busy，不再最多等待 15 秒。
2. DLL 转发 `/process/start` 不再把内部 HTTP 超时提升到 22 秒，恢复使用默认请求超时。
3. Proxy DLL 入口 `/process/start` 不再绕过切换中快速拒绝逻辑，切换中立即返回 `terminal_switching`。
4. Proxy 管理界面 `StartProcess` 不再等待切换完成，拿不到控制门禁或稳定终端路由时立即返回 `Busy`。

## 涉及文件

- `src/exports.cpp`：移除 `StartProcess` 切换等待和 22 秒超时兜底。
- `src/delphi_proxy.h`
- `src/delphi_proxy.cpp`
- `Server/DllCommandHandler.cs`
- `Server/Coordinator/BizOperationHandler.cs`
- `PROGRESS.md`

## 兼容性说明

- 外部接口：未改变。
- 第三方调用：函数名、参数、调用约定、错误码定义未改变。
- Proxy HTTP API：请求/响应格式未改变。
- 行为变化：`StartProcess` 在终端切换中由等待切换完成恢复为快速失败/忙。

## 验证状态

- [x] C# Proxy `Release|x86|net46` 隔离输出编译通过：0 warning，0 error。
- [x] DLL `Release|Win32` 编译通过：0 warning，0 error。
- [ ] 现场连续执行“切换终端后立即开始流程”：待验证。

## 回退方式

- 恢复 `StartProcess` 的切换等待函数、Proxy 入口等待逻辑和 `ProcessStart` 可选长超时参数。

# 方案 A：100% 缩放下硬件健康检测卡片显示不全修复（2026-07-09）

## 当前阶段

- [x] 已按用户要求在修改前提交当前版本，提交信息为 `1.2.8版本`。
- [x] 已确认问题属于 WinForms 管理界面 DPI/布局问题，不涉及 DLL 业务逻辑。
- [x] 已按 192 DPI 设计基准对健康检测 Header 右侧区域、刷新按钮和设备卡片固定列做最小宽度压缩。
- [x] 已补充 100% 缩放窗口宽度下的 UI 布局回归断言。
- [ ] 真实 Windows 100%、125%、150%、200% 缩放截图复核待现场执行。

## 本次修改内容

1. 新增 `HardwareHealthDpiLayout.ScaleFromDesignDpi()`，以 192 DPI 作为既有设计基准，在 100% 缩放下压缩固定列宽，同时保留可读最小值。
2. `HardwareHealthPanel` Header 右侧状态区、刷新按钮列和摘要/按钮间距改为按当前 DPI 计算，避免低 DPI 下固定宽度过大。
3. `DeviceHealthCard` 内部短码列、状态列、左右 Padding 和短码右侧间距改为 DPI 感知宽度，减少对中间设备名和说明文本的挤压。
4. UI 测试将摘要与刷新按钮间距从固定 12px 改为至少 8px，适配 100% 缩放紧凑布局。
5. 新增 `HardwareHealthPanel_DeviceCardsFitAtOneHundredPercentWindowWidth()`，验证 1180px 宽度下短码、状态和说明列保持基本可读。

## 涉及文件

- `UI/HardwareHealthPanel.cs`：健康检测面板 DPI 基准压缩和设备卡片固定列宽调整。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：100% 缩放宽度布局回归断言。
- `PROGRESS.md`：本次修复记录。

## 兼容性说明

- 外部接口：DLL 导出函数、调用约定、参数、结构体、错误码和回调签名未修改。
- 第三方调用：不影响第三方程序调用方式。
- Proxy HTTP API：未修改请求/响应格式。
- 配置文件和部署方式：未修改。
- 影响范围：仅 WinForms 后台服务管理界面的健康检测区域显示。

## 风险与注意事项

1. 本次没有修改 `MainForm.Designer.cs` 的 `AutoScaleDimensions = 192F, 192F`，避免牵连整个窗体缩放；若后续还有其他区域在 100% 下异常，需要单独处理。
2. 自动化测试只能验证控件边界和基本宽度，真实 Windows DPI 渲染效果仍需截图确认。
3. 如果现场窗口宽度低于约 1180px，五张卡片仍可能需要进一步改为两行布局。

## 验证状态

- [x] Proxy Debug `net46` 编译：通过，0 警告，0 错误。
- [x] Proxy Release `x86|net46` 编译：通过，0 警告，0 错误。
- [x] Tests Debug `net46` 编译：通过，0 警告，0 错误。
- [x] `dotnet test --no-restore`：退出码 0，但当前 SDK 未输出测试摘要。
- [x] `dotnet vstest`：发现并执行 22 个测试，摘要显示 22 通过、0 失败；测试宿主清理阶段崩溃导致命令退出码为 1。
- [ ] 真实 100% DPI 界面截图验证：待现场执行。
- [ ] 真实 200% DPI 回归截图验证：待现场执行。

## 回退方式

- 回退 `HardwareHealthPanel.cs` 中 `HardwareHealthDpiLayout`、Header 右侧宽度计算和 `DeviceHealthCard` 列宽计算即可恢复旧固定列宽。
- 回退 `MainFormLogRenderingTests.cs` 中新增的 100% 宽度测试和间距断言调整即可恢复旧测试约束。

# 方案 B：切换终端后开始流程顺序等待（2026-07-08）

## 当前阶段

- [x] 已在修改前提交当前版本，提交信息为 `1.2.7版本`。
- [x] DLL 和 C# Proxy 已改为允许 `SwitchTerminal` 后紧接 `StartProcess` 时按顺序等待切换完成。
- [x] DLL 导出函数、调用约定、参数、错误码、第三方请求格式和终端协议未修改。
- [x] 正式运行目录 EXE/DLL 已覆盖部署到 `bin/x86/Release/net46`。
- [ ] 真实第三方连续切换终端并立即开始流程的现场复测待执行。

## 本次修改内容

1. DLL `StartProcess` 在检测到终端切换中时，不再立即返回忙，而是最多等待 15 秒，切换完成后继续转发开始流程。
2. DLL 转发 `/process/start` 到 Proxy 时，将本次内部 HTTP 超时提升到至少 22 秒，覆盖 Proxy 等待切换和终端开始流程的耗时。
3. C# Proxy 的 DLL 命令入口 `/process/start` 绕过切换中的快速拒绝逻辑，最多等待 15 秒获取新终端路由后再执行。
4. C# Proxy 管理界面本机开始流程入口使用同一等待逻辑，避免 UI 入口与 DLL 入口行为不一致。
5. 其他 OCR、NFC、授权、抓拍、预览等接口仍保持终端切换中快速拒绝，不扩大排队等待范围。

## 涉及文件

- `src/exports.cpp`：`StartProcess` 切换等待和内部 Proxy 超时兜底。
- `src/delphi_proxy.h`、`src/delphi_proxy.cpp`：内部 `ProcessStart` 增加可选超时参数。
- `Server/DllCommandHandler.cs`：DLL `/process/start` 等待终端切换完成后再执行。
- `Server/Coordinator/BizOperationHandler.cs`：Proxy UI 开始流程入口同步等待策略。
- `PROGRESS.md`：记录本次方案 B 修改。

## 兼容性说明

- DLL 导出 ABI：未修改。
- 第三方调用参数、调用约定、错误码：未修改。
- Proxy HTTP 路由和请求/响应 JSON：未修改。
- 终端 HTTP 协议：未修改。
- 行为变化仅限 `StartProcess`：终端切换中由立即失败改为最多等待 15 秒后继续或按原忙语义失败。

## 风险与注意事项

1. 第三方线程调用 `StartProcess` 时，若刚好处于终端切换中，切换等待段最多约 15 秒；整体耗时还包括 Proxy/终端开始流程请求时间，DLL 内部 HTTP 超时下限为 22 秒。
2. 如果终端切换超过 15 秒仍未完成，`StartProcess` 仍会返回原有忙/切换中语义，不引入无限等待。
3. 仅解决“切换请求已发出但尚未完成时立即开始流程”的竞态；如果终端实际 HTTP 服务切换后仍不可用，仍需要按现有失败日志排查终端侧。

## 验证状态

- [x] `git diff --check`：通过，仅有 LF/CRLF 规范化提示。
- [x] C# Proxy `Release|x86|net46` 独立输出编译：通过，0 警告，0 错误。
- [x] C# Proxy `Release|x86|net46` 正式输出编译：通过，0 警告，0 错误。
- [x] C++ DLL `Release|Win32` 编译：通过，0 警告，0 错误。
- [ ] C# 测试结果记录：`dotnet test` 返回 0，但未输出测试摘要和 TRX 文件，需后续重新生成有效报告。
- [x] 正式运行目录覆盖部署：已更新 `HZCYKJTHardWare.Proxy.exe` 和 `HZCYKJTHardWare.dll`。
- [ ] 真实第三方现场复测：待执行。

## 下一步计划

- [ ] 使用第三方程序连续执行“切换终端 1/2 后立即开始流程”，确认不再因切换中返回开始流程失败。
- [ ] 补充切换超时、终端不可达、重复快速调用 `StartProcess` 的回归验证。

## 回退方式

- 回退上述 5 个代码文件中新增的 `StartProcess` 等待逻辑和内部超时参数，即可恢复终端切换中立即失败的旧行为；DLL 导出接口和部署配置无需回退。

# 授权回调 resource_type 日志识别后打印（2026-07-08）

## 当前阶段

- [x] 修复授权终端回调缺少 `resource_type` 时先打印空值的问题。
- [x] 缺少 `resource_type` 但带 `status=yes/no` 和 `request_id` 的回调会先识别为 `protocol`，再打印日志。
- [x] C# Proxy `Release|x86|net46` 编译通过。
- [ ] 真实终端授权回调现场复测待执行。

## 本次修改内容

1. `TerminalCallbackHandler.HandleAsync()` 将协议回调兜底识别前移到日志输出前。
2. 授权回调缺少 `resource_type` 时，日志改为输出 `resource_type=protocol(inferred)`。
3. 未识别资源类型时不再输出空 `resource_type=`，只保留未知资源类型诊断日志。

## 兼容性说明

- DLL 导出函数、第三方回调 JSON、终端请求/回调协议：未修改。
- 仅调整 C# Proxy 内部日志打印顺序和显示内容。

## 验证状态

- [x] `git diff --check`：通过，仅有 LF/CRLF 规范化提示。
- [x] C# Proxy `Release|x86|net46` 编译：通过，0 警告，0 错误。
- [ ] 真实终端授权回调日志：待现场复测，预期不再出现 `[终端回调] resource_type=` 空值。

## 回退方式

- 将 `TerminalCallbackHandler.HandleAsync()` 中的 `resource_type` 日志恢复到兜底识别前打印即可。

# 授权结果字段自动回填与抓拍汇总修复（2026-07-08）

## 当前阶段

- [x] C# Proxy 授权回调支持在终端未回传证件/姓名等字段时自动回填原请求信息。
- [x] DLL 授权调用和 C# Proxy 本机授权入口均已登记原始授权请求体。
- [x] 抓拍成功日志改为前缀汇总，带无畸变图路径的指纹抓拍成功日志也会进入抓拍汇总。
- [ ] 真实终端授权回调和指纹抓拍现场复测待执行。

## 本次修改内容

1. `RequestRegistry` 的请求上下文新增 `OriginalRequestBodyUtf8`，用于保存授权请求原始字段。
2. `DllCommandHandler.EnqueueAuthorize()` 注册授权请求时传入 DLL 原始请求体。
3. `BizOperationHandler.RequestAuthorizeAsync()` 为 Proxy 本机授权按钮构造兼容 DLL 字段名的回填请求体。
4. `TerminalCallbackHandler.HandleProtocolAsync()` 优先使用终端回调字段，字段为空时回填 `ZJHM/ZJLB/GJDQDM/XM/XB/CSRQ/KADM`。
5. `MainForm.TryAggregateCaptureSuccess()` 从精确匹配改为前缀匹配，避免追加无畸变图路径后脱离抓拍汇总。

## 涉及文件

- `Core/RequestRegistry.cs`：保存原始请求体。
- `Server/DllCommandHandler.cs`：DLL 授权请求注册时携带原请求体。
- `Server/Coordinator/BizOperationHandler.cs`：Proxy 本机授权请求注册时携带回填源。
- `Server/TerminalCallbackHandler.cs`：授权结果字段自动回填并继续投递给 DLL。
- `MainForm.cs`：抓拍成功日志汇总前缀匹配。
- `PROGRESS.md`：记录本次修改。

## 兼容性说明

- DLL 导出函数、调用约定、参数顺序、错误码：未修改。
- 第三方授权回调 JSON 字段名：未修改，仍输出 `ZJHM/ZJLB/GJDQDM/XM/XB/CSRQ/KADM`。
- 终端 HTTP 请求格式：未修改。
- 配置文件、部署方式、x86/net46 目标：未修改。

## 风险与注意事项

1. 回填只在终端回调字段为空时生效；如果终端回调给出非空字段，以终端回调为准。
2. C# Proxy 本机授权入口当前没有口岸代码输入，因此该入口回填的 `KADM` 仍为空；DLL 调用入口会使用原请求中的 `KADM`。
3. 真实终端仍可能返回 `status=no`，此时授权结果仍为 `0`，但证件/姓名字段应不再为空。

## 验证状态

- [x] `git diff --check`：通过，仅有工作区 LF/CRLF 规范化提示。
- [x] C# Proxy `Release|x86|net46` 编译：通过，0 警告，0 错误。
- [ ] CSharpThirdPartyDemo + DLL 授权回调字段回填：待现场复测。
- [ ] 指纹抓拍日志汇总与 352x544 无畸变图保存：待现场复测。

## 回退方式

- 回退 `OriginalRequestBodyUtf8` 相关注册和路由字段，`TerminalCallbackHandler` 恢复为只读取终端回调字段。
- 将 `TryAggregateCaptureSuccess()` 恢复为 `string.Equals()` 精确匹配即可恢复旧日志行为。

# 指纹无畸变图成功日志汇总调整（2026-07-08）

## 当前阶段

- [x] 将无畸变图保存成功日志合并到 `[指纹抓拍]` 汇总日志。
- [x] 保留无畸变图缺字段、保存失败、异常的可见排查日志。
- [ ] 真实终端重新抓拍验证待现场执行。

## 本次修改内容

1. `SaveUndistortedFingerprintImage()` 从直接输出成功日志改为返回保存路径。
2. `CaptureFingerprintAsync()` 在主图保存成功日志中追加无畸变图保存结果。
3. 成功场景由两条日志合并为一条，例如：`[指纹抓拍] 图片保存成功，无畸变图保存成功: ...`。

## 兼容性说明

- DLL 导出函数、C# ThirdParty Demo 调用参数、Proxy HTTP API 和终端协议：未修改。
- 仅调整 Proxy 内部日志输出方式，不影响图片保存路径和文件内容。

## 验证状态

- [x] Proxy `Release|x86|net46` 编译：通过，0 警告，0 错误。
- [ ] CSharpThirdPartyDemo + 真实终端指纹抓拍：待现场重新执行，预期成功时只出现一条 `[指纹抓拍]` 汇总日志。

## 回退方式

- 将 `SaveUndistortedFingerprintImage()` 恢复为 `void` 并在方法内部输出 `[无畸变] 图片保存成功` 即可回退。

# 指纹无畸变图 data 字段解析修复（2026-07-08）

## 当前阶段

- [x] 修复 CSharpThirdPartyDemo 调 DLL 后 Proxy 未保存 352x544 无畸变指纹图的问题。
- [x] 保持 DLL 导出函数、C# Demo 调用签名和终端协议不变。
- [x] Proxy `Release|x86|net46` 默认输出已重新编译到测试目录。
- [ ] 真实终端重新抓拍验证待现场执行。

## 本次修改内容

1. `BizOperationHandler.SaveUndistortedFingerprintImage()` 保留兼容顶层 `undistorted_image_base64`。
2. 顶层取不到时改为读取 `data.undistorted_image_base64`，匹配终端实际返回结构。
3. 缺少无畸变字段时改为输出可见业务日志，避免 Info 日志级别下静默跳过。
4. 无畸变图继续按 352x544 写入 BMP。

## 涉及文件

- `Server/Coordinator/BizOperationHandler.cs`：修复无畸变 Base64 字段解析路径和跳过日志。
- `PROGRESS.md`：记录本次修复。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和第三方回调签名：未修改。
- C# ThirdParty Demo 双参数调用：未修改。
- Proxy HTTP API、终端请求/响应协议、配置文件和部署方式：未修改。
- 仅修正 Proxy 对终端 JSON 的读取位置，兼容旧的顶层字段和新的 `data` 内字段。

## 验证状态

- [x] Proxy `Release|x86|net46` 临时输出目录编译：通过，0 警告，0 错误。
- [x] Proxy `Release|x86|net46` 默认输出目录编译：通过，0 警告，0 错误。
- [ ] CSharpThirdPartyDemo + 真实终端指纹抓拍：待现场重新执行，预期在无畸变目录生成 `fingerprint_undistorted_*.bmp`。

## 回退方式

- 将 `SaveUndistortedFingerprintImage()` 恢复为只读取顶层 `undistorted_image_base64` 即可回退本次行为。

# C# Proxy 单实例防重复启动（2026-07-07）

## 当前阶段

- [x] 在修改前提交当前 `v1.2.6` 基线：`971cff4 release: v1.2.6 current proxy baseline`。
- [x] 增加 C# Proxy 单实例启动保护。
- [x] 编译和自动化回归测试已完成。
- [ ] 双开弹窗待现场/人工验证。

## 本次修改内容

1. `Program.Main()` 启动早期创建命名互斥体 `Local\HZCYKJTHardWare.Proxy.SingleInstance`。
2. 第一个进程持有互斥体直到主窗口退出，保持原启动流程和后台服务行为不变。
3. 第二个进程发现已有实例运行时，不创建 `MainForm`、不启动监听服务，只弹窗提示“程序已在运行，请勿重复打开。”后退出。

## 涉及文件

- `Program.cs`：新增单实例互斥体检查和重复启动提示。
- `PROGRESS.md`：记录本次修改。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和第三方回调签名：未修改。
- Proxy HTTP API、终端协议、配置文件和部署方式：未修改。
- 第一个正常启动的 Proxy 行为：不变。
- 重复启动行为：由“可能打开第二个界面并启动服务失败”改为“弹窗提示后退出”。
- 互斥体使用 `Local\` 命名空间，限制同一 Windows 登录会话内单实例；不要求管理员权限。

## 风险与注意事项

1. 如果现场存在多用户远程桌面，并要求整台机器全局只允许一个实例，需要改为 `Global\` 互斥体；该方式可能涉及额外权限，不作为本次默认实现。
2. 第二个实例只弹窗退出，不主动激活已有窗口；如后续需要，可再增加查找旧窗口并置前逻辑。

## 验证状态

- [x] `Release|x86|net46` 编译：通过，0 警告，0 错误。
- [x] 测试项目 `Release|x86|net46` 编译：通过，0 警告，0 错误。
- [x] 自动化回归测试：69/69 通过。
- [x] `git diff --check`：通过，仅有 LF/CRLF 规范化提示。
- [ ] 双开弹窗验证：待现场/人工验证。

## 回退方式

- 移除 `Program.cs` 中 `SingleInstanceMutexName` 常量和 `Main()` 内的 `Mutex` 判断，即可恢复修改前允许重复启动到主界面的行为。

## 当前阶段

C# Proxy 外部预览窗口时序修复验证阶段。

## 本次修改内容

1. 外部预览启动成功后，先等待 Proxy 主窗口完成最小化，再发送 `/preview-ready` 回调给第三方 UI。
2. `MinimizeMainFormAsync()` 使用 UI 线程执行窗口操作，并设置 2 秒超时，避免 UI 线程异常时导致回调无限等待。
3. `SetMinimizeToTaskbar()` 增强隐藏/已最小化状态处理，确保窗口可以完成明确的 `Show` 到 `Minimized` 状态切换。

## 涉及文件

- `Server/DllCommandHandler.cs`：调整外部预览成功后的最小化与回调顺序。
- `MainForm.cs`：补强主窗口最小化逻辑。

## 兼容性说明

- DLL 导出函数：未修改。
- 第三方调用参数：未修改。
- `/preview-ready` 回调 JSON 字段：未修改。
- 预览播放器和 HWND 嵌入逻辑：未修改。
- 新增依赖：无。

## 风险与注意事项

1. 第三方收到 `/preview-ready` 的时间最多可能比之前晚约 2 秒，但 HTTP `/preview/*/start` 仍会立即返回 `accepted`。
2. 若 UI 线程长时间无响应，最小化等待超时后仍会继续回调，避免第三方永久等待；现场仍需验证是否能彻底解决首次调用 UI 异常。

## 验证状态

- [x] `git diff --check`：通过，仅有 Git CRLF 规范化提示。
- [x] `Release|x86` 编译：通过，0 错误，0 警告。
- [ ] 第三方 UI 首次调用后是否恢复正常：待现场验证。

## 回退方式

1. 将 `DllCommandHandler.cs` 中外部预览成功分支恢复为先发送 `/preview-ready`，再执行窗口最小化。
2. 将 `MainForm.SetMinimizeToTaskbar()` 恢复为仅设置 `_suppressTrayHide` 和 `WindowState = FormWindowState.Minimized`。
# 方案 C 内部结构重构进度

## 当前阶段

- [x] 阶段 1：`Single Runner + Latest Pending` 业务队列。
- [x] 阶段 2：统一 `RequestRegistry` 和请求状态机。
- [x] 阶段 3：Proxy 生命周期、活动任务跟踪和有界停止。
- [x] 阶段 4：统一 `SwitchCoordinator`，收口 Runtime/Coordinator/Scheduler 边界。
- [x] 阶段 5：DLL `SdkRuntime`、在途调用租约和限时 `ReleaseSdk`。
- [x] 阶段 6：移除 DLL 重复业务队列，由 C# Proxy 独占业务排队职责。
- [ ] 真实终端并发、切换、回调和长稳联调。

## 本次修改内容

1. `ActiveTasksTracker` 改为先占用并发槽再创建任务，容量满时不再启动未追踪任务。
2. `TransportLayer` 跟踪 accept loop、活动连接和 handler；停止时关闭活动 socket，并按统一期限等待。
3. `ProxyRuntime` 使用约 5 秒共享停止预算；所有 WorkerQueue 先同时停止，再共享 3 秒退出预算。
4. MainForm 启停路径统一调用 `ProxyServer.Dispose()`，释放 `RequestRegistry` 定时器、HTTP 客户端和传输资源。
5. DLL 请求和 UI 请求统一通过 `SwitchCoordinator`，移除重复的切换执行逻辑和未使用预览/Misc worker。
6. DLL 新增 `SdkRuntime` 状态机，串行化初始化/释放；导出业务函数通过运行时租约保护在途调用。
7. `ReleaseSdk` 在释放期间拒绝新调用，等待在途调用；第三方回调或接收线程无法及时退出时安全失败，允许调用方重试。
8. CallbackServer 停止时主动关闭监听及活动 socket；EventDispatcher 增加限时停止和回调内禁止释放保护。
9. DLL 释放时清除第三方回调，避免重新初始化后使用历史回调地址。
10. 删除 DLL 中已禁用的 `DllTaskQueue` 旧实现；业务队列唯一所有者为 C# Proxy。

## 涉及文件

- `Server/Runtime/ActiveTasksTracker.cs`、`TransportLayer.cs`、`ProxyRuntime.cs`
- `Server/Coordinator/SwitchCoordinator.cs`
- `Server/Scheduler/WorkerExecutionEngine.cs`
- `Server/ProxyServer.cs`、`Server/DllCommandHandler.cs`、`MainForm.cs`
- `Core/QueueManager.cs`、`Core/WorkerQueue.cs`
- `src/sdk_runtime.h`、`src/sdk_runtime.cpp`
- `src/exports.cpp`、`src/callback_server.*`、`src/event_dispatcher.*`
- `scripts/verify_dll_lifecycle.ps1`

## 兼容性说明

- DLL 导出函数：未改变，当前产物与基线均为 20 个导出函数。
- 调用约定和参数：保持 `__stdcall` 及现有参数顺序。
- 第三方返回值：保持 1/0 语义。
- 第三方回调 JSON、终端 HTTP 路径和主要报文字段：未改变。
- C# Proxy：继续使用 `net46`、`x86`，未新增第三方依赖。
- 终端回调请求体上限调整为 16 MB，并发读取连接限制为 8，保护 x86 地址空间。

## 风险与注意事项

1. `ReleaseSdk` 遇到超过预算的在途调用或阻塞的第三方回调会返回 0，不会强制销毁仍被使用的资源；调用方应在业务调用结束后重试。
2. 16 MB 回调上限已覆盖当前约 0.8 MB OCR 基线样例，仍需用真实终端最大虹膜/OCR 报文确认。
3. 当前运行中的 Proxy 占用了正式输出 EXE，因此本轮 C# 构建使用独立临时输出目录完成验证，未终止用户进程。
4. 集成测试依赖 `HttpListener`，当前执行环境报 `PlatformNotSupportedException`；需在正式 Windows 测试环境执行。

## 验证状态

- [x] C# Proxy `Release|x86`：0 warning / 0 error。
- [x] C# 测试项目 `Release|x86`：0 warning / 0 error。
- [x] 核心单元测试：24/24 通过。
- [x] DLL `Release|Win32`：0 warning / 0 error。
- [x] DLL 导出表：20/20 与基线一致。
- [x] x86 DLL 生命周期：连续 3 次 Init/Release 成功，Release 为 1ms/0ms/0ms。
- [ ] 7 项 Proxy 集成测试：待正式 Windows `HttpListener` 环境验证。
- [ ] 真实终端虹膜/OCR/NFC/授权和切换联调：待验证。
- [ ] 2 小时快速压测及 24 小时长稳：待验证。

## 下一步计划

- [ ] 部署新 Proxy 与 DLL 到隔离测试目录，执行完整端到端回归。
- [ ] 在活动抓拍、活动回调、终端断开三种情况下测量 `ReleaseSdk`。
- [ ] 记录线程、句柄、内存、GDI 对象及 P95/P99 响应时间。

## 回退方式

- 按本轮 Git diff 回退上述 C#、C++ 和项目文件。
- 删除 `src/sdk_runtime.h/.cpp` 并从 `HZCYKJTHardWare.vcxproj` 移除对应编译项。
- 恢复旧 `exports.cpp` 导出包装和 Proxy 停止/切换实现。

# 方案 B：第三方 HTTP MJPEG 预览自动恢复（2026-06-29）

## 当前阶段

代码修改和离线验证已完成，等待真实终端断流/恢复联调。

## 本次修改内容

1. HTTP MJPEG 连续两次读取失败后，不再永久重试旧 URL；播放器向 `PreviewManager` 上报一次流故障并退出。
2. `PreviewManager` 按会话代次串行执行“释放旧播放器 → 清除 URL 缓存 → 申请新 URL → 在原 HWND 重建播放器”。
3. 恢复失败按 1/2/5/10 秒有上限退避继续尝试；主动停止、终端切换、Proxy 退出或 HWND 失效时退出旧恢复任务。
4. HTTP MJPEG 临时 URL 不再参与 60 秒后台主动校验，避免校验请求生成新流并干扰正在播放的流；RTSP 校验保持不变。
5. 增加恢复退避和 HTTP URL 校验策略单元测试。

## 涉及文件

- `Preview/MjpegPreviewController.cs`：断流判定、一次同 URL 快速重连、单次故障通知和线程退出。
- `Preview/PreviewManager.cs`：会话代次、串行自动恢复、取消/退避和 HTTP URL 校验策略。
- `../HZCYKJTHardWare.Proxy.Tests/Preview/PreviewRecoveryPolicyTests.cs`：恢复策略测试。

## 兼容性说明

- DLL 导出函数、调用约定、参数和返回值：未修改。
- 第三方预览请求、响应、回调 JSON 和 HWND 使用方式：未修改。
- 终端 HTTP 路径和报文字段：未修改。
- C# Proxy：仍为 `net46`、`x86`，未新增依赖。

## 风险与注意事项

1. 终端 HTTP 服务持续不可用时，Proxy 会以最多每 10 秒一次的频率重试该会话；同一时刻每个会话只允许一个恢复任务。
2. 从视频停止到触发新 URL 恢复，最坏包含两次 5 秒读取超时和一次 1 秒同 URL 重连等待；现场需确认该灵敏度是否合适。
3. 新 URL 申请及播放器重建受全局预览操作锁串行保护，恢复期间主动停止或终端切换最多等待当前一次 5 秒 URL 请求结束。

## 验证状态

- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [x] C# Proxy `Release|x86`：0 warning / 0 error。
- [x] C# 测试项目 `Release|x86`：0 warning / 0 error。
- [x] 非集成单元测试：26/26 通过（包含新增 2 项恢复策略测试）。
- [ ] 7 项 Proxy 集成测试：当前环境 `HttpListener` 报 `PlatformNotSupportedException`，待正式 Windows 测试环境验证。
- [ ] 真实终端断流后自动申请新 URL 并恢复原第三方 HWND：待验证。
- [ ] 终端切换、主动停止与断流恢复并发：待验证。
- [ ] 2 小时快速压测和 24 小时长稳：待验证。

## 下一步计划

- [ ] 在隔离目录部署新 Proxy，分别模拟 MJPEG 断流、终端 HTTP 服务短暂不可用和服务恢复。
- [ ] 核对日志中同一会话仅出现一个恢复任务，并记录黑屏持续时间。
- [ ] 回归第三方摄像头、指纹、虹膜预览的启动、停止和终端切换。

## 回退方式

- 仅回退 `MjpegPreviewController.cs`、`PreviewManager.cs` 和 `PreviewRecoveryPolicyTests.cs` 本节对应 diff；DLL 无需回退。

# v1.2.5 基线后工业级稳定性优化（2026-06-30）

## 当前阶段

- [x] 保持 `net46`、`x86`、原生 TCP/HTTP 和现有 DLL ABI。
- [x] 落实“执行 1 + 等待 1 + 第三个替换等待项”。
- [x] 落实 Proxy 到 DLL 的业务结果一次投递及 DLL 端防御性去重。
- [x] 完成阶段 3 生命周期收口所需的停止、取消和后台任务观察。

## 本次修改内容

1. 队列满载时只替换唯一等待任务，不中断正在执行任务；被替换任务立即返回 `queue_replaced`。
2. HTTP 等待方超时后原子完成任务结果，仍在等待队列中的任务不再迟到执行硬件操作。
3. `RequestRegistry` 的重复注册、活动容量、完成迁移和 TTL 清理统一串行化；活动项严格限制为 5000，完成去重项限制为 8192。
4. 每个请求增加生命周期取消令牌；完成、失败、超时、切换和停止会取消正在进行的一次性回调投递。
5. Proxy 到 DLL 的业务结果只投递一次；网络异常、超时、503 或其他非 2xx 响应立即结束请求并记录失败，不在新请求到达后延迟投递旧结果。
6. 终端回调 HTTP 在线程被接纳后立即返回 202，保存和 DLL 回调投递在有界后台任务中执行，不增加第三方 UI 调用 DLL 的同步等待。
7. DLL 回调接收端仅在成功进入有界队列后返回 202；队列满或停止返回 503，不完整 HTTP body 返回 400。
8. DLL 去重键改为 `(request_id, resource_type)`；已完成记录限制为 8192，回调等待队列同时限制 512 条和 64MB，避免 x86 地址空间被大图载荷耗尽。
9. DLL 事件队列不再静默删除最旧项；worker 在停止时排空已接纳任务，并交替处理回调和第三方事件，避免单侧饥饿。
10. `ProxyRuntime` 停止时先取消正在进行的回调投递，再取消注册表、释放队列、停止预览并排空后台任务；后台异常和资源释放异常均写入完整日志。

## 涉及文件

- `Core/WorkerQueue.cs`、`Core/QueueManager.cs`、`Core/RequestRegistry.cs`
- `Server/DllCommandHandler.cs`、`Server/DllCallbackSender.cs`、`Server/TerminalCallbackHandler.cs`、`Server/ProxyServer.cs`
- `Server/Runtime/ProxyRuntime.cs`、`Server/Runtime/ActiveTasksTracker.cs`
- `Server/Scheduler/WorkerExecutionEngine.cs`
- `src/callback_server.cpp`
- `src/event_dispatcher.h`、`src/event_dispatcher.cpp`
- `src/request_session_manager.h`、`src/request_session_manager.cpp`
- `HZCYKJTHardWare.Proxy.Tests/Core/WorkerQueueTests.cs`
- `HZCYKJTHardWare.Proxy.Tests/Core/RequestRegistryTests.cs`
- `HZCYKJTHardWare.Proxy.Tests/Core/DllCallbackSenderTests.cs`

## 兼容性说明

- DLL 导出表仍为 20 项，函数名、`__stdcall` 装饰、参数和结构体均未修改。
- DLL 为 `Win32/x86`；C# Proxy 为 `.NET Framework 4.6/x86`。
- 正常业务的 DLL 接口、终端请求字段、回调 JSON 和成功语义未改变。
- 未新增第三方依赖，未新增 `DllTaskQueue`，第三方 UI 到 DLL 再到 Proxy 的同步 HTTP 调用链未增加等待层。

## 风险与注意事项

1. 业务结果采用一次投递，优先保证实时性和请求时序；DLL 不可达、超时或返回 503 时，该结果不会再次延迟投递，第三方应根据失败状态重新发起业务请求。
2. 已开始执行的硬件调用不会被强制中断；仅保证尚在等待的超时或被替换任务不会继续执行。
3. DLL 第三方事件回调仍为单 worker 串行调用；第三方回调长期不返回时会形成背压，但不会阻塞第三方 UI 调用 DLL 的 HTTP 请求路径。
4. 完成去重记录超过 8192 时淘汰最旧项；这是 x86 内存上限与 10 分钟去重窗口之间的保护策略。

## 验证状态

- [x] C# Proxy `Compile|Release|x86`：通过；正式输出 EXE 正被运行中进程占用，本轮未覆盖该文件。
- [x] C++ DLL `Build|Release|Win32`：通过，0 警告、0 错误。
- [x] 非集成回归测试：35/35 通过。
- [x] 队列极端顺序：仅执行首项和最新等待项；超时等待项不迟到执行。
- [x] Registry：重复不覆盖、并发容量精确、完成取消、8192 完成项上限。
- [x] 回调发送：503、400、网络失败和非法 URL 均不重试；每个业务结果最多发起一次 HTTP 投递。
- [x] DLL ABI：与 `.baseline/exports_v1.2.def` 一致；生成 DLL 确认为 x86，20 项导出及 stdcall 装饰保持不变。
- [ ] 现有 7 项集成测试：当前测试运行器创建 `HttpListener` 时抛出 `PlatformNotSupportedException`，未进入被测业务，待正式 Windows 测试宿主复验。
- [ ] 真实第三方 UI 线程调用、真实终端回调、断网恢复、终端切换和 2/24 小时长稳验证。

## 下一步计划

- [ ] 在正式 x86 Windows 测试宿主运行 7 项集成测试。
- [ ] 使用第三方 Demo 测量 DLL 同步调用和结果回调耗时，确认不存在旧结果延迟投递。
- [ ] 执行 DLL 回调端停止/重启、503、重复回调和大图压力测试。
- [ ] 使用连续新请求覆盖旧等待项，验证旧请求不会因网络恢复而产生延迟回调。

## 回退方式

- Git 基线标签：`v1.2.5`（提交 `7196dba`）。
- 本轮修改尚未提交；可按上述涉及文件逐项回退，DLL ABI 和部署结构无需迁移。

# 外部预览重复最小化闪现修复（2026-06-30）

## 本次修改内容

- `MainForm.SetMinimizeToTaskbar()` 改为幂等操作：窗口已经最小化或隐藏到托盘时直接返回。
- 删除重复预览启动时的 `Minimized → Normal → Minimized` 强制状态切换。
- 首次从正常显示状态启动外部预览时，仍按原流程最小化并发送 `/preview-ready`。

## 兼容性说明

- DLL 导出函数、调用约定、参数、错误码和回调 JSON：未修改。
- 第三方 HWND、预览播放器、终端 HTTP 路由和回调顺序：未修改。
- 未新增依赖，不增加第三方同步调用等待。

## 验证状态

- [x] 原因定位：重复预览调用会主动将已最小化窗口恢复为 `Normal` 后再次最小化。
- [x] C# Proxy `Compile|Release|x86`：通过；仅有沙箱无法写入增量状态缓存的警告。
- [x] 非集成回归测试：35/35 通过。
- [ ] 第三方场景“视频预览后启动指纹预览”：待现场验证窗口不再闪现。

## 回退方式

- 恢复 `MainForm.SetMinimizeToTaskbar()` 中的 `Show()` 和 `Minimized → Normal → Minimized` 状态切换。

# 压力测试固定图片与CSV汇总（2026-06-30）

## 本次修改内容

- `scripts/stress_test.ps1` 的人脸抓拍固定写入 `face.jpg`，每次成功抓拍覆盖同一文件。
- 指纹抓拍固定写入 `fingerprint.jpg`，每次成功抓拍覆盖同一文件。
- 每次压力测试生成 `stress_summary_yyyyMMdd_HHmmss.csv`，记录人脸、指纹及抓拍总成功/失败次数、终端切换成功/失败次数、循环次数、耗时和失败率。
- 终端切换成功统计口径为 `/terminal/switch` 返回 `status=ok`。

## 兼容性说明

- 仅修改测试脚本，不修改 DLL、Proxy、HTTP 路由或业务返回格式。
- 默认输出目录仍为 `stress_captures`；仅图片文件名和汇总输出方式变化。

## 验证状态

- [x] PowerShell AST 语法解析：通过。
- [x] 已核对 Proxy 对带扩展名的 `save_dir` 使用精确文件路径并覆盖保存。
- [ ] 连接真实 Proxy 执行压力测试并核对 CSV 统计值：待验证。

## 回退方式

- 恢复 `Invoke-Capture()` 传入目录路径，并删除 CSV 汇总生成段。

# DLL 真实第三方链路压力测试脚本（2026-06-30）

## 当前阶段

- [x] 新增 DLL 直调压力测试脚本。
- [x] 完成无设备静态验证。
- [ ] 连接真实 Proxy 和硬件终端执行压力测试。

## 本次修改内容

1. 新增 `scripts/stress_test_dll.ps1`，通过 x86 PowerShell 内嵌 C# P/Invoke 直接调用现有 `__stdcall` DLL 导出函数。
2. 测试链路改为“第三方 UI 线程 → DLL → HTTP → C# Proxy → 硬件终端”，不再绕过 DLL。
3. 默认创建 WinForms 预览窗口并传入有效摄像头、指纹 HWND；所有 DLL 调用在该 STA UI 线程同步执行，用调用耗时衡量第三方 UI 阻塞时间。
4. 覆盖 `InitSdk`、事件回调注册、`StartProcess`、摄像头/指纹预览、人脸/指纹抓拍、终端切换、停止预览、`EndProcess` 和 `ReleaseSdk`。
5. 人脸、指纹抓拍分别固定覆盖 `face.jpg` 和 `fingerprint.jpg`。
6. 每次运行生成汇总 CSV 和逐次调用明细 CSV，记录抓拍及切换成功/失败、P95/最大耗时、UI 阻塞阈值命中数和回调数量。
7. 新增 `-ValidateOnly`，只验证 x86 自重启、DLL PE 架构和 P/Invoke 编译，不初始化 SDK、不调用 Proxy、不切换终端。

## 涉及文件

- `scripts/stress_test_dll.ps1`：DLL 真实第三方链路压力测试脚本。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`：本次进度记录。

## 兼容性说明

- DLL 导出函数、参数、`__stdcall` 调用约定及 1/0 返回语义：未修改。
- C# Proxy HTTP 路由、请求/响应格式和硬件业务逻辑：未修改。
- 脚本无第三方依赖；64 位宿主会自动转入系统 32 位 Windows PowerShell STA 进程。
- `char*` 路径按 UTF-8 非托管内存传入，兼容中文保存路径。
- 默认加载 Proxy `bin/x86/Release/net46` 目录中的 DLL，使 DLL 可按现有配置发现同目录 Proxy EXE；也可用 `-DllPath` 指定正式部署 DLL。

## 风险与注意事项

1. 正式运行脚本会真实初始化 DLL、启动预览、抓拍并在终端 1/2 之间切换；执行前必须确认现场允许切换。
2. 抓拍和终端切换是同步 DLL 调用；逐次 CSV 中的 `DurationMs` 即第三方 UI 线程本次被占用的时间。
3. 脚本按真实 UI 串行调用模型执行，不用于制造多个线程同时调用 DLL 的并发队列场景。
4. DLL 对外只返回 1/0；失败的内部错误原因仍需结合 DLL 和 Proxy 日志定位。

## 验证状态

- [x] PowerShell AST 语法解析：通过。
- [x] `-ValidateOnly`：通过；已验证自动进入 x86 STA PowerShell、DLL 为 x86 PE、内嵌 P/Invoke 声明可编译。
- [ ] DLL 初始化与 Proxy 启动：待真实环境验证。
- [ ] 摄像头/指纹预览和固定文件覆盖：待真实终端验证。
- [ ] 抓拍、切换统计及 CSV 数值：待真实压力测试核对。
- [ ] 第三方 UI 阻塞 P95/P99 和最长耗时：待真实压力测试评估。

## 下一步计划

- [ ] 先运行 5 分钟测试，核对固定图片、汇总 CSV、逐次调用 CSV 与 DLL/Proxy 日志。
- [ ] 通过后执行 2 小时快速压力测试和 24 小时长稳测试。

## 回退方式

- 删除 `scripts/stress_test_dll.ps1`，并移除本节进度记录；DLL、Proxy 和第三方接口无需回退。

# 终端切换后当前终端显示实时刷新（2026-06-30）

## 当前阶段

- [x] 原因定位与代码修复完成。
- [x] `net46/x86` 隔离编译和非集成回归完成。
- [ ] 使用新构建 Proxy 进行 DLL 真实切换显示验证。

## 本次修改内容

1. `SwitchCoordinator` 在 `TerminalManager.SwitchTo()` 实际提交新终端后发送内部状态通知，不在 DLL 请求刚受理时提前更新 UI。
2. `ProxyServer` 增加可选的内部终端状态回调，并在服务启动时同步一次实际初始终端。
3. `MainForm` 接收状态通知后通过 `BeginInvoke` 安全切回 WinForms UI 线程，更新当前终端文字和左右通道按钮状态。
4. 删除左右通道按钮完成后无条件设置 `_headerTerminalIndex` 的逻辑；切换失败时界面不再错误显示为目标终端。
5. 增加 `SwitchCoordinatorTests.SwitchToAsync_NotifiesCommittedTerminal`，验证通知值与已提交的 `TerminalManager.CurrentIndex` 一致。

## 涉及文件

- `Server/Coordinator/SwitchCoordinator.cs`：实际切换提交后的状态通知。
- `Server/ProxyServer.cs`：内部终端状态回调转发和初始状态同步。
- `MainForm.cs`：UI 线程安全刷新当前终端显示。
- `HZCYKJTHardWare.Proxy.Tests/Core/SwitchCoordinatorTests.cs`：状态通知回归测试。
- `PROGRESS.md`：本次进度记录。

## 兼容性说明

- DLL 导出函数、参数、调用约定和返回值：未修改。
- `/terminal/switch` 请求、响应格式和“先受理、后执行”行为：未修改。
- 终端切换、预览停止与恢复顺序：未修改。
- 新增回调仅为 Proxy 进程内部状态通知，不增加第三方 UI 调用等待，不引入第三方依赖。
- 继续兼容 `.NET Framework 4.6/x86`。

## 风险与注意事项

1. DLL 切换接口返回成功表示请求已受理；标题区域在后台切换真正更新 `TerminalManager` 后刷新，存在正常的短暂时间差。
2. 终端状态通知在后台线程触发；MainForm 已处理窗口销毁竞态，关闭期间不再投递 UI 更新。
3. 当前正式 Proxy EXE 正在运行，本次仅生成隔离验证产物，未覆盖或重启现场进程。
4. 测试项目现有 `DllCallbackSenderTests.cs` 使用 `System.Net.Http`，但测试 csproj 未显式声明该框架引用；本次采用明确 net46 引用的手工编译验证，未扩大范围修改测试项目配置。

## 验证状态

- [x] C# Proxy `Release|x86|net46` 隔离编译：通过，代码编译 0 错误。
- [x] 新增终端通知测试：1/1 通过。
- [x] 非集成回归测试：36/36 通过。
- [ ] 7 项现有 Proxy 集成测试：本轮未执行；当前环境的 `HttpListener` 限制仍待正式 Windows 测试宿主复验。
- [ ] DLL/压力脚本切换终端 1/2 后标题、URL、按钮状态实时更新：待重启新构建现场验证。

## 下一步计划

- [ ] 退出当前旧 Proxy，构建并启动新版本。
- [ ] 分别通过 Proxy UI、第三方 DLL 和 `stress_test_dll.ps1` 切换终端，核对标题文字、URL及按钮状态。
- [ ] 模拟切换失败，确认标题仍保持实际终端，不提前显示目标终端。

## 回退方式

- 回退 `SwitchCoordinator`、`ProxyServer` 和 `MainForm` 的终端状态回调修改，并删除对应测试文件；DLL 和 HTTP 协议无需回退。

# 方案 B：A→B→A 后 IC/OCR/虹膜流程回调恢复（2026-07-01）

## 当前阶段

- [x] 根因定位完成：流程 request_id 被终端切换 generation 清理，且旧 Registry 按一次性回调结束流程绑定。
- [x] 流程会话与一次性请求 Registry 分离。
- [x] 控制操作并发门、终端路由快照和回调来源校验完成。
- [x] `net46/x86` 隔离编译和非集成回归完成。
- [ ] 双真实终端 A→B→A IC/OCR/虹膜现场验证。

## 本次修改内容

1. 新增 `TerminalProcessRegistry`，按终端独立保存长生命周期流程会话；A、B 的成功 `StartProcess` 绑定可同时存在，切换 generation 不再删除流程会话。
2. `StartProcess` 使用 `Prepare → terminal POST → Commit/Rollback` 事务式更新；同一终端重新开始流程仅在新请求成功后替换旧会话。
3. 新增无等待 `ControlOperationGate`，串行保护 `StartProcess`、`EndProcess` 和终端切换；竞争请求立即返回 busy，不新增第三方 UI 等待队列。
4. `RequestRegistry` 继续只管理 OCR/NFC/虹膜/授权等一次性主动请求；切换只取消旧 generation 的一次性请求，并保留旧版本流程登记的滚动兼容保护。
5. 终端回调首先按一次性请求精确匹配；未命中时再按流程 request_id 匹配当前激活终端的持久会话。
6. 回调投递前校验会话终端、当前终端、路由 epoch 和可识别的来源 IP；处理期间发生切换时取消旧路由投递，避免 A/B 串线。
7. 每个持久流程事件生成唯一内部 DLL 投递 request_id，避免 C++ DLL 的一次性去重表吞掉同一流程中的第二次刷卡/OCR；原流程 request_id 仍用于 Proxy 内部路由和文件归档。
8. 对相同终端回调正文设置 2 秒短窗去重，抑制设备的即时重复 POST；DLL 投递仍为单次尝试，不恢复长时间重试。
9. 切换提交后同步当前终端的流程活动状态和保存目录；Runtime 关闭时同时清理一次性请求与持久流程会话。

## 涉及文件

- `Core/ControlOperationGate.cs`：控制操作立即获取/立即拒绝门。
- `Core/TerminalProcessRegistry.cs`：按终端持久化流程会话、事务替换和事件去重。
- `Core/RequestRegistry.cs`：一次性请求增加终端归属，切换清理兼容流程条目。
- `Terminal/TerminalManager.cs`：原子路由快照、route epoch 和来源 IP 到终端映射。
- `Server/Coordinator/SwitchCoordinator.cs`：切换纳入共享控制门。
- `Server/Coordinator/BizOperationHandler.cs`、`Server/DllCommandHandler.cs`：流程启动/结束接入新 Registry。
- `Server/TerminalCallbackHandler.cs`：一次性请求与持久流程双通道路由、防串线和唯一投递 ID。
- `Server/ProxyServer.cs`、`Server/Runtime/ProxyRuntime.cs`、`Server/Scheduler/WorkerExecutionEngine.cs`：依赖组合、生命周期和保存目录接入。
- `HZCYKJTHardWare.Proxy.Tests/Core/TerminalProcessRegistryTests.cs`：流程会话并发与替换测试。
- `HZCYKJTHardWare.Proxy.Tests/Core/TerminalCallbackRoutingTests.cs`：A→B→A NFC 回调恢复回归测试。
- `HZCYKJTHardWare.Proxy.Tests/Core/RequestRegistryTests.cs`：切换 generation 兼容测试。

## 兼容性说明

- DLL 导出函数、参数、调用约定、结构体、错误码：未修改。
- Proxy HTTP 路由、终端请求格式和第三方 DLL 调用方式：未修改。
- 继续兼容 `.NET Framework 4.6/x86`，未新增第三方依赖。
- 未新增 `DllTaskQueue` 或等待队列；控制操作冲突立即返回 busy。
- `EndProcess` 保持现有全局语义：清空所有终端流程会话和一次性请求。
- 业务回调失败仍立即结束本次投递，不做长时间重试；持久流程会话本身不因单次 DLL 回调失败而失效。

## 风险与注意事项

1. 持久流程事件的 DLL 回调 request_id 为内部派生 ID；第三方原有 `StartProcess` 仅接收成功/失败返回，不依赖流程 request_id，因此不改变导出接口，但现场需核对第三方日志是否错误假设 request_id 固定。
2. 来源 IP 只有在能映射到已配置终端时才做强校验；经过 NAT/反向代理且无法识别来源时，仍以流程 request_id 与当前路由为准。
3. 2 秒相同正文去重用于抑制即时重复 POST；如果设备对两次合法刷卡生成完全相同正文且间隔小于 2 秒，第二次会按重复事件跳过。
4. 本轮未修改 C++ DLL；C++ 通过现有 `process_active` fallback 接收派生事件 ID，并继续执行接收端去重。

## 验证状态

- [x] C# Proxy `Release|x86|net46` 隔离编译：通过，0 警告、0 错误。
- [x] C# Proxy 正式 `bin/x86/Release/net46` 构建：通过，0 警告、0 错误。
- [x] 非集成单元测试：42/42 通过。
- [x] 自动回归 A→B→A：A 首次 NFC 投递、A 非当前终端时拒绝、切回 A 后再次投递通过。
- [x] 流程 Registry：A/B 会话并存、单终端替换不影响另一终端、启动失败回滚、重复事件短窗去重通过。
- [ ] 现有集成测试：本轮未执行；当前测试宿主的 `HttpListener` 限制仍待正式 Windows 测试宿主复验。
- [ ] 真实 IC 卡、OCR 证件、虹膜设备 A→B→A：待现场验证。
- [ ] 第三方 DLL 事件回调内容和 UI 实时性：待 `stress_test_dll.ps1` 验证。

## 下一步计划

- [ ] 使用新构建 Proxy 和 DLL，分别在 A、B 各调用一次 `StartProcess`。
- [ ] 执行 A 刷卡/OCR → B 刷卡/OCR → 切回 A 刷卡/OCR，核对每次第三方事件和 Proxy 路由日志。
- [ ] 连续切换 1000 次并交叉刷卡，确认无跨终端回调、无 busy 状态残留、内存和句柄稳定。

## 回退方式

- 回退 `TerminalProcessRegistry`、`ControlOperationGate` 及上述 Server/Terminal 接入修改，恢复流程资源登记到 `RequestRegistry` 的旧实现；DLL、HTTP 协议和部署配置无需回退。

# 方案 B：x86 稳定性、切换熔断与手工 TCP 超时加固（2026-07-01）

## 当前阶段

- [x] 路由、Generation、取消令牌原子化。
- [x] 旧终端批次主动熔断并解除 HTTP 等待。
- [x] 手工 TCP 异步读写截止时间生效。
- [x] Base64 图片保存移除整图 `byte[]` LOH 分配。
- [x] `Release|x86|net46` 编译与完整自动回归。
- [ ] 双真实终端和硬件 SDK 长稳压测。

## 本次修改内容

1. 新增不可变 `TerminalRouteEpochSnapshot`，由 `SwitchCoordinator.TryCaptureRoute` 在同一临界区内绑定终端路由、Generation 和批次取消令牌。
2. DLL HTTP 入口、UI 直调、Scheduler 工作线程统一使用准入时路由快照；执行阶段不再动态读取 `CurrentBaseUrl/CurrentIndex`。
3. 终端切换开始时先取消旧批次令牌，再清理旧 Generation Registry；排队等待和正在执行的终端 HTTP 请求立即完成为 `terminal_switching`，不永久占用 HTTP 客户端。
4. OCR、NFC、虹膜、授权、人脸、指纹、流程启动和预览均透传同一批次上下文，回调 Registry 的 terminal index 与实际下发路由保持一致。
5. `TerminalClient` 支持外部取消令牌，并与原有单次请求超时合并；未改变原有调用签名兼容性和 JSON 返回结构。
6. `HttpProtocolHandler` 的异步 `ReadAsync/WriteAsync/FlushAsync` 接入取消令牌；Proxy 对 DLL 通道使用 2 秒、终端回调通道使用 30 秒硬截止，并通过关闭 `TcpClient` 解除 .NET Framework 异步 Socket 挂起。
7. `FileSaver` 使用 `FromBase64Transform + CryptoStream` 分块解码到临时文件，取消 `Convert.FromBase64String` 产生的整图 LOH `byte[]`；成功后覆盖目标文件，失败时清理临时文件。
8. 保留全部业务队列 `maxLength=2, replaceOld=true`：第 1 个执行、第 2 个等待、第 3 个替换第 2 个。
9. 修正集成测试的终端状态泄漏，并补充旧路由批次取消/新路由快照发布测试；测试项目仅增加 .NET Framework 自带 `System.Net.Http` 引用。

## 涉及文件

- `Terminal/TerminalManager.cs`：不可变路由批次快照。
- `Server/Coordinator/SwitchCoordinator.cs`：原子准入、旧批次取消、新批次发布。
- `Server/DllCommandHandler.cs`：准入快照进入各业务任务，切换时解除等待。
- `Server/Scheduler/WorkerExecutionEngine.cs`：工作线程只使用入队路由，不再漂移到新终端。
- `Server/Coordinator/BizOperationHandler.cs`：UI 直调和流程控制使用同一准入模型。
- `Terminal/TerminalClient.cs`：请求超时与终端批次取消合并。
- `Server/HttpProtocolHandler.cs`、`Server/ProxyServer.cs`：手工 TCP 异步读写硬截止。
- `Storage/FileSaver.cs`：Base64 流式落盘，降低 x86 LOH 压力。
- `HZCYKJTHardWare.Proxy.Tests/Core/SwitchCoordinatorTests.cs`：路由批次切换回归。
- `HZCYKJTHardWare.Proxy.Tests/Integration/ProxyServerIntegrationTests.cs`：测试终端状态隔离。
- `HZCYKJTHardWare.Proxy.Tests/HZCYKJTHardWare.Proxy.Tests.csproj`：补充框架程序集引用。

## 兼容性说明

- DLL 导出名、参数、调用约定、结构体布局、错误码：本轮未修改。
- Proxy HTTP 路由、请求/响应 JSON、流程与预览业务行为：未修改。
- 业务准入仍是 1 个执行 + 1 个最新等待；第 3 个请求替换第 2 个等待请求。
- 继续目标 `.NET Framework 4.6/x86`；仅使用 BCL，无新增第三方依赖。
- C# 第三方 Demo 的 native callback 已由窗体 `readonly` 字段持有强引用；本轮审计确认无需改变回调 ABI。
- 未引入 `HandleProcessCorruptedStateExceptions`；C++ 导出边界继续使用现有 SEH 防护。原生线程级 AV 的最终隔离仍需独立进程方案，不能依靠 C# catch 安全恢复。

## 风险与注意事项

1. 异步 TCP 截止时间现在真实生效；超过 2 秒仍未完整发送 DLL 请求、或超过 30 秒仍未完整发送终端回调的连接会被关闭。
2. 终端可能在取消到达前已经受理旧请求；Proxy 会取消旧 Registry 并拒绝其过期结果，避免回调串到新终端。
3. Base64 流式保存使用同目录临时文件并复制覆盖，降低内存峰值但增加一次本地文件复制；磁盘空间不足时保持原有失败返回。
4. 当前仍为 x86 进程，原始 HTTP Base64 字符串和 JSON 对象仍占用内存；现场应继续监控 Private Bytes、LOH、句柄数和回调积压。
5. 本轮未修改 C++ DLL 文件；工作区内原有 C++ 未提交变更不属于本次修改。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告、0 错误。
- [x] C# 测试程序集 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] Visual Studio VSTest `/Platform:x86` 完整回归：50/50 通过，0 失败、0 跳过。
- [x] “第 3 个替换第 2 个”等待任务精确回归：通过。
- [x] A→B 旧批次取消、新 Route/Generation 发布回归：通过。
- [ ] 真实 OCR 大图/虹膜大图连续 24 小时 LOH 与 Private Bytes：待现场验证。
- [ ] 双终端断线、重连、切换并发 1000 次：待现场验证。
- [ ] C++ DLL Win32 Release 和第三方 Delphi/C# Demo：本轮未改 DLL，仍待交付前联合回归。

## 下一步计划

- [ ] 使用真实终端执行 1 个运行 + 1 个等待 + 连续替换请求，核对仅第 1 个和最后 1 个执行。
- [ ] 在 OCR/虹膜返回大图时记录 Private Bytes、Gen 2/LOH、TCP 连接数和文件落盘耗时。
- [ ] 在终端请求传输中切换 A/B，确认旧 HTTP 调用快速结束、旧回调不投递、新终端请求正常受理。
- [ ] 阶段 6 删除 DLL 重复队列时，保留 `TryCaptureRoute + RouteEpoch + Enqueue(latest-wins)` 作为 Proxy 唯一准入契约。

## 回退方式

- 按文件回退本节列出的 RouteEpoch、TerminalClient 取消、HTTP deadline 和 FileSaver 流式保存修改；QueueManager 与对外 ABI 无需回退。

# 第三方 Panel 预览焦点与 Z-order 修复（2026-07-01）

## 当前阶段

- [x] MJPEG、VLC 跨进程预览子窗口禁止激活。
- [x] 预览子窗口固定到第三方 Panel 子窗口 Z-order 底层。
- [x] Proxy 主窗口改为无激活最小化。
- [ ] 第三方真实 UI 无需切换前后台即可持续点击：待现场验证。

## 本次修改内容

1. MJPEG 与 VLC 的 `STATIC` 渲染子窗口增加 `WS_EX_NOACTIVATE | WS_EX_NOPARENTNOTIFY`，避免跨进程创建子窗口时改变第三方窗口激活状态。
2. 预览布局由 `MoveWindow` 改为 `SetWindowPos(HWND_BOTTOM, ..., SWP_NOACTIVATE)`，保持渲染窗口在 Panel 子控件底层且不抢焦点。
3. VLC 重新绑定父窗口时使用 `SW_SHOWNOACTIVATE` 显示，不再使用可能改变激活状态的 `SW_SHOW`。
4. Proxy 自动最小化改用 `ShowWindowAsync(SW_SHOWMINNOACTIVE)`，失败时才回退到原 WinForms 最小化方式。
5. 暂未禁用预览子窗口输入，避免改变预览区域现有鼠标行为；若现场仍有拦截，再单独评估输入穿透。

## 涉及文件

- `Preview/MjpegPreviewController.cs`：无激活窗口样式及底层 Z-order 布局。
- `Preview/VlcPreviewPlayer.cs`：无激活创建、显示、重绑定和布局。
- `MainForm.cs`：Proxy 无激活最小化。
- `PROGRESS.md`：记录修改范围与验证结果。

## 兼容性说明

- DLL 导出函数、参数、调用约定、错误码和回调 JSON 未改变。
- 第三方继续传入专用预览 `Panel.Handle`，无需修改调用逻辑。
- C# Proxy 继续目标 `.NET Framework 4.6/x86`，未新增依赖。
- 视频 URL、解码方式、画面缩放算法和网络请求逻辑未改变。

## 风险与注意事项

1. `HWND_BOTTOM` 会把视频放在同一 Panel 的其他子控件后面；这是避免视频覆盖按钮的预期行为。
2. 若第三方 Panel 使用特殊透明控件或自定义绘制，需要现场确认画面层次。
3. 本次仅解决焦点/Z-order，不解决 Proxy 重启和第三方客户端生命周期不同步问题。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告、0 错误。
- [x] C# 测试程序集 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] VSTest 非集成回归：43/43 通过。
- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [ ] 真实第三方 Panel：摄像头/指纹预览启动后立即点击其他控件，待验证。
- [ ] 连续启动、停止、切换前后台和终端切换，待验证。

## 下一步计划

- [ ] 替换现场 C# Proxy 后，分别验证摄像头和指纹预览启动瞬间的第三方焦点。
- [ ] 不切换前后台，连续点击第三方功能按钮并观察 10 分钟。
- [ ] 若仍拦截输入，补充渲染子窗口禁用或命中测试穿透方案。

## 回退方式

- 恢复上述三个 C# 文件中的窗口样式、`MoveWindow` 和 WinForms 最小化逻辑，并删除本节进度记录；DLL 和第三方程序无需回退。

# 第三方 Panel 点击预览焦点修复（综合 A+B，2026-07-01）

## 当前阶段

- [x] 点击预览后第三方 UI 无响应的复现条件已定位。
- [x] 预览子窗口输入禁用和 Proxy 隐藏到托盘已完成。
- [x] `Release|x86|net46` 编译与非集成回归已完成。
- [ ] 真实第三方程序点击预览区域验证。

## 本次修改内容

1. MJPEG 和 VLC 的跨进程 `STATIC` 渲染子窗口增加 `WS_DISABLED`，预览窗口仅负责显示，不再接收鼠标、键盘或输入焦点。
2. 保留 `WS_EX_NOACTIVATE | WS_EX_NOPARENTNOTIFY`、`SWP_NOACTIVATE` 和底层 Z-order 防护。
3. 外部预览启动成功后，Proxy 在发送预览就绪回调前隐藏到系统托盘，恢复 v0.9 的窗口生命周期行为。
4. 删除 v1.1 引入的“抑制托盘隐藏并保留任务栏最小化窗口”逻辑，避免点击跨进程预览后输入焦点停留在 Proxy 进程。

## 涉及文件

- `Preview/MjpegPreviewController.cs`：MJPEG 渲染子窗口禁用输入。
- `Preview/VlcPreviewPlayer.cs`：VLC 渲染子窗口禁用输入。
- `MainForm.cs`：外部预览成功后隐藏 Proxy 到托盘。
- `Server/DllCommandHandler.cs`：等待托盘隐藏完成后再通知第三方预览就绪。
- `PROGRESS.md`：本次进度记录。

## 兼容性说明

- DLL 导出函数、参数、调用约定、错误码和回调 JSON：未修改。
- 第三方仍传入原有专用 `Panel.Handle`：调用逻辑无需修改。
- MJPEG/VLC URL、解码、缩放和网络重连逻辑：未修改。
- C# Proxy 仍为 `.NET Framework 4.6/x86`：未新增依赖。
- 外部行为变化：Proxy 不再保留任务栏最小化项，改为隐藏到托盘；预览画面本身不再接收点击。

## 风险与注意事项

1. 若未来需要在视频画面内实现点击、双击、拖拽或右键菜单，需要单独设计由第三方 Panel 接收输入的交互协议。
2. Proxy 隐藏后需通过系统托盘图标恢复主窗口，与 v0.9 行为一致。
3. 跨进程 `SetParent` 架构仍保留，本次通过显示窗口禁用输入和顶层窗口隐藏规避焦点归属问题。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告、0 错误。
- [x] C# 测试程序集 `Release|x86|net46`：通过，1 个既有 `AssemblyReference.cache` 访问权限警告、0 错误。
- [x] VSTest 非集成回归：43/43 通过。
- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [ ] 真实第三方 Panel：点击摄像头、指纹预览区域后立即操作其他按钮，待验证。
- [ ] Proxy 托盘隐藏、托盘恢复、连续启停预览和终端切换，待验证。

## 下一步计划

- [ ] 退出当前旧 Proxy，部署并启动本次新构建。
- [ ] 启动 MJPEG 预览后连续点击画面，再直接点击第三方按钮，确认无需 Alt+Tab 即可响应。
- [ ] 连续执行预览启动/停止、终端切换和第三方程序重启，确认无焦点回归。

## 回退方式

- 从 MJPEG/VLC 子窗口样式中移除 `WS_DISABLED`，并恢复 `MainForm.SetMinimizeToTaskbar()` 与 `DllCommandHandler.MinimizeMainFormAsync()`；DLL 和第三方程序无需回退。

# 外部预览跨宿主与 Proxy 重启恢复（方案 B，2026-07-02）

## 当前阶段

- [x] 修改前基线已提交并创建注释标签 `v1.2.6`，提交号 `d7b1fbb`。
- [x] 基线提交未包含任何 `.ico` 文件。
- [x] C# Demo/第三方宿主退出后的旧外部预览清理已实现。
- [x] 第三方仅调用一次预览接口时，C# Proxy 重启后的自动重建请求已实现。
- [x] DLL、Proxy、测试编译及无真实终端的租约恢复验证已完成。
- [ ] 真实终端、真实 C# Demo 和第三方程序现场联调。

## 本次修改内容

1. C# Proxy 每次创建服务实例时生成唯一 `proxy_instance_id`，`GET /ping` 在保留 `status=ok` 的基础上返回该标识。
2. DLL 在摄像头或指纹外部预览活动期间按 `check_hwnd_interval_ms` 轻量查询 Proxy 实例；检测到服务不可用后恢复、实例变化或同一进程内服务重建时，使用原 `request_id`、原 `HWND` 和回调地址重新提交预览。
3. DLL 监控请求采用 `750ms` 有界超时；`ReleaseSdk` 先停止监控，再尽力通知 Proxy 停止摄像头和指纹预览，Proxy 不可用时不阻塞本地资源释放。
4. Proxy 记录外部预览宿主的 `HWND + PID + 进程启动时间`，使用已有 `check_hwnd_interval_ms` 定时校验；宿主退出或 HWND 被其他进程复用时，串行停止并移除旧播放器和恢复信息。
5. 显式新预览仍保持“后请求替换旧外部会话”的既有语义，终端切换继续由原 generation/route epoch 约束接管。
6. 增加 Proxy 实例标识、外部宿主身份校验单元测试，以及 x86 DLL 假 Proxy 生命周期验证脚本。

## 涉及文件

- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Infrastructure/AppConfig.cs`：读取 `check_hwnd_interval_ms`。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Preview/PreviewManager.cs`：外部宿主身份记录、周期校验和失效会话释放。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/DllCommandHandler.cs`：Proxy 实例标识和 `/ping` 响应。
- `src/delphi_proxy.h`、`src/delphi_proxy.cpp`：实例标识查询及有界预览恢复/停止请求。
- `src/exports.cpp`：外部预览租约监控、Proxy 重启恢复和 `ReleaseSdk` 清理。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Core/ProxyInstanceIdentityTests.cs`：实例标识测试。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Preview/ExternalPreviewHostIdentityTests.cs`：HWND 所有者与句柄销毁测试。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Integration/ProxyServerIntegrationTests.cs`：`/ping` 稳定实例标识断言及集成分类。
- `scripts/verify_preview_lease.ps1`：x86 DLL 的 Proxy 中断、实例变化、自动重发和 Release 停止验证。

## 兼容性说明

- DLL 导出函数名、参数、`__stdcall`、结构体、回调签名和错误码均未改变。
- 第三方仍只需调用原有 `StartCameraPreview` / `StartFingerprintPreview`；无需增加重连调用。
- `/ping` 仅增加 `proxy_instance_id` 字段，原有 `status=ok` 保持不变。
- C# Proxy 继续使用 `.NET Framework 4.6/x86`，DLL 继续生成 `Win32/x86`，未新增第三方依赖。
- 虹膜预览继续使用 DLL 本地渲染链路，本次未改变。

## 风险与注意事项

1. Proxy 的预览启动响应仍是异步受理；租约恢复确认“请求已受理”，真实画面是否成功仍由现有预览回调和日志确认。
2. 当前每种资源仍只有一个 External 会话，多个第三方进程同时启动同一资源时继续保持最后一次请求替换前一次请求。
3. 第三方必须保持传入的预览 HWND 有效；控件主动重建 Handle 后仍应重新调用预览接口。
4. Proxy 重启期间终端也不可用时，DLL 会保留租约并按周期重试；真实终端恢复时长需现场记录。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告、0 错误。
- [x] C# 测试程序集（临时隔离输出）`Release|x86|net46`：通过，0 警告、0 错误。
- [x] VSTest 非集成回归：46/46 通过。
- [x] DLL `Release|Win32`：通过，0 警告、0 错误。
- [x] DLL 架构与导出表：PE32/x86，20/20 导出名称及 `__stdcall` 装饰保持不变。
- [x] `verify_preview_lease.ps1`：`PASS ping=5 camera_start=2 camera_stop=1`，确认 Proxy 中断/实例变化后自动重发一次，并在 Release 时停止。
- [x] `git diff --check`：通过，仅存在既有 LF/CRLF 提示。
- [ ] 7 项 Proxy 集成测试：当前桌面测试宿主创建 `HttpListener` 报 `PlatformNotSupportedException`，需在正式 Windows 测试环境执行。
- [ ] 真实 C# Demo 关闭后第三方立即接管摄像头/指纹预览：待验证。
- [ ] 第三方保持运行，完全退出并重启 C# Proxy 后画面自动恢复：待验证。
- [ ] 连续 20 次宿主/Proxy 重启、终端切换竞争及 2 小时/24 小时长稳：待验证。

## 下一步计划

- [ ] 部署最新 `Release/HZCYKJTHardWare.dll` 与 Proxy `Release|x86` 生成物。
- [ ] 分别验证 C# Demo 正常关闭、强制结束以及第三方接管场景。
- [ ] 第三方保持运行时退出并重启 Proxy，记录自动恢复时长和 DLL/Proxy 日志中的实例标识。
- [ ] 执行连续重启、终端切换和长稳验证，观察线程数、句柄数、GDI 对象与内存趋势。

## 回退方式

- 以 `v1.2.6` 为修改前基线，恢复本节列出的 DLL/Proxy/测试文件并移除 `verify_preview_lease.ps1`；不需要恢复或修改任何 `.ico` 文件。

# 通道级车牌 RTSP 预览（2026-07-02）

## 当前阶段

- [x] DLL 已启用既有车牌预览导出函数。
- [x] 车牌 VLC 已迁移到 C# Proxy，外部会话直接绑定第三方 HWND。
- [x] Proxy 本地调试预览与第三方预览可同时运行，双方独立启停。
- [ ] 现场相机配置和真实 RTSP 播放待验证。

## 本次修改内容

1. `ConfigManager` 新增 `preview.plate` 配置读取和 RTSP URL 构建，默认 `/Streaming/Channels/101`，仅在配置为 `102` 时使用子码流。
2. 用户名和密码执行 RTSP user-info 百分号编码，支持账号中存在特殊字符。
3. DLL 新增 `/preview/plate/start`、`/preview/plate/stop` 转发和车牌预览租约；第三方进程不再加载或释放车牌 VLC。
4. C# Proxy 的外部车牌会话调用 `libvlc_media_player_set_hwnd` 直接绑定第三方传入的专用 HWND，不再创建跨进程 `STATIC` 子窗口。
5. Proxy 新增独立本地车牌调试会话和 Panel；调试预览默认不自动启动，可与外部会话同时播放，任一会话启停或失败不修改另一会话。
6. 车牌外部/本地会话标记为通道级资源，终端1/2切换时不停止、不重启。
7. DLL 监控 Proxy 实例变化，Proxy 重启后保留有效 HWND 租约并重建外部车牌预览；`ReleaseSdk` 主动通知 Proxy 停止。
8. Proxy VLC 日志中的 RTSP 认证信息统一替换为 `***:***@`，避免账号密码落入日志。
9. 根目录、Delphi ThirdParty Demo 和 Delphi 7 Demo 配置模板补充单镜头车牌配置。

## 涉及文件

- `src/config_manager.h`、`src/config_manager.cpp`：车牌配置、主/子码流和 URL 编码。
- `src/hzsjkjt_context.h`、`src/hzsjkjt_context.cpp`：运行期车牌配置和外部预览租约。
- `src/delphi_proxy.h`、`src/delphi_proxy.cpp`：车牌预览 Proxy HTTP 路由封装。
- `src/exports.cpp`：既有车牌导出接口改为 Proxy 转发、重启恢复和释放清理。
- `src/event_dispatcher.cpp`：车牌预览成功/失败事件和失败租约清理。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Infrastructure/AppConfig.cs`：Proxy 读取同一份车牌 RTSP 配置。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Preview/PreviewManager.cs`：外部/本地双会话及终端切换隔离。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Preview/VlcPreviewPlayer.cs`、`VlcPreviewController.cs`：外部 HWND 直绑、输入禁用和凭据脱敏。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/DllCommandHandler.cs`：独立车牌启动/停止路由。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/Coordinator/BizOperationHandler.cs`、`MainForm.cs`、`MainForm.Designer.cs`：本地车牌调试预览。
- `src/libvlc_rtsp_renderer.cpp`：RTSP 凭据日志脱敏。
- `HZCYKJTHardWare.json`、`demo/DelphiThirdPartyDemo/HZCYKJTHardWare.json`、`demo/Delphi7Demo/HZCYKJTHardWare.json`：部署配置模板。

## 兼容性说明

- DLL 导出名、参数、`__stdcall` 调用约定、错误码和事件结构体：未改变。
- 车牌预览不绑定终端1/2，切换终端时外部和本地调试会话均保持当前播放。
- `preview.plate.enabled=false` 时继续返回 `HZCYKJTHardWare_RET_UNSUPPORTED`。
- 未新增第三方依赖，车牌 libVLC 仅由 x86 C# Proxy 进程加载。

## 风险与注意事项

1. 配置模板没有写入现场 IP、用户名和密码；启用前必须补齐并设置 `enabled=true`。
2. 同时开启第三方和 Proxy 调试预览会建立两路 RTSP 连接并执行两路解码；调试会话默认关闭，现场需要确认相机连接数上限。
3. 主码流码率和解码负载通常高于子码流，现场需要观察 Proxy x86 进程 CPU、Private Bytes 和 VLC 稳定性。
4. 当前未连接真实车牌相机验证认证、编码格式、双路播放和断流行为。

## 验证状态

- [x] DLL `Release|Win32`：通过，0 警告、0 错误。
- [x] C# Proxy `Release|x86`：通过，0 警告、0 错误。
- [x] 车牌配置和日志脱敏单元测试：3/3 通过。
- [!] Proxy 全量测试：49项通过；7项集成测试因当前测试宿主不支持 `HttpListener` 未执行成功，与车牌代码断言无关。
- [x] 生成物：PE32/x86。
- [x] DLL 导出表：20/20，`StartPlatePreview@4` 与 `StopPlatePreview@0` 保持不变。
- [x] 四份配置 JSON（含 Release 输出）解析通过，默认端口554、主码流101。
- [ ] TestTool：工作区中不存在原配置路径下的 `testdemo.vcxproj`，未执行。
- [ ] 真实 RTSP 主码流、第三方 HWND 直绑、Proxy 同步调试、重复启停和 SDK 释放：待验证。

## 下一步计划

- [ ] 填入通道车牌相机 `host/username/password` 并设置 `enabled=true`。
- [ ] 使用第三方传入的专用 Panel HWND 连续启停20次。
- [ ] 同时开启第三方预览与 Proxy 本地调试预览，分别停止其中一路并确认另一路持续播放。
- [ ] 验证终端1/2切换不影响车牌预览。
- [ ] 验证断网恢复、错误密码、相机重启以及2小时/24小时长稳。

## 回退方式

- 设置 `preview.plate.enabled=false` 可立即关闭功能并恢复原“不支持”行为。
- 代码回退时恢复本节列出的 C++、C# Proxy 和配置文件；DLL ABI 无需回退。

# 车牌 CJ/RJ2/RJ3 平铺接口（2026-07-03）

## 当前阶段

- [x] DLL/Proxy 不感知方向，只提供 CJ、RJ2、RJ3 平铺接口。
- [x] 三路外部预览与三路本地调试预览可分别建立会话。
- [x] Native、C# Proxy、测试项目编译及车牌配置单元测试完成。
- [ ] 真实车牌相机和第三方程序联调待执行。

## 本次修改内容

1. 删除旧 `StartPlatePreview/StopPlatePreview` DLL 导出，新增 `Start/StopPlatePreviewCJ`、`Start/StopPlatePreviewRJ2`、`Start/StopPlatePreviewRJ3`，保持 `__stdcall` 和 `HWND` 参数类型。
2. DLL 将三路状态拆分为独立配置、`request_id`、HWND 和运行标记；Proxy 重启恢复、宿主失效清理及 `ReleaseSdk` 停止均逐路处理。
3. Proxy 新增 `/preview/plate/cj|rj2|rj3/start|stop` 六个平铺路由，不读取、不推断 `Direction`。
4. `PreviewManager` 使用 `PlateCJ`、`PlateRJ2`、`PlateRJ3` 三个资源键，避免启动或停止一路时覆盖其他车牌会话。
5. `preview.plate` 改为 `cj`、`rj2`、`rj3` 三个独立配置节点；每个节点分别配置 RTSP 地址、认证和主/子码流。
6. Proxy 管理界面增加三组独立启停按钮和三块预览区域；C# 第三方 Demo 的 P/Invoke 声明同步更新。
7. 接口头文件、导出表、README 和两份第三方调用说明同步更新。

## 涉及文件

- `include/HZCYKJTHardWare.h`、`HZCYKJTHardWare.def`：六个新导出声明，移除两个旧符号。
- `src/config_manager.*`、`src/hzsjkjt_context.*`：三路配置和运行状态。
- `src/delphi_proxy.*`、`src/exports.cpp`、`src/event_dispatcher.cpp`：平铺路由、租约、回调和释放。
- `Infrastructure/AppConfig.cs`、`Preview/PreviewManager.cs`、`Server/DllCommandHandler.cs`：三路 Proxy 配置、会话和 HTTP 路由。
- `Server/Coordinator/BizOperationHandler.cs`、`MainForm.cs`、`MainForm.Designer.cs`：三路本地调试操作及界面。
- `HZCYKJTHardWare.json`、两份 Demo JSON：三路配置模板。
- `PlatePreviewConfigurationTests.cs`：三路配置隔离和 URL 测试。
- `README.md`、两份第三方调用说明：新 ABI 和调用组合说明。

## 兼容性说明

- 外部接口：旧车牌导出符号按需求删除；第三方必须重新编译并切换到新接口。
- 调用约定：新函数继续使用 Win32 `__stdcall`；Start 参数仍为一个稳定的专用 `HWND`。
- 方向组合：方向 1 由第三方调用 CJ；方向 2 由第三方分别传入两个 HWND 调用 RJ2、RJ3。
- 配置文件：旧单节点 `preview.plate.enabled/host/...` 不再读取，部署时必须迁移到三个子节点。
- 回调和错误码：继续使用 `plate_image`、1901/1902/1903 及现有返回码；通过唯一 `request_id` 区分三路。
- 平台和依赖：继续使用 DLL Win32/x86、C# Proxy .NET Framework 4.6/x86，未新增依赖。

## 风险与注意事项

1. RJ2、RJ3 同时预览会建立两路 RTSP 连接并增加 x86 Proxy 的 CPU、内存和 VLC 解码负载。
2. 三个 Start 接口必须传入三个相互独立、生命周期稳定的 HWND；复用同一个 HWND 会产生渲染竞争。
3. 旧 DLL 符号已删除，未升级的第三方程序会在加载或调用时失败。
4. 当前只完成无真实相机验证；认证失败、断流、相机重启和并发释放仍需现场验证。

## 验证状态

- [x] DLL `Release|Win32`：通过，0 警告、0 错误。
- [x] DLL 导出表：24 个符号；CJ/RJ2/RJ3 六个新符号存在，旧车牌符号不存在。
- [x] C# Proxy/Test `Release|x86|net46` 独立输出：通过，0 警告、0 错误。
- [x] C# 第三方 Demo `Release|x86|net46` 独立输出：通过，0 警告、0 错误。
- [x] 车牌配置和 URL 单元测试：4/4 通过。
- [!] 全量测试：50/57 通过；7 项集成测试因当前测试宿主不支持 `HttpListener` 未执行成功。
- [x] `git diff --check`：通过（仅保留工作区既有 LF/CRLF 提示）。
- [ ] Proxy 管理界面实际启动和三块 Panel 布局：待人工验证。
- [ ] 真实 CJ 单路、RJ2+RJ3 双路、独立停止、Proxy 重启恢复和 SDK 释放：待验证。

## 下一步计划

- [ ] 填写三路相机配置并分别验证主码流/子码流及认证失败提示。
- [ ] 方向 2 同时启动 RJ2、RJ3，分别停止其中一路并确认另一路持续播放。
- [ ] 连续执行 20 次三路启停、第三方窗口关闭和 Proxy 重启。
- [ ] 记录 2 小时/24 小时 CPU、Private Bytes、线程、句柄和相机连接数。

## 回退方式

- 代码回退时仅反向撤销本节列出的三路平铺修改，不覆盖工作区此前已有改动。
- 配置回退时恢复旧单节点 `preview.plate`；如仅需现场禁用，分别设置 `cj/rj2/rj3.enabled=false`。
- 回退到旧 DLL 时第三方程序也必须恢复调用旧 `StartPlatePreview/StopPlatePreview`，DLL 与调用方必须成套部署。

# Proxy 车牌预览界面汉化与布局优化（2026-07-03）

## 当前阶段

- [x] 预览控制区域拥挤和英文函数名已处理。
- [x] 车牌业务名称、占位提示及日志已汉化。
- [x] C# Proxy 编译验证完成。
- [ ] 真实窗口、高 DPI 和多分辨率视觉验证待执行。

## 本次修改内容

1. 将 `StartPlatePreviewCJ/RJ2/RJ3` 按钮文案改为“出境车牌预览”和“入境车牌预览 2/3”，不向管理用户暴露内部函数名。
2. 车牌预览 Panel 占位提示和启停日志同步使用相同业务名称。
3. 顶部三张控制卡片高度由 332 调整为 380，预览控制六行按钮获得更多垂直空间。
4. 上下两排预览 Panel 增加间距，避免黑色预览区域视觉粘连。
5. 相应下移预览区和日志区；窗口总尺寸及视频区域高度保持不变。

## 涉及文件

- `MainForm.cs`：按钮文案、业务名称映射、日志文案和 Panel 间距。
- `MainForm.Designer.cs`：控制卡片高度、区域位置和占位提示。
- `todo.md`、`PROGRESS.md`：记录修改与验证状态。

## 兼容性说明

- DLL 导出函数、`__stdcall`、错误码和回调结构：未改变。
- Proxy HTTP 路由及 CJ/RJ2/RJ3 会话逻辑：未改变。
- JSON 配置结构和部署方式：未改变。
- C# Proxy 继续使用 `.NET Framework 4.6/x86`，未新增依赖。

## 风险与注意事项

1. 顶部控制区域增加 48 像素后，日志显示区相应减少 48 像素。
2. 当前未替换正在运行的旧 Proxy，需启动新构建后确认现场 DPI 下的实际效果。
3. 入境镜头通过数字 2/3 区分，内部接口名仍保持 RJ2/RJ3，不影响第三方调用。

## 验证状态

- [x] C# Proxy `Release|x86|net46` 独立输出：通过，0 警告、0 错误。
- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [ ] 预览控制按钮无重叠：待启动新 Proxy 人工确认。
- [ ] 100%、125%、150%、200% DPI：待验证。

## 下一步计划

- [ ] 退出当前旧 Proxy，部署并启动新构建。
- [ ] 核对六行预览控制按钮、三块车牌占位提示和实时日志区域。
- [ ] 分别在常用 DPI 下调整窗口大小，确认文字不截断、不重叠。

## 回退方式

- 恢复 `MainForm.cs` 中旧按钮/日志文案及 Panel Margin。
- 恢复 `MainForm.Designer.cs` 中顶部区域高度、位置和旧占位提示。
- DLL、配置及第三方程序无需回退。

# Proxy 长期运行 UI 日志无响应优化（2026-07-03）

## 当前阶段

- [x] 完成长时间日志量与 UI 刷新链路排查。
- [x] 按最小改动方案完成 UI 日志汇总、批量插入、批量裁剪和 Undo 缓冲限制。
- [x] 完成 x86 Release 编译、现有测试和 10 万行 UI 日志回放。
- [ ] 现场 24～48 小时长稳验证待执行。

## 本次修改内容

1. 人脸和指纹“图片保存成功”明细继续完整写入磁盘日志，UI 改为每秒显示一条成功数量汇总。
2. 同一批次且颜色相同的日志合并为一次 `RichTextBox` 写入，避免逐行执行 `Select/SelectedText`。
3. 实时日志超过 3300 行后批量裁剪回 3000 行，避免达到上限后每次刷新都从文本头部删除少量行。
4. 历史日志由逐行插入改为按颜色分组批量插入；返回实时模式时强制裁剪到 3000 行。
5. 将 RichEdit Undo 上限设为 0，避免只读日志控件长期积累无用途的编辑历史。

## 涉及文件

- `MainForm.cs`：UI 日志汇总、批量格式化、批量裁剪和 Undo 设置。
- `PROGRESS.md`：本次修改、兼容性、验证及回退记录。

## 兼容性说明

- DLL 导出名、参数、`__stdcall`、错误码和回调结构：未改变。
- Proxy HTTP 请求/响应格式、终端通信和业务队列：未改变。
- 磁盘日志：成功明细保持不变；仅管理界面改为按秒汇总显示。
- 配置文件、部署方式、x86 和 `.NET Framework 4.6`：未改变。
- 未新增第三方依赖。

## 风险与注意事项

1. 管理界面不再逐条显示人脸/指纹成功日志，排查单笔明细时应查看 EXE 磁盘日志。
2. 实时日志窗口允许在两次批量裁剪之间短暂增长到 3300 行，这是为减少头部删除次数的预期行为。
3. 本次压力回放验证了 UI 控件处理路径，但不能替代真实设备、预览解码和终端通信同时运行的现场长稳测试。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 测试项目 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 非集成测试：50 项通过。
- [!] 7 项集成测试：测试宿主构造 `HttpListener` 时抛出 `PlatformNotSupportedException`，与本次 UI 日志代码无关。
- [x] UI 日志压力回放：100,000 行通过，复跑总耗时 71,072 ms，单批 100 行最大耗时 164 ms。
- [x] 高频日志汇总：人脸 1000 条、指纹 1000 条正确生成汇总。
- [x] 历史日志：500 行批量插入和返回实时模式通过。
- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [ ] 真实业务按钮、自动滚动、错误着色和“仅错误”筛选：待人工界面验证。
- [ ] 24～48 小时设备长稳及 UI 响应：待验证。

## 下一步计划

- [ ] 使用新构建运行真实人脸/指纹抓拍，确认 UI 每秒汇总且磁盘明细完整。
- [ ] 压力期间反复点击流程、切换和预览按钮，记录最长 UI 响应时间。
- [ ] 连续运行 24～48 小时，记录 CPU、Private Bytes、线程、Handle、GDI 对象和 UI 队列长度。

## 回退方式

- 仅反向撤销 `MainForm.cs` 中本节对应的日志汇总、批量插入、批量裁剪和 Undo 设置。
- `Logger.cs`、DLL、配置和第三方程序均无需回退。

# Proxy 日志字体与重绘一致性（2026-07-03）

## 当前阶段

- [x] 日志字体统一和同步重绘修改完成。
- [x] C# Proxy x86 Release 编译完成。
- [ ] 实际窗口和不同 DPI 视觉验证待执行。

## 本次修改内容

1. 日志字体由不包含中文字形的 `Consolas` 改为 `Microsoft YaHei 9F Regular`，避免中文回退到其他字体造成粗细不一致。
2. `WM_SETREDRAW` 恢复后增加 `Update()`，立即完成 RichTextBox 重绘，减少快速刷新时的重绘残影。
3. 设计器与运行时使用同一字体，避免控件创建和后续日志插入采用不同字体。

## 涉及文件

- `MainForm.cs`：运行时日志字体和同步重绘。
- `MainForm.Designer.cs`：设计器日志字体。
- `PROGRESS.md`：本次修改和验证记录。

## 兼容性说明

- DLL、Proxy HTTP、配置、磁盘日志、错误码和第三方调用行为：未改变。
- 继续使用 `.NET Framework 4.6/x86`，未新增依赖。
- `Microsoft YaHei` 已在当前构建环境解析为 `Regular 9F`；字体为非等宽字体，长路径不再按字符列严格对齐。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 字体解析：`Microsoft YaHei -> Microsoft YaHei, Regular, 9F`。
- [x] `git diff --check`：通过，仅有工作区既有 LF/CRLF 提示。
- [ ] 实际窗口中文、英文、数字粗细一致性：待人工验证。
- [ ] 100%、125%、150%、200% DPI 和持续日志刷新：待验证。

## 回退方式

- 将 `MainForm.cs` 和 `MainForm.Designer.cs` 的日志字体恢复为 `Consolas 9F`。
- 移除 `EndLogUpdate()` 中新增的 `memoLog.Update()`；其他日志性能优化不需要回退。

# Proxy 历史日志插入错位修复（2026-07-03）

## 当前阶段

- [x] `[2[2026...` / `026-07-03...` 日志拆分问题已复现并修复。
- [x] 针对性 UI 回归测试通过。
- [ ] 新版本部署和现场界面确认待执行。

## 本次修改内容

1. 确认问题发生在历史日志按颜色分组插入时：字符串换行长度与 RichEdit 选择索引不属于同一坐标体系，黄色警告日志被插入到下一条实时日志的第 2 个字符之后。
2. 每个颜色分组插入完成后，改用 `SelectionStart` 获取 RichEdit 的真实插入终点，作为下一分组的插入位置。
3. 新增回归测试：先写入实时日志，再向头部插入两条普通历史日志和一条黄色警告日志，验证四条日志的内容和顺序完全一致。

## 涉及文件

- `MainForm.cs`：修正批量格式化日志的插入位置计算。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：新增日志顺序回归测试。
- `PROGRESS.md`：本次修复和验证记录。

## 兼容性说明

- 磁盘日志、DLL、Proxy HTTP、配置、颜色规则和第三方调用行为：未改变。
- 仅修正管理界面历史日志与实时日志混合显示时的字符位置。
- 未新增依赖，继续使用 `.NET Framework 4.6/x86`。

## 验证状态

- [x] Proxy `Release|x86|net46` Compile：通过，0 警告、0 错误。
- [x] 定向回归测试 `PrependHistoryLines_WithColorChange_DoesNotSplitActiveLine`：1/1 通过。
- [x] 回归测试修复前精确复现：警告行前出现 `[2`，下一实时行从 `026-07-03` 开始。
- [x] 修复后日志内容、顺序和时间戳均完整。
- [ ] 正在运行的旧 Proxy 未替换；重启新构建后待人工确认。

## 回退方式

- 恢复 `InsertFormattedLines()` 原插入位置累计方式，并删除对应 UI 回归测试；其他日志性能和字体修改无需回退。
# 硬件健康检测网格（2026-07-06）

## 当前阶段

- [x] 当前版本已提交为 `v1.2.6` 基线（`aa3d9e7b`）。
- [x] 完成硬件健康响应解析、五设备状态网格和人性化提示文案。
- [x] 完成 `Release|x86|net46` 编译及非集成回归测试。
- [ ] 真实终端、常用 DPI 和长时间轮询待现场验证。

## 本次修改内容

1. 在顶部运行信息区下方增加 OCR、NFC、指纹、虹膜、人脸五列健康卡片，不增加页面固定内容总高度。
2. 支持待检测、正常、启动中、离线、异常五种视觉状态；使用图标、状态文字、语义色和浅色背景共同表达，不依赖单一颜色。
3. `recovery_local`、`silence_timeout`、`recovery_local_failed` 分别转换为“正在恢复连接…”“设备暂未响应”“自动恢复失败，请检查设备”。原始诊断代码、`request_id` 和检测时间保留在 Tooltip 中。
4. “启动中”使用低频图标切换动画；无启动中设备时自动停止 Timer，避免无意义 UI 刷新。
5. 健康检测在服务启动后立即执行，并在终端切换后立即刷新；仍保留 5 分钟周期轮询。
6. 校验顶层 `status` 和 `data`，缺失设备补为 `unknown/not_reported`，避免空 `data` 被误判为全部正常。
7. 使用终端 `RouteEpoch` 丢弃切换过程中的旧终端响应，并合并并发刷新请求，避免状态串台或刷新丢失。

## 涉及文件

- `UI/HardwareHealthPanel.cs`：五设备状态卡片、配色、动画、Tooltip 和人性化文案。
- `MainForm.cs`：嵌入健康面板、接收状态更新、服务停止时复位。
- `MainForm.Designer.cs`：重新分配 Header、操作区和预览区高度；预览改为等比例双行布局。
- `Terminal/TerminalHealthChecker.cs`：即时检测、响应校验、缺失设备处理和并发刷新保护。
- `Server/ProxyServer.cs`：终端切换后触发健康状态刷新。
- `Terminal/TerminalHealthCheckerTests.cs`：协议解析和文案映射测试。
- `UI/MainFormLogRenderingTests.cs`：健康卡片数量、Dock 布局及区域不重叠测试。

## 兼容性说明

- DLL 导出函数、参数、`__stdcall`、结构体、错误码和回调签名：未改变。
- Proxy HTTP 请求/响应格式及 `/resources/devices/status` 路径：未改变。
- JSON 配置、部署方式和第三方调用行为：未改变。
- 继续使用 `.NET Framework 4.6/x86`，未新增依赖。

## 风险与注意事项

1. 当前自动化测试验证了控件数量、Dock 关系和固定内容总高度，不能替代 100%、125%、150%、200% DPI 的人工视觉检查。
2. 终端未上报五类设备中的任意一项时，该设备显示“待检测”，整体不判定为健康；需确认现场固件始终返回完整设备列表。
3. 健康接口异常时保留五张卡片并显示检测失败，不会将终端不可达直接等同为五个硬件全部离线。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 测试项目 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 健康响应解析与文案定向测试：4/4 通过。
- [x] UI Dock 与旧日志回归测试：2/2 通过。
- [x] 非集成回归测试：56/56 通过。
- [!] 完整测试：56 项通过、7 项集成测试因测试宿主构造 `HttpListener` 抛出既有 `PlatformNotSupportedException` 未执行成功。
- [ ] 真实终端五种状态、终端切换和 5 分钟轮询：待验证。
- [ ] 100%、125%、150%、200% DPI 及高分屏缩放：待验证。

## 下一步计划

- [ ] 使用真实终端核对五类设备状态和原始诊断代码。
- [ ] 在左右通道间快速切换，确认旧终端响应不会覆盖当前终端状态。
- [ ] 在常用 DPI 下检查设备名称、辅助文案和状态文字无截断。
- [ ] 连续运行 24～48 小时，确认轮询 Timer、句柄和 UI 响应稳定。

## 回退方式

- 以 `v1.2.6`（`aa3d9e7b`）作为本次修改前基线，反向撤销上述 C# UI、健康检测和测试文件即可。
- DLL、配置文件及第三方调用程序无需回退。

# 硬件健康与预览控制细节优化（2026-07-07）

## 当前阶段

- [x] 健康卡片中的 `nfc` 界面命名已改为“IC 卡”，协议字段仍保持 `nfc`。
- [x] 预览控制从 12 个密集按钮改为“设备下拉框 + 开始预览 + 停止预览”。
- [x] 预览设备下拉框改为自绘白底，选择后不再显示系统默认蓝色选中背景。
- [x] 终端未就绪、不可达或健康接口异常时已加入短间隔自动复查机制。
- [ ] 真实终端恢复上线、真实预览启停和多 DPI 视觉效果待现场验证。

## 本次修改内容

1. `HardwareHealthPanel` 将 `nfc` 设备显示从 `NFC / IC 卡` 改为 `IC 卡`，左侧短码改为 `IC`。
2. `MainForm` 新增预览设备下拉框，保留摄像头、指纹、虹膜、出境车牌、入境车牌 2、入境车牌 3 六个目标。
3. 预览启停统一走共享方法，旧的单设备事件方法保留，避免后续 Designer 或测试引用失效。
4. 本地预览状态用 `_activeLocalPreviews` 记录；服务停止时清空状态并禁用预览下拉框。
5. `TerminalHealthChecker` 改为 one-shot 定时调度：健康状态 5 分钟后复查，终端不可达/超时/解析异常 5 秒后复查，硬件非健康 15 秒后复查。
6. 下拉框使用 `OwnerDrawFixed` 自绘显示区，闭合状态强制白底，展开列表选中项使用浅灰底。
7. 补充 UI 测试和健康检测调度测试，覆盖 IC 卡命名、预览控制布局、下拉框自绘和异常复查间隔。

## 涉及文件

- `UI/HardwareHealthPanel.cs`：IC 卡展示名。
- `MainForm.cs`：预览下拉框、开始/停止按钮、预览状态联动。
- `Terminal/TerminalHealthChecker.cs`：异常短间隔复查调度。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：IC 卡命名和预览控制布局测试。
- `HZCYKJTHardWare.Proxy.Tests/Terminal/TerminalHealthCheckerTests.cs`：健康检测复查间隔测试。
- `PROGRESS.md`：本次修改与验证记录。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码、回调签名：未改变。
- Proxy HTTP API、终端协议字段、`/resources/devices/status` 响应格式：未改变。
- `nfc` 仍是终端协议和内部设备 id；只改变管理界面展示文案。
- 配置文件、部署方式、第三方调用程序：未改变。
- 继续使用 `.NET Framework 4.6/x86`，未新增依赖。

## 风险与注意事项

1. 自动复查只负责刷新健康检测状态，不会自动重发已经失败的业务请求；业务请求仍需按原流程重新发起。
2. 预览流本身已有 MJPEG 断线重连逻辑；本次改动主要解决管理界面按钮重叠和终端健康状态恢复刷新问题。
3. 当前构建环境中旧 Proxy exe 正在运行并锁定 `bin` 输出，因此验证输出写入了系统 Temp 目录。
4. 多 DPI 和真实终端上线恢复仍需现场验证，自动化测试不能完全替代视觉检查和设备联调。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 测试项目 `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 本次相关测试：6/6 通过。
- [x] 下拉框白底显示回归测试：已加入 `PreviewControl_UsesDeviceSelectorAndTwoActions`。
- [x] 非集成回归测试：59/59 通过。
- [!] 全量测试：59 通过，7 个 Integration 因当前测试宿主 `HttpListener` 抛出既有 `PlatformNotSupportedException` 失败。
- [x] `git diff --check`：通过，仅 LF/CRLF 规范化提示。
- [ ] 真实终端未就绪后恢复上线：待现场验证。
- [ ] 真实预览设备下拉启停：待现场验证。
- [ ] 100%、125%、150%、200% DPI：待现场验证。

## 下一步计划

- [ ] 启动新构建，确认预览控制卡片在实际窗口中无重叠。
- [ ] 断开/恢复终端网络，确认 5 秒短复查能自动刷新健康状态。
- [ ] 分别启动/停止六类本地预览，确认下方对应窗口和按钮状态同步。

## 回退方式

- 反向撤销本节涉及的 `HardwareHealthPanel.cs`、`MainForm.cs`、`TerminalHealthChecker.cs` 和测试修改即可。
- 若只需临时回退预览控制 UI，可恢复 `InitCardLayouts()` 中原 `SetupGrid2x6(tlpPreviewControl)` 与 12 个 `AddToGrid` 调用。
- DLL、配置文件和第三方调用程序无需回退。

# 健康检测手动刷新退避语义调整（2026-07-07）

## 当前阶段

- [x] 手动刷新改为只触发一次检测，不再无条件重置退避计数。
- [x] 检测成功且设备全部正常时，退避计数清零并恢复正常 5 分钟轮询。
- [x] 检测失败、终端不可达或设备仍异常时，保持退避已达上限状态，不重新从 5 秒开始自动复查。

## 本次修改内容

1. `TerminalHealthChecker.RequestCheck()` 保持内部刷新语义，用于启动、终端切换等场景，仍会重置退避链路。
2. 新增 `RequestCheck(resetRetryAttempt: false)` 路径供手动刷新使用，只立即检测一次，不清零 `_retryAttempt`。
3. `ProxyServer.RequestHealthCheck()` 改为调用不重置退避的手动刷新路径。
4. 管理界面日志文案改为“已手动触发一次刷新”，避免误解为重新开启自动复查链路。
5. 补充单元测试，验证手动刷新不重置退避，内部刷新仍可重置退避。

## 兼容性说明

- DLL、HTTP API、终端协议字段、配置文件和第三方调用方式均未改变。
- 只改变健康检测定时器内部调度策略和管理界面刷新按钮语义。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 测试项目 `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 定向测试：退避逻辑、手动刷新按钮、健康区域布局、预览下拉框回归 4/4 通过。
- [x] 非集成回归测试：60/60 通过。
- [ ] 真实终端不可达达到上限后手动刷新：待现场验证。

## 回退方式

- 将 `ProxyServer.RequestHealthCheck()` 恢复为 `_healthChecker?.RequestCheck()` 即可回到手动刷新重置退避链路的旧行为。

# 健康刷新按钮与预览下拉框布局修复（2026-07-07）

## 当前阶段

- [x] 修复硬件健康检测摘要文字挤压导致“刷新状态”按钮被遮挡的问题。
- [x] 修复预览设备下拉框显示高度不足、选项文字显示不全的问题。

## 本次修改内容

1. `HardwareHealthPanel` 标题栏从普通 `Panel` 改为三列 `TableLayoutPanel`，标题、摘要、刷新按钮分列布局。
2. 刷新按钮使用固定列宽并 `DockStyle.Fill`，避免被右侧摘要文字覆盖。
3. 预览设备下拉框所在行改为 44px 固定高度。
4. `ComboBox` 放入独立 `Panel` 容器，使用 `DockStyle.Top` 和固定 28px 高度，避免被 `TableLayoutPanel` 拉伸后裁剪文字。
5. 补充 UI 回归测试，覆盖刷新按钮固定列布局和下拉框非拉伸布局。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 测试项目 `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 定向 UI 测试：3/3 通过。
- [x] 非集成回归测试：61/61 通过。
- [ ] 真实窗口视觉检查：待验证。

## 回退方式

- 恢复 `HardwareHealthPanel` 标题栏为原 `Panel` 布局，并恢复 `MainForm.InitializePreviewSelectorControls()` 中下拉框直接加入 `tlpPreviewControl` 的旧方式。

# 健康刷新按钮与预览下拉框二次显示修复（2026-07-07）

## 当前阶段

- [x] 修复刷新按钮在摘要文字较长、高 DPI 或字体缩放下显示不全的问题。
- [x] 修复预览设备下拉框缺少明确边界、控件高度不足导致看起来显示不全的问题。

## 本次修改内容

1. `HardwareHealthPanel` 顶部标题栏高度从 32px 调整为 36px。
2. 刷新按钮固定列宽从 108px 调整为 136px，继续保留“刷新状态”完整文案，并增加 Tooltip。
3. 预览设备下拉框所在行从 44px 调整为 56px。
4. 下拉框外层容器增加 `FixedSingle` 边框，并增加内部留白。
5. `ComboBox` 高度从 28px 调整为 34px，`ItemHeight` 从 24px 调整为 28px。
6. 更新 UI 回归测试，覆盖刷新按钮完整文案、下拉框边框和下拉框高度。

## 兼容性说明

- DLL 导出函数、HTTP API、终端协议、配置文件和第三方调用方式均未改变。
- 本次只修改 WinForms 管理界面布局参数和对应 UI 测试。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 测试项目 `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 定向 UI 测试：3/3 通过。
- [x] 非集成回归测试：61/61 通过。
- [ ] 真实窗口视觉检查：待验证。
- [ ] 125%、150%、200% DPI 视觉检查：待验证。

## 回退方式

- 将 `HardwareHealthPanel` 顶部标题栏高度、刷新按钮列宽/文字恢复到上一版本。
- 将 `MainForm.InitializePreviewSelectorControls()` 中预览下拉框行高、外层 `Panel` 边框/留白、`ComboBox` 高度恢复到上一版本。

# 健康刷新按钮重叠遮挡修复（2026-07-07）

## 当前阶段

- [x] 修复“检测失败”摘要文字与“刷新状态”按钮在窄宽度/高 DPI 下仍然水平重叠的问题。

## 本次修改内容

1. `HardwareHealthPanel` 整体高度从 112px 调整为 132px。
2. 健康区头部从单行三列布局改为两行两列布局。
3. 第一行仅显示“硬件健康检测”标题和右侧固定宽度“刷新状态”按钮。
4. 第二行独立显示检测摘要，跨两列并启用 `AutoEllipsis`，摘要过长时只省略自身，不再挤压按钮。
5. `MainForm.InitializeHardwareHealthPanel()` 外层承载高度同步调整为 132px。
6. UI 回归测试新增约束：刷新按钮必须位于第一行固定按钮列，摘要必须位于第二行跨列显示。

## 兼容性说明

- DLL 导出函数、HTTP API、终端协议、健康检测退避策略和第三方调用方式均未改变。
- 只改变 WinForms 管理界面健康检测区域布局。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 测试项目 `Release|x86|net46`：通过，0 警告，0 错误。
- [x] 定向 UI 测试：3/3 通过。
- [x] 非集成回归测试：61/61 通过。
- [ ] 真实窗口视觉检查：待现场验证。

## 回退方式

- 将 `HardwareHealthPanel` 头部恢复为上一版单行布局，并将健康区高度恢复为 112px。

# 顶部状态栏高度坍塌修复（2026-07-07）

## 当前阶段

- [x] 修复硬件健康检测面板加入 `panelHeader` 后挤压顶部运行信息区的问题。
- [x] 修复主标题 `HZCYJKTHardWare后台服务` 被主动省略的问题。
- [x] 补充 UI 回归测试，覆盖标题可读高度、Header 下方区域顺延和健康面板不重叠。
- [ ] 真实窗口与多 DPI 视觉检查待现场验证。

## 本次修改内容

1. `MainForm.InitializeHardwareHealthPanel()` 在加入健康面板前记录原 Header 高度。
2. 新增 `EnsureHeaderHasHealthPanelSpace()`，将 `panelHeader` 高度和最小高度扩展为“原 Header 高度 + 健康面板高度”。
3. 为 `headerLayout` 设置最小高度，避免后续布局时再次被健康面板压缩到不可读。
4. 关闭 `lblPageTitle.AutoEllipsis`，常规窗口宽度下不再把主标题强制显示为省略号。
5. 更新 `HardwareHealthPanel_IsEmbeddedWithoutOverlappingHeaderOrContent()`，新增标题高度和下方操作区顺延断言。

## 涉及文件

- `MainForm.cs`：Header 高度保护。
- `MainForm.Designer.cs`：主标题取消 `AutoEllipsis`。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：顶部状态栏布局回归测试。
- `PROGRESS.md`：本次修复记录。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP API、终端协议、配置文件、部署方式和第三方调用行为：未改变。
- 只修改 WinForms 管理界面布局和 UI 测试。

## 风险与注意事项

1. 自动化测试验证了 WinForms 控件布局关系和标题可读高度，不能完全替代真实窗口截图检查。
2. 100%、125%、150%、200% DPI 下仍需人工确认顶部信息、健康卡片、刷新按钮和下方卡片没有视觉重叠。

## 验证状态

- [x] `git diff --check`：通过，仅 LF/CRLF 规范化提示。
- [x] C# Proxy + 测试项目 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 定向 UI 测试：4/4 通过。
- [x] 非集成回归测试：61/61 通过。
- [ ] 真实窗口视觉检查：待现场验证。
- [ ] 100%、125%、150%、200% DPI：待现场验证。

## 下一步计划

- [ ] 启动新构建，确认顶部运行信息区、健康检测区和下方操作卡片无裁切、无重叠。
- [ ] 在常用 DPI 缩放下检查主标题、监听地址、当前终端和运行状态均完整可读。

## 回退方式

- 撤销 `MainForm.cs` 中 `EnsureHeaderHasHealthPanelSpace()` 及其调用。
- 将 `MainForm.Designer.cs` 中 `lblPageTitle.AutoEllipsis` 恢复为 `true`。
- 撤销本次新增的 UI 测试断言即可恢复到本次修复前行为。

# 健康检测右侧按钮与设备短码折行修复（2026-07-07）

## 当前阶段

- [x] 修复健康检测右侧“刷新状态”按钮文字挤压问题。
- [x] 修复“检测失败/等待服务启动”等摘要与刷新按钮垂直区域过近导致的重叠风险。
- [x] 修复设备卡片中 `OCR`、`指纹`、`虹膜`、`人脸` 短码以及“待检测”状态被拆行的问题。
- [ ] 真实窗口和多 DPI 视觉检查待现场验证。

## 本次修改内容

1. `HardwareHealthPanel` 顶部 Header 右侧改为独立单列两行 `TableLayoutPanel`。
2. “刷新状态”按钮固定右对齐，并设置明确 `Size` 和 `MinimumSize`，避免文本换行挤压。
3. 摘要标签放在按钮下方独立行，单行显示并启用 `AutoEllipsis`，避免和按钮共享同一绘制区域。
4. 设备健康卡片短码列从 58px 调整为 76px，状态列从 92px 调整为 118px。
5. 设备短码标签启用 `AutoEllipsis` 并增加右侧间距，避免 `OCR` 被拆成 `OC/R`。
6. 补充 UI 回归测试，覆盖刷新按钮文本尺寸、按钮/摘要垂直关系、短码列和状态列单行空间。

## 涉及文件

- `UI/HardwareHealthPanel.cs`：右侧刷新/摘要布局、设备卡片列宽。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：按钮和设备卡片单行显示回归测试。
- `PROGRESS.md`：本次修复记录。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP API、终端协议、配置文件、部署方式和第三方调用行为：未改变。
- 只修改 WinForms 管理界面布局和 UI 测试。

## 风险与注意事项

1. 右侧状态区域固定宽度加大后，会减少左侧标题区域可用宽度；当前测试覆盖 1480px 面板宽度，真实更窄窗口仍需人工确认。
2. 自动化测试验证控件尺寸和布局关系，不能完全替代真实 DPI 下的截图检查。

## 验证状态

- [x] C# Proxy + 测试项目 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 定向 UI 测试：5/5 通过。
- [x] 非集成回归测试：62/62 通过。
- [ ] 真实窗口视觉检查：待现场验证。
- [ ] 100%、125%、150%、200% DPI：待现场验证。

## 下一步计划

- [ ] 启动新构建，确认右侧刷新按钮、摘要文字和五个设备卡片均无重叠、无拆行。
- [ ] 在常用 DPI 缩放下确认 `OCR`、`待检测`、刷新按钮完整可读。

## 回退方式

- 恢复 `HardwareHealthPanel` 顶部 Header 为上一版两列两行布局。
- 将设备短码列和状态列宽度恢复为 58px、92px。
- 撤销本次新增的 UI 测试断言即可恢复到本次修复前行为。

# 健康检测右侧严格间距与右对齐修复（2026-07-07）

## 当前阶段

- [x] 刷新按钮、检测摘要和最右侧人脸设备卡片已建立明确垂直安全间距。
- [x] 刷新按钮、检测摘要和最右侧人脸设备卡片右边界已严格对齐。
- [x] 健康面板外层高度同步增加，避免新增安全间距挤压设备卡片。
- [x] OCR 短码列保留加宽布局，防止在常规宽度下再次折行。
- [ ] 真实窗口和多 DPI 视觉检查待现场验证。

## 本次修改内容

1. `HardwareHealthPanel` 增加 `DefaultHeight = 156`，外层面板和 `MainForm` 承载高度统一使用该值。
2. 右侧状态区改为四段式垂直布局：刷新按钮、8px 间隔、摘要文本、16px 底部安全间隔。
3. Header 高度调整为 80px，设备卡片区域保留 76px 高度，避免卡片被新间距挤压。
4. UI 测试新增实际 Bounds 校验，验证按钮到摘要间距为至少 8px，摘要到卡片上边缘至少 16px。
5. UI 测试新增右边界校验，验证刷新按钮、摘要标签和最右侧卡片右边界一致。

## 涉及文件

- `UI/HardwareHealthPanel.cs`：健康面板高度、右侧状态区分段布局和安全间距。
- `MainForm.cs`：健康面板承载高度改为 `HardwareHealthPanel.DefaultHeight`。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：间距、右对齐和短码单行回归测试。
- `PROGRESS.md`：本次修复记录。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP API、终端协议、配置文件、部署方式和第三方调用行为：未改变。
- 只修改 WinForms 管理界面布局和 UI 测试。

## 风险与注意事项

1. 健康面板高度从 132px 增加到 156px，会让下方主内容整体下移 24px；这是为保证视觉安全间距做出的布局调整。
2. 自动化测试已验证 1480px 健康面板宽度下的控件边界关系，真实 DPI 缩放仍需人工截图确认。

## 验证状态

- [x] `git diff --check`：通过，仅 LF/CRLF 规范化提示。
- [x] C# Proxy + 测试项目 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 定向 UI 测试：5/5 通过。
- [x] 非集成回归测试：62/62 通过。
- [x] 实际控件边界诊断：按钮到摘要 8px，摘要到卡片 16px，按钮/摘要/最右卡片右边界均为 1480px。
- [ ] 真实窗口视觉检查：待现场验证。
- [ ] 100%、125%、150%、200% DPI：待现场验证。

## 下一步计划

- [ ] 启动新构建，确认右上角刷新按钮、红色检测摘要和人脸设备卡片无压边、无贴边、无重叠。
- [ ] 在常用 DPI 缩放下确认 `OCR`、`待检测`、刷新按钮和红色摘要完整可读。

## 回退方式

- 将 `HardwareHealthPanel.DefaultHeight` 恢复为上一版高度，并恢复右侧状态区为上一版两行布局。
- 将 `MainForm.InitializeHardwareHealthPanel()` 中健康面板高度恢复为旧固定值。
- 撤销本次新增的 Bounds 间距和右对齐测试断言即可恢复到本次修复前行为。

# 健康检测标题行横向并排重构（2026-07-07）

## 当前阶段

- [x] 将“硬件健康检测”标题、红色检测摘要和“刷新状态”按钮合并为同一 Header Row。
- [x] 右侧状态组件改为横向布局：摘要在左、刷新按钮在右，中间固定 12px 间距。
- [x] Header Row 与下方五个设备卡片之间保留 20px 安全间距。
- [x] 刷新按钮右边界与最右侧“人脸设备”卡片右边框对齐。
- [x] 保留设备短码列加宽和 `OCR` 单行显示约束。

## 本次修改内容

1. `HardwareHealthPanel` 顶部 Header 改为两列两行 `TableLayoutPanel`：左侧标题、右侧状态行，第二行仅作为 20px 网格安全间距。
2. 右侧状态行改为三列横向结构：摘要列、12px 固定间隔列、188px 刷新按钮列。
3. 摘要标签启用 `AutoEllipsis` 并右对齐，避免长文本挤压按钮或向下换行。
4. 刷新按钮使用固定按钮列与 `DockStyle.Fill`，保持文本完整、单行显示。
5. 健康面板默认高度从上一版 156px 回收为 132px，减少下方内容被整体下推的幅度。
6. UI 回归测试改为验证横向行结构、12px 横向间距、20px 下方安全间距、右边界对齐和 OCR 单行空间。

## 涉及文件

- `UI/HardwareHealthPanel.cs`：健康检测标题行、右侧状态行、按钮和卡片列宽布局。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：横向布局、间距、右对齐和单行显示回归测试。
- `PROGRESS.md`：本次重构记录。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP API、终端协议、配置文件、部署方式和第三方调用行为：未改变。
- 只修改 WinForms 管理界面布局和 UI 测试。
- 用户要求的“摘要在左、按钮在右”横向布局下，摘要尾字不会与按钮右边界重合；本次实现为右侧组件整体和按钮右边界对齐到“人脸设备”卡片右边框，摘要右边界与按钮左边界保持 12px 间距。

## 风险与注意事项

1. 右侧状态区使用 760px 固定列宽，适合当前宽屏控制台；极窄窗口下仍需现场确认标题和摘要是否需要进一步降级显示。
2. 自动化测试验证的是 WinForms 控件边界关系，不能完全替代真实 DPI 截图检查。

## 验证状态

- [x] C# Proxy + 测试项目 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 定向 UI 测试：5/5 通过。
- [x] 非集成回归测试：62/62 通过。
- [x] 实际控件边界断言：摘要到按钮 12px，Header Row 到卡片网格至少 20px，按钮和最右卡片右边界一致。
- [ ] 真实窗口视觉检查：待现场验证。
- [ ] 100%、125%、150%、200% DPI：待现场验证。

## 下一步计划

- [ ] 启动新构建，现场确认“硬件健康检测”标题、红色摘要、刷新按钮和五个设备卡片无压边、无贴边、无重叠。
- [ ] 在常用 DPI 缩放下确认 `OCR`、`待检测`、刷新按钮和红色摘要完整可读。

## 回退方式

- 将 `HardwareHealthPanel` Header 恢复为上一版垂直状态区布局，并将 `DefaultHeight` 恢复为 156px。
- 撤销本次横向行结构和 Bounds 间距测试断言即可回到上一版“严格垂直安全间距”方案。

# Proxy Release x86 重建 CS0579 修复（2026-07-07）

## 当前阶段

- [x] 修复 VS `Release x86` 全部重新生成时报 `TargetFrameworkAttribute` 重复的问题。
- [x] 同步保护测试项目，避免测试项目内 `_codex_build` 临时目录被 SDK-style 默认编译项扫描。

## 问题原因

- Proxy 项目是 SDK-style `net46` 项目，会自动生成 `.NETFramework,Version=v4.6.AssemblyAttributes.cs`。
- 项目目录下存在 `_codex_build` 临时构建目录，里面也有自动生成的 `.NETFramework,Version=v4.6.AssemblyAttributes.cs`。
- SDK-style 项目默认递归收集项目目录下的 `*.cs`，`_codex_build` 不属于默认排除目录，因此临时生成文件被误加入编译，和当前 `obj\x86\Release\net46` 下的生成文件形成 `TargetFrameworkAttribute` 重复。

## 本次修改内容

1. 在 `HZCYKJTHardWare.Proxy.csproj` 中将 `_codex_build\**\*` 加入 `DefaultItemExcludes`。
2. 在 `HZCYKJTHardWare.Proxy.csproj` 中显式移除 `_codex_build` 下的 `Compile`、`EmbeddedResource`、`None`、`Content` 项。
3. 在 `HZCYKJTHardWare.Proxy.Tests.csproj` 中做同样排除，避免测试项目默认构建路径出现同类问题。

## 涉及文件

- `HZCYKJTHardWare.Proxy.csproj`：排除项目内 `_codex_build` 临时构建目录。
- `HZCYKJTHardWare.Proxy.Tests.csproj`：排除测试项目内 `_codex_build` 临时构建目录。
- `PROGRESS.md`：本次构建修复记录。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP API、终端协议、配置文件、部署方式和第三方调用行为：未改变。
- 只修改 MSBuild 默认编译项收集规则，不改变源码运行逻辑。

## 验证状态

- [x] Proxy `Release|x86|net46` 默认路径 `dotnet build`：通过，0 警告、0 错误。
- [x] Proxy `Release|x86|net46` 默认路径 `Rebuild`：通过，0 警告、0 错误。
- [x] Tests `Release|x86|net46` 默认路径 `dotnet build`：通过，0 警告、0 错误。
- [x] 非集成回归测试：62/62 通过。
- [ ] Visual Studio 内手动“全部重新生成”：待现场确认。

## 回退方式

- 移除两个 `csproj` 中本次新增的 `_codex_build` 排除规则即可回到修改前构建项收集行为。

# 刷新状态按钮底部裁切修复（2026-07-07）

## 当前阶段

- [x] 修复“刷新状态”按钮底部文字/边框在当前布局下仍有轻微裁切的问题。
- [x] 保持红色检测摘要、刷新按钮和“人脸设备”卡片的横向布局与右边界对齐关系。
- [x] 保持 Header Row 到下方设备卡片网格的安全间距。

## 本次修改内容

1. `HardwareHealthPanel.DefaultHeight` 从 132px 调整为 140px，给顶部健康检测区域增加 8px 垂直空间。
2. `HeaderRowHeight` 从 40px 调整为 48px，避免右侧按钮行在字体缩放或高 DPI 下过紧。
3. `RefreshButtonHeight` 从 34px 调整为 40px，并作为按钮 `MinimumSize` 的高度来源。
4. 刷新按钮上下 `Margin` 调整为 4px，使按钮在 48px 行高中保持完整显示和垂直居中。
5. UI 测试新增按钮实际高度不得低于 40px 的断言，防止后续回退导致再次裁切。

## 涉及文件

- `UI/HardwareHealthPanel.cs`：健康检测 Header 行高、面板高度、刷新按钮最小高度和 Margin。
- `HZCYKJTHardWare.Proxy.Tests/UI/MainFormLogRenderingTests.cs`：刷新按钮高度回归断言。
- `PROGRESS.md`：本次修复记录。

## 兼容性说明

- DLL 导出函数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP API、终端协议、配置文件、部署方式和第三方调用行为：未改变。
- 只修改 WinForms 管理界面布局尺寸和 UI 测试。

## 验证状态

- [x] C# Proxy + 测试项目 `Release|x86|net46`：通过，0 警告、0 错误。
- [x] 定向 UI 测试：4/4 通过。
- [x] 非集成回归测试：62/62 通过。
- [ ] 真实窗口视觉检查：待现场验证。
- [ ] 100%、125%、150%、200% DPI：待现场验证。

## 回退方式

- 将 `HardwareHealthPanel.DefaultHeight`、`HeaderRowHeight`、`RefreshButtonHeight` 和刷新按钮 `Margin` 恢复为上一版数值。
- 移除本次新增的刷新按钮 40px 最小实际高度测试断言即可恢复到修改前测试约束。

# 第二批长期运行稳定性优化（2026-07-13）

## 当前阶段

- [x] 健康检查快速退避耗尽后恢复 5 分钟慢速探测。
- [x] 统一终端请求、Proxy 受理等待和 DLL 抓拍等待的分层超时预算。
- [x] 本机 DLL 回调增加有界短重试。
- [x] 预览恢复任务纳入统一后台任务生命周期。
- [x] 增加 5 分钟一次的轻量长稳指标。
- [ ] 真实双终端、断网和 24～72 小时长稳验证待执行。

## 本次修改内容

1. 健康检查失败时按 5/10/20/40/60 秒快速复查，之后不再永久停止，而是每 5 分钟慢速探测；探测成功立即恢复正常周期。
2. 新增内部 `OperationTimeouts`，人脸终端请求 3 秒、指纹和异步受理 4 秒、Proxy 等待 4.5 秒；DLL 人脸/指纹继续读取现有 5 秒配置，形成“终端 < Proxy < DLL”的预算关系。
3. 回调仅对 `HttpRequestException`、非主动取消的超时和 HTTP 503 重试，间隔为 50ms、200ms；4xx 不重试，重试复用同一请求体和 `request_id`，服务关闭时取消。
4. 终端切换预览恢复和 MJPEG 恢复在生产路径中统一进入 `ActiveTasksTracker`；关闭时取消预览生命周期、停止两个 Timer、停止播放器并等待已跟踪任务。
5. 每 5 分钟汇总 Private Bytes、Working Set、托管堆、线程、总句柄、GDI/USER 句柄、GC 次数、活动任务、预览会话、请求会话、日志积压/丢弃、磁盘空间和队列统计。
6. 本地 OCR/图片文件仍使用第三方传入的固定文件路径循环覆盖，本批不增加目录、历史文件、留存天数或清理策略。

## 涉及文件

- `Infrastructure/OperationTimeouts.cs`：内部超时预算。
- `Terminal/TerminalHealthChecker.cs`：失败后的慢速恢复探测。
- `Server/DllCommandHandler.cs`、`Server/Scheduler/WorkerExecutionEngine.cs`、`Server/Coordinator/BizOperationHandler.cs`：应用分层超时。
- `src/delphi_proxy.h`、`src/delphi_proxy.cpp`、`src/exports.cpp`：将现有抓拍超时配置传入内部 HTTP 调用，不改变导出 ABI。
- `Server/DllCallbackSender.cs`：有界回调短重试。
- `Preview/PreviewManager.cs`、`Server/Coordinator/SwitchCoordinator.cs`、`Server/Runtime/ProxyRuntime.cs`：预览任务取消、跟踪和关闭顺序。
- `Server/Runtime/RuntimeMetricsReporter.cs`、`Infrastructure/Logger.cs`：低频长稳指标和日志丢弃累计值。
- 对应测试文件：健康检查、超时预算、回调重试和运行指标回归测试。

## 兼容性说明

- DLL 导出函数名、参数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP 路径、请求/响应 JSON、终端协议和部署方式：未改变。
- C# Proxy 继续保持 `net46`、`x86`，DLL 继续保持 `Release|Win32`，未新增第三方依赖。
- 本批未修改 Delphi 示例；当前联调仍以 C# Demo 为准。

## 风险与注意事项

1. 超时值已按当前终端实测耗时留出余量，但仍需用真实设备的 P99 复核；若设备固件偶发超过 4 秒，应优先确认网络/设备原因后再调整常量。
2. 本机回调在连续超时场景下最多执行 3 次 HTTP 尝试；任务受回调监听并发和关闭令牌约束，不创建脱离生命周期的重试任务。
3. 长稳指标默认一分钟后首次记录、之后每 5 分钟记录两条 INFO 日志，日志增量较小。
4. 为避免共享关闭预算耗尽时晚到清理任务对已释放锁执行 `Release()`，`PreviewManager` 不主动释放唯一的 `_operationLock`；该对象与进程同生命周期，不会随业务次数增长。

## 验证状态

- [x] C# Proxy `Release|x86|net46`：编译通过，0 警告、0 错误。
- [x] C# 测试项目 `Release|x86|net46`：编译通过，0 警告、0 错误。
- [x] 非集成回归测试：79/79 通过。
- [x] DLL `Release|Win32`：编译通过，0 警告、0 错误。
- [ ] `HttpListener` 集成测试：当前测试宿主不支持，待正式 Windows 测试环境执行。
- [ ] C# Demo 双终端切换、设备断网/恢复、回调 503/超时异常注入：待现场验证。
- [ ] 新版本 24～72 小时资源曲线：待验证。

## 下一步计划

- [ ] 部署本批构建，用 C# Demo 验证人脸、指纹、OCR、NFC、授权及双终端切换。
- [ ] 验证终端断网超过快速重试周期后，5 分钟慢速探测能够自动恢复。
- [ ] 观察 `[长稳指标]` 中 Private Bytes、线程、句柄、GDI/USER 句柄和活动任务是否持续单向增长。
- [ ] 根据真实 P99 决定是否调整超时预算；没有资源曲线证据前，不实施指纹流式 Base64 重写或大对象池。

## 回退方式

- 第二批将作为独立 Git 提交；整体回退该提交即可恢复到 P0 + ID 卡兼容版本。
- 若只回退某一项，可分别恢复 `TerminalHealthChecker`、`OperationTimeouts` 调用点、`DllCallbackSender`、预览生命周期或 `RuntimeMetricsReporter` 对应文件。

# 指纹抓拍 GC 与固定覆盖优化（2026-07-13）

## 当前阶段

- [x] 人脸、指纹终端响应改为一次 JSON 解析并复用轻量结果模型。
- [x] 无畸变指纹 Base64 改为固定缓冲区解码，不再创建整图 LOH `byte[]`。
- [x] C# Demo 的无畸变目录输入自动转换为固定 BMP 文件路径。
- [x] 编译与非集成回归验证完成。
- [ ] 真实终端性能和 24～72 小时 GC/内存曲线待验证。

## 修改动机

1. 终端 2 压力日志显示，15:25～15:30 新增 95 次指纹抓拍时 Gen2 GC 增加 96 次，说明大 JSON/Base64 数据对 x86 进程形成明显完整代 GC 压力。
2. 指纹响应此前可能先通过 `ResultParser` 读取 `save_path`，再通过 `CallbackParser` 读取主图，保存无畸变图时又重新解析同一个响应。
3. 无畸变图此前通过 `Convert.FromBase64String` 创建 352×544、约 191KB 的整图数组，直接进入 LOH。
4. C# Demo 默认传入 `save_dir_hk=.\captures_hk` 目录，Proxy 因此按时间戳生成新文件，不符合当前循环覆盖测试目标。

## 本次修改内容

1. `ImageCallbackResult` 增加 `SavePath` 和 `UndistortedImageBase64`，`CallbackParser.ParseImageCapture()` 在同一次 `JObject.Parse` 中提取主图、保存路径和无畸变图。
2. 保持原有兼容顺序：无畸变字段优先读取顶层 `undistorted_image_base64`，为空时读取 `data.undistorted_image_base64`；`save_path` 继续只读取顶层字段。
3. 人脸、指纹同步抓拍各自只解析一次终端响应，后续保存逻辑复用同一结果对象。
4. 无畸变 BMP 使用 32KB Base64 输入缓冲区和单行像素缓冲区，按定位写入方式继续输出正高度、bottom-up、8 位灰度、256 级调色板 BMP。
5. 保留临时文件 + `MoveFileEx(REPLACE_EXISTING | WRITE_THROUGH)` 原子替换；无效 Base64 或写入失败时不破坏已有目标文件。
6. C# Demo 默认无畸变路径改为 `.\captures_hk\fingerprint_undistorted.bmp`；若用户输入没有扩展名的目录，抓拍前自动追加该固定文件名并回写文本框。

## 涉及文件

- `Parsing/CallbackParser.cs`：抓拍轻量结果模型和单次响应解析。
- `Server/Coordinator/BizOperationHandler.cs`：人脸、指纹保存逻辑复用解析结果。
- `Storage/FileSaver.cs`：无畸变 BMP 分块解码、单行缓冲和原子覆盖。
- `HZCYKJTHardWare.Proxy.Tests/Core/ImageCallbackParserTests.cs`：字段兼容与单模型解析测试。
- `HZCYKJTHardWare.Proxy.Tests/Storage/FileSaverTests.cs`：真实尺寸、方向、布局、原子覆盖及失败保留测试。
- `demo/CSharpThirdPartyDemo/HZCYKJTHardWare.CSharpDemo/MainForm.cs`、`MainForm.Designer.cs`：固定无畸变文件路径。
- `PROGRESS.md`：本次进度记录。

## 兼容性说明

- DLL 导出函数名、参数、调用约定、结构体、错误码和回调签名：未改变。
- Proxy HTTP 路径、请求/响应 JSON、终端协议和配置项：未改变。
- 主指纹图、人脸图、无畸变 BMP 的尺寸、方向和文件格式：未改变。
- Proxy 仍保留“调用方直接传目录时生成时间戳无畸变文件”的旧能力；本次只让 C# Demo 默认并自动传入完整文件路径，不影响其他第三方调用方。
- C# Proxy 和 Demo 继续保持 `net46`、`x86`，未新增第三方依赖。

## 风险与注意事项

1. 本次消除了无畸变图约 191KB 的解码数组及重复 JSON 解析，但终端原始响应字符串、主图 Base64 字符串仍是大对象，不能保证完全消除 Gen2 GC。
2. 流式 BMP 通过单行缓冲和随机定位保持原有正高度 bottom-up 字节布局；已用 2×2 和 352×544 数据验证首尾行方向。
3. 运行中的 Proxy 占用了默认 `bin\x86\Release\net46\HZCYKJTHardWare.Proxy.exe`，未强制结束压力测试进程；x86 Release 改用隔离输出目录完成编译验证。

## 验证状态

- [x] C# Proxy `Release|x86|net46` 隔离目录编译：通过，0 警告、0 错误。
- [x] C# 测试项目 `Release|x86|net46` 隔离目录编译：通过，0 警告、0 错误。
- [x] C# Demo `Release|x86|net46` 默认目录编译：通过，0 警告、0 错误。
- [x] 本次定向测试：8/8 通过。
- [x] 非集成回归测试：84/84 通过。
- [ ] `HttpListener` 集成测试：7 项因当前 VSTest 宿主抛出 `PlatformNotSupportedException` 未运行成功，待正式 Windows 测试宿主验证。
- [ ] 真实终端主图与无畸变图循环覆盖：待现场验证。
- [ ] 修改后指纹 P50/P95/P99 和 Gen2 GC 增量：待重新压力测试。
- [ ] 24～72 小时长稳验证：待执行。

## 下一步计划

- [ ] 当前压力进程自然结束后，将新 Proxy 和 C# Demo 部署到正式 x86 Release 目录。
- [ ] 连续抓拍至少 100 次，确认只保留 `fingerprint.jpg` 和 `fingerprint_undistorted.bmp` 两个固定结果文件。
- [ ] 对比同等请求数量下 `[长稳指标]` 的 Gen2 GC 增量、Private Bytes 和指纹耗时分位数。

## 回退方式

- 恢复 `CallbackParser.cs` 和 `BizOperationHandler.cs` 可回退单次响应解析。
- 恢复 `FileSaver.WriteBmpFile()` 可回退到整图 `Convert.FromBase64String` 解码。
- 恢复 C# Demo 的默认文本和路径规范化方法，可回退到目录下时间戳文件行为。

# P1-1 请求与 OCR 单次 JSON 解析（2026-07-13）

## 当前阶段

- [x] 上一批人脸/指纹解析与文件保存优化已建立独立 Git 基线：`b19c2fea`。
- [x] DLL 请求入口改为每个正常请求只构造一次 `JObject`。
- [x] OCR 元数据、证据图片和 `MRZ.json` 共用同一棵解析树。
- [x] Proxy/Test `Release|x86|net46` 编译和非集成回归完成。
- [ ] 真实 OCR、授权、预览和双终端联调待执行。

## 修改动机

1. `DllCommandHandler.HandleAsync` 此前分别调用 `JsonHelper.ExtractString` 读取 `request_id`、`save_dir` 和 `callback_url`，授权请求又为七个业务字段重复调用，每次调用都会重新执行 `JObject.Parse`。
2. OCR 回调此前先由 `CallbackParser.ParseOcrDocument` 解析元数据，保存证据图片时再次解析完整回调并逐项解析图片 JSON，生成 `MRZ.json` 时又第三次解析完整回调。
3. 当前系统虽然只有一个本机第三方客户端，但授权、预览和抓拍会长期重复进入同一入口；统一解析上下文可以减少短命 `JObject/JToken`，并让字段默认值和异常行为集中维护。

## 本次修改内容

1. 新增内部 `ParsedJsonBody`，保存原始请求文本及唯一 `JObject`；正常 JSON 的字符串和整数均从同一解析树读取。
2. 非法 JSON 不重复尝试解析；字符串字段继续使用原 `JsonHelper` 手工提取兜底，整数字段继续返回 `0`，保持原有兼容语义。
3. `DllCommandHandler` 将同一解析上下文传给终端切换、抓拍、流程、授权、终端预览和车牌预览处理；授权转发及请求登记继续保留原始 JSON 文本。
4. `CallbackParser.ParseOcrDocument` 增加内部已解析对象入口，原字符串入口继续保留并保持调用行为不变。
5. `TerminalCallbackHandler` 的 OCR 元数据、证据图片和 `MRZ.json` 改为读取同一个 `JObject`；`ocr_result.json` 仍保存终端原始 `bodyUtf8`，不重新序列化。
6. 证据图片字段继续兼容 `imageData/image_data/image_base64`、`lampType/lamp_type`、`imageType/image_type`；首张可见光、红外光、紫外光和人像去重规则未改变。

## 涉及文件

- `Parsing/ParsedJsonBody.cs`：单次解析上下文及非法 JSON 兼容兜底。
- `Parsing/JsonHelper.cs`：增加对已解析 `JObject` 的字段读取重载。
- `Server/DllCommandHandler.cs`：入口解析一次并向各业务处理器传递上下文。
- `Parsing/CallbackParser.cs`：OCR 复用已解析对象。
- `Server/TerminalCallbackHandler.cs`：OCR 证据图片和 MRZ 保存复用解析树。
- `HZCYKJTHardWare.Proxy.Tests/Core/ParsedJsonBodyTests.cs`：正常、非法、OCR 元数据及证据图片兼容测试。
- `PROGRESS.md`：本次进度记录。

## 兼容性说明

- DLL 导出函数、参数、返回值、调用约定、错误码和回调签名：未改变。
- DLL 与 Proxy 的 HTTP 路径、请求 JSON、响应 JSON 和终端协议：未改变。
- 授权请求原文、OCR 原始 JSON、证据图片名称、MRZ 文件结构及保存路径：未改变。
- C# Proxy 继续保持 `net46`、`x86`，未新增依赖。
- C# Demo 和 Delphi 示例：本批未修改。

## 风险与注意事项

1. 解析上下文只在单次 HTTP 请求或 OCR 回调期间持有，不进入静态缓存和长期队列。
2. 正常 JSON 行为由同一个 `JObject` 提供；非法 JSON 的字符串手工兜底与原实现一致，但非法输入仍不保证完整业务处理成功。
3. OCR 证据图片仍会持有终端回调中的 Base64 字符串直到本次回调保存完成；本次目标是消除重复解析，不改变终端报文格式。
4. 工作区既有 `src/http_client.cpp` 和 `todo.md` 修改未纳入本批，也未被覆盖。

## x64 Proxy 评估

- 当前项目文件只声明 `x86`，本次生成的 Proxy 为 PE32/x86。
- 使用命令行临时覆盖 `PlatformTarget=x64` 的探测构建返回 `NETSDK1047`，当前还原资产只有 `net46/win-x86`，说明 x64 不是只改一个编译开关，还需要独立的 x64 项目配置和依赖还原。
- 当前随程序分发的 `vlc/libvlc.dll` 和 `vlc/libvlccore.dll` 均为 x86；直接把 Proxy 改成 x64 会导致 VLC 加载失败，RTSP/车牌等依赖 VLC 的预览不可用。
- DLL 和 Proxy 通过本机 HTTP 通信，因此 DLL 保持 x86、Proxy 单独改成 x64 在架构上可行；但必须同时准备完整 x64 VLC 及插件目录，并验证 HWND 数值、预览窗口跨进程绑定和所有 native 调用。
- x64 的主要收益是扩大虚拟地址空间、降低 LOH 碎片导致内存耗尽的风险；不会降低终端硬件 HTTP 耗时，也不会自动减少 JSON/Base64 分配或 GC 次数。
- 当前 2.5 天压力测试内存能够回落且功能正常，因此本版本不建议直接切换 x64；应先完成当前 x86 优化和 24～72 小时长稳验证，再建立独立 x64 验证版本。

## 验证状态

- [x] Proxy `Release|x86|net46` 隔离输出编译：0 警告、0 错误。
- [x] Tests `Release|x86|net46` 隔离输出编译：0 警告、0 错误。
- [x] 新增单次解析定向测试：4/4 通过。
- [x] 非集成回归测试：88/88 通过。
- [ ] `HttpListener` 集成测试：已尝试 7 项，但当前 VSTest 宿主在测试初始化阶段抛出 `PlatformNotSupportedException`，尚未进入业务断言，待正式 Windows 测试宿主验证。
- [ ] C# Demo 真实终端 OCR、授权、抓拍、预览和双终端切换：待验证。
- [ ] 2 小时短稳和 24～72 小时长稳：待验证。

## 下一步计划

- [ ] 使用 C# Demo 验证授权请求原文、普通证件 OCR、ID 卡 OCR、证据图片和 `MRZ.json`。
- [ ] 回归人脸、指纹、终端切换以及三路预览启动/停止。
- [ ] P1-1 验证完成后进入 P2-1：WinHTTP 临时句柄 RAII。

## 回退方式

- 本批将作为独立 Git 提交；回退该提交即可恢复到 `b19c2fea`。
- 若只回退请求入口，可恢复 `ParsedJsonBody.cs`、`JsonHelper.cs` 和 `DllCommandHandler.cs`。
- 若只回退 OCR，可恢复 `CallbackParser.cs` 和 `TerminalCallbackHandler.cs`。

# Proxy x64 独立迁移方案 A（2026-07-13）

## 当前阶段

- [x] 保留现有 `Release|x86|net46` 构建与 x86 VLC 发布链。
- [x] 新增独立 `Debug/Release|x64|net46` 构建配置。
- [x] 新增同版本、同文件清单的 x64 VLC 自包含依赖。
- [x] 完成 x86/x64 编译、非集成回归和 PE 架构校验。
- [ ] x64 Proxy 真实终端、跨进程预览窗口及长稳验证待执行。

## 修改动机

1. x86 Proxy 在长时间处理 JSON、Base64 图片和预览资源时受约 2 GB 用户地址空间限制；x64 迁移可降低地址空间耗尽和大对象堆碎片导致进程失败的风险。
2. DLL 与 Proxy 通过本机 HTTP 通信，DLL、C# Demo 和第三方程序可以继续保持 x86，Proxy 可作为独立进程单独提供 x64 发布版本。
3. x64 不会降低终端硬件 HTTP 响应时间，也不会自动消除 JSON/Base64 分配，因此本次只建立独立可回退的发布链，不替换正在运行的 x86 版本。

## 本次修改内容

1. Proxy 与测试项目增加条件化 `PlatformTarget`、`x86;x64` 平台清单和 `Prefer32Bit=false`；两个 Proxy solution 同步增加 Debug/Release x64 映射。
2. x86 构建继续从项目 `vlc` 目录发布依赖；x64 构建从 `vlc-x64` 读取，并显式映射到输出目录 `vlc`，保持 `VlcPreviewPlayer` 的运行时加载路径不变。
3. `vlc-x64` 采用本机 `D:\VLC` 的 VLC 3.0.23，按现有 x86 VLC 清单复制 326 个文件；继续排除已知不支持的 `plugins/access/libsftp_plugin.dll`。
4. Proxy 内部将预览 `hwnd` 从 32 位整数读取调整为 64 位整数读取，避免 x64 Proxy 截断窗口句柄；HTTP 字段名、JSON 数值格式和 DLL 请求协议均未改变。
5. 启动日志增加进程架构、操作系统架构和 CLR 版本，便于现场确认实际运行的是 x86 还是 x64 发布包。
6. 内存边界和流式保存注释调整为架构中性表述，原有 16 MB 请求体上限及文件保存行为不变。

## 涉及文件

- `HZCYKJTHardWare.Proxy.csproj`：双平台配置及 x86/x64 VLC 条件发布。
- `HZCYKJTHardWare.Proxy.Tests.csproj`：测试项目双平台配置。
- 两个 `HZCYKJTHardWare.Proxy.sln`：增加 Debug/Release x64 solution 映射。
- `vlc-x64/`：VLC 3.0.23 x64 自包含运行文件。
- `Parsing/JsonHelper.cs`、`Parsing/ParsedJsonBody.cs`：内部 64 位整数读取。
- `Server/DllCommandHandler.cs`：预览 HWND 使用 64 位解析结果。
- `MainForm.cs`：启动架构日志。
- `Server/HttpProtocolHandler.cs`、`Storage/FileSaver.cs`：架构中性注释。
- `HZCYKJTHardWare.Proxy.Tests/Core/ParsedJsonBodyTests.cs`：64 位 HWND 数值回归测试。
- `PROGRESS.md`：本次迁移记录。

## 兼容性说明

- DLL 导出函数、参数、调用约定、结构体、错误码和回调签名：未改变；DLL 继续为 x86。
- C# Demo 与 Delphi 第三方程序：未修改，继续为 x86；通过 HTTP 调用 x64 Proxy 不受进程位数限制。
- Proxy HTTP 路径、请求/响应 JSON、终端协议和配置文件：未改变。
- TargetFramework：继续为 `net46`，实际部署在已安装 .NET Framework 4.8 的 Windows 10 x64 上，不引入新的运行库要求。
- 发布方式：x86 和 x64 为两个独立输出目录，不应混合复制 Proxy EXE、VLC 核心或插件。

## 风险与注意事项

1. x64 VLC 依赖约 103 MB；部署时必须整体复制输出目录中的 `vlc`，不得与旧 x86 `vlc` 合并覆盖。
2. 跨进程 HWND 在 DLL 请求中仍保持原 JSON 数值字段；本次已避免 Proxy 侧 32 位截断，但真实的 `SetParent/MoveWindow`、窗口销毁时序和高 DPI 表现仍需现场验证。
3. 所有 VLC DLL 已静态校验为 AMD64，但仅靠 PE 校验不能替代真实 RTSP/车牌预览启动、停止、终端切换和 Proxy 退出验证。
4. 当前 x86 Proxy 压力测试进程占用默认 Release 文件，x86 回归使用隔离输出完成，未结束或替换正在运行的实例。
5. x64 扩大的是地址空间余量；终端接口约 300 ms 的硬件处理耗时、网络抖动和业务串行规则不会因位数变化而直接加速。

## 验证状态

- [x] Proxy + Tests `Release|x64|net46`：编译通过。
- [x] x64 非集成回归：89/89 通过。
- [x] Proxy + Tests `Release|x86|net46` 隔离输出：编译通过，0 警告、0 错误。
- [x] x86 非集成回归：89/89 通过。
- [x] PE 架构：x86 Proxy=`0x014C`，x64 Proxy=`0x8664`。
- [x] VLC 文件清单：x86/x64 均 326 个文件、324 个 DLL；x86 DLL 全部 `0x014C`，x64 DLL 全部 `0x8664`，异常混用 0 个。
- [x] VLC 核心版本：x86/x64 均为 3.0.23；两端输出均无 `libsftp_plugin.dll`。
- [ ] `HttpListener` 集成测试：x64 已尝试 7 项，但当前 VSTest 宿主在创建 Mock Server 时抛出 `PlatformNotSupportedException`，尚未进入 Proxy 业务断言。
- [ ] x64 Proxy 启动、真实双终端业务和预览窗口嵌入：待现场验证。
- [ ] x64 2 小时短稳及 24～72 小时长稳：待执行。

## 下一步计划

- [ ] 在不替换生产 x86 目录的前提下，将 x64 Release 整体复制到独立测试目录并启动，确认日志显示 `进程架构=x64`。
- [ ] 使用现有 x86 C# Demo 回归终端切换、人脸、指纹、OCR、授权及流程开始/结束。
- [ ] 分别验证 RTSP/车牌等 VLC 预览启动、停止、窗口切换、重复初始化和 Proxy 正常退出。
- [ ] 完成 2 小时短稳后再执行 24～72 小时长稳，对比 Private Bytes、Managed Heap、Gen2 GC、线程、句柄和 GDI/USER 句柄趋势。

## 回退方式

- 本批作为独立 Git 提交；整体回退该提交即可恢复为仅 x86 Proxy 的构建和发布方式。
- 部署回退只需停止 x64 Proxy 并重新启动原 x86 发布目录，不需要替换 DLL、C# Demo、配置文件或修改第三方调用。

# P1 GC 压力优化：抓拍响应流式解析（方案 B，2026-07-14，已回退）

## 当前阶段

- [x] 2026-07-15 根据约 18 小时真实压力测试结果，停止采用方案 B 并恢复原抓拍响应路径。
- [x] 人脸、指纹同步抓拍改为 HTTP 响应流直接解析。
- [x] 去除抓拍成功路径中的完整响应字符串和 `JObject/JToken` 对象树。
- [x] 保持现有 Base64 流式文件写入、原子覆盖和双终端路由逻辑。
- [x] 完成 x86/x64 Release 编译和非集成回归测试。
- [ ] 真实终端短压测和 24～72 小时 GC/内存长稳对比待执行。

## 本次修改内容

1. `TerminalClient` 新增抓拍专用 `PostImageCaptureAsync`，使用 `HttpCompletionOption.ResponseHeadersRead`，不再通过 `ReadAsStringAsync` 缓冲完整成功响应。
2. 新增 `ImageCaptureStreamParser`，通过 `JsonTextReader` 从终端响应流解析轻量 `ImageCallbackResult`；Base64 字段仍交给现有流式文件写入逻辑。
3. 保留历史字段优先级：人脸优先 `data.face_capture`；指纹主图优先 `data.image_base64`；无畸变图仍优先根字段；MIME 默认值保持不变。
4. 非 2xx 响应只读取最多 1024 字节作为日志预览，并继续受抓拍超时/终端批次取消令牌约束，避免响应头返回后错误响应体无限等待。
5. 指纹主图保存完成后立即解除主 Base64 字符串引用，再处理无畸变图，缩短大字符串存活时间和跨代晋升窗口。
6. OCR、授权、预览、健康检查等通用 `PostJsonAsync/GetJsonAsync` 未修改，降低本批回归范围。

## 涉及文件

- `Parsing/ImageCaptureStreamParser.cs`：抓拍响应流式轻量解析和历史字段优先级。
- `Terminal/TerminalClient.cs`：抓拍专用 `ResponseHeadersRead` 请求、超时和错误预览边界。
- `Server/Coordinator/BizOperationHandler.cs`：人脸/指纹切换至抓拍专用请求路径，并缩短指纹主图字符串生命周期。
- `HZCYKJTHardWare.Proxy.Tests/Core/ImageCallbackParserTests.cs`：指纹、人脸字段优先级及非法 JSON 尾部兼容测试。
- `PROGRESS.md`：本阶段实施和验证记录。

## 兼容性说明

- DLL 导出函数、参数、调用约定、结构体、错误码、回调签名：未改变。
- 第三方 Delphi/C# 调用方式、Proxy 本机 HTTP 路径和 JSON 格式：未改变。
- 终端请求路径、请求体、字段名、字段优先级、超时配置：未改变。
- 图片命名、`save_dir/save_dir_hk` 目录或精确文件行为、循环覆盖和原子替换：未改变。
- x86/x64 Proxy 共用同一托管代码路径；TargetFramework 继续为 `net46`，未新增依赖。

## 风险与注意事项

1. `JsonTextReader` 仍需为每个 Base64 JSON 字段创建一个字符串；本批消除的是完整 HTTP 响应字符串副本和 JSON DOM，并非完全消除 LOH 分配。
2. 非法 JSON 只能保留解析异常前已经读取到的字段；正常终端 JSON 行为及历史字段优先级已由测试覆盖。
3. 真正的 Gen0/Gen1/Gen2 降幅取决于终端图片大小和字段组合，不能用单元测试结果替代真实压力数据。
4. 本批没有使用 `GC.Collect`、大数组对象池或扩大抓拍队列，也没有改变终端硬件串行处理规则，因此不应预期终端 300～700 ms 硬件耗时显著下降。
5. 工作区原有 `src/http_client.cpp`、`todo.md` 和其他未跟踪文件未被覆盖，也不属于本批修改。

## 验证状态

- [x] Proxy + Tests `Release|x86|net46`：0 warning，0 error。
- [x] Proxy + Tests `Release|x64|net46`：0 warning，0 error。
- [x] x86 非集成回归：92/92 通过。
- [x] x64 非集成回归：92/92 通过。
- [x] 新增流式解析定向测试：3/3 通过。
- [ ] 7 项 `HttpListener` 集成测试：已执行，但测试宿主在 `MockTerminalServer` 初始化时抛出 `PlatformNotSupportedException`，未进入业务断言。
- [ ] C# Demo + 真实双终端的人脸、普通指纹、无畸变指纹、切换和异常超时：待验证。
- [ ] 使用相同抓拍频率完成至少 2 小时对照测试，并比较每千次请求的 Gen0/Gen1/Gen2、Private Bytes、Managed Heap、P95/P99 和成功率：待验证。
- [ ] 24～72 小时长稳：待验证。

## 下一步计划

- [ ] 先使用 x86 版本执行与 2026-07-13 基线相同的压力脚本，确认功能和字段兼容。
- [ ] 以每千次抓拍为单位比较 GC 次数，重点确认 Gen2/请求是否明显下降；30% 为观察目标，不作为未经实测的结论。
- [ ] x86 短压测通过后，再使用独立 x64 发布目录执行相同测试，避免混用 VLC 依赖。
- [ ] 单独复核“停止抓拍后 `ReleaseSdk active=1`”生命周期问题，不与本批 GC 代码合并提交。

## 回退方式

- 本批可作为独立 Git 提交整体回退，不需要回退 x64 发布配置提交 `ffda563a`。
- 单独恢复 `BizOperationHandler` 的两处调用为 `PostJsonAsync + CallbackParser.ParseImageCapture`，并删除抓拍专用方法和流解析文件，即可恢复原抓拍响应路径。

## 实测结论与回退状态（2026-07-15）

1. x86 方案 B 从 2026-07-14 14:28 至 2026-07-15 08:44 连续运行约 18 小时 15 分钟，功能、双终端隔离、队列和句柄状态正常。
2. 约 135,146 次抓拍期间，Gen0/Gen1/Gen2 增量分别约为 20,762,107 / 372,170 / 221,094；折合每次抓拍约 153.63 / 2.75 / 1.64 次，未达到降低 GC 压力的目标。
3. 人脸平均耗时约 144 ms；指纹平均耗时约 720 ms、P95 约 1032 ms，方案 B 未形成可确认的响应时间收益。
4. 已定向恢复人脸和指纹的 `PostJsonAsync + CallbackParser.ParseImageCapture` 路径，删除 `PostImageCaptureAsync`、`ImageCaptureStreamParser` 及其 3 项专用测试。
5. 本次回退不涉及 DLL ABI、Proxy HTTP 协议、图片命名和覆盖逻辑，也不回退 P0、x64 独立发布、VLC 目录修复或 `ReleaseSdk active=1` 修复。
6. 方案 B 的历史记录保留用于后续分析，但不再作为当前版本默认实现。

### 验证状态

- [x] Proxy + Tests `Release|x86|net46`：编译通过，0 warning、0 error；非 Integration 回归 91/91 通过。
- [x] Proxy + Tests `Release|x64|net46`：编译通过，0 error；非 Integration 回归 91/91 通过。NuGet 漏洞数据源不可访问产生 2 个 `NU1900` 环境警告，不影响本地缓存依赖和生成结果。
- [ ] 真实终端 2 回退后 30～60 分钟短压测：待执行。
- [ ] 回退版本 24～72 小时长稳：待执行。

### 后续如需重新验证方案 B

- 如后续需要复现实验，只能在独立分支重新应用方案 B，不应直接替换当前发布目录。
- 恢复实验时需同时恢复流式解析器、专用终端请求方法、Handler 调用和定向测试，避免形成不完整路径。

# x64 VLC 发布与 ReleaseSdk active=1 修复（2026-07-14）

## 当前阶段

- [x] x64 VLC 改为独立 `vlc-x64` 发布目录，避免与 x86 `vlc` 混用。
- [x] VLC 加载前增加 PE Machine 校验，错误位数的 DLL 不再交给 `LoadLibraryEx`。
- [x] `ReleaseSdk` 在途调用等待预算由原来的约 3.5 秒调整为 20 秒。
- [x] 在途调用增加导出函数名、线程 ID 和调用持续时间诊断。
- [x] 完成 x86/x64 Proxy、Win32 DLL 隔离编译及非集成回归测试。
- [ ] 真实 x64 VLC 预览和真实 `active=1` 退出时序待现场验证。

## 本次修改内容

1. x86 构建继续将 x86 VLC 发布到输出目录 `vlc`；x64 构建将 x64 VLC 发布到输出目录 `vlc-x64`，不再把两种架构映射为同一个目录名。
2. x64 Proxy 优先查找 `vlc-x64`，随后保留对旧 `vlc` 目录的兼容探测；所有候选目录均先读取 `libvlccore.dll` 和 `libvlc.dll` 的 PE Machine，只有与当前进程一致时才加载。
3. x64 进程要求 `0x8664`，x86 进程要求 `0x014C`；不兼容或无效 PE 文件会被跳过并写入清晰诊断，避免再次出现 Windows error 193。
4. `ReleaseSdk` 进入 Releasing 后继续拒绝新业务调用，但允许已经进入的调用最多用 20 秒自然退出；正常 `active=0` 时不会增加退出等待。
5. 每个受保护的 DLL 导出调用在内部记录导出函数名、线程 ID 和开始时间；20 秒仍未退出时记录 `active`、函数名、线程 ID 和 `age_ms`，随后恢复 Running 状态，不强制销毁仍被使用的资源。

## 涉及文件

- `HZCYKJTHardWare.Proxy.csproj`：x86/x64 VLC 输出目录隔离及 SFTP 插件删除路径条件化。
- `Preview/VlcPreviewPlayer.cs`：架构相关目录优先级、PE Machine 校验和错误架构跳过逻辑。
- `HZCYKJTHardWare.Proxy.Tests/Preview/PlatePreviewConfigurationTests.cs`：目录优先级和交叉架构 PE 校验回归测试。
- `src/sdk_runtime.h`、`src/sdk_runtime.cpp`：在途导出调用诊断上下文。
- `src/exports.cpp`：导出函数名登记、20 秒安全排空和超时诊断。
- `PROGRESS.md`：本次修改与验证记录。

## 兼容性说明

- DLL 导出函数名、参数、`__stdcall` 调用约定、返回值、错误码和回调签名：未改变。
- Proxy HTTP 路径、请求/响应 JSON、终端协议、配置文件和第三方调用方式：未改变。
- DLL 和 C# Demo 继续保持 x86；Proxy 仍可分别构建 x86/x64，TargetFramework 继续为 `net46`。
- x86 发布目录行为不变，仍使用 `vlc`；x64 部署时必须整体复制新输出中的 `vlc-x64`，旧 x86 `vlc` 可以保留但不会被错误加载。
- `ReleaseSdk` 正常退出路径无新增等待；只有确实存在在途调用时才等待，最长 20 秒。

## 风险与注意事项

1. 若第三方在调用 `ReleaseSdk` 时仍持续执行一个真正卡死的导出调用，`ReleaseSdk` 最长会等待 20 秒后返回失败；这是避免并发释放导致崩溃的安全边界，不会强制终止业务线程。
2. 20 秒超时后 SDK 恢复 Running，调用方可停止对应业务线程后再次执行 `ReleaseSdk`；日志中的函数名、线程 ID 和 `age_ms` 用于定位未退出调用。
3. x64 发布包目录名已从输出 `vlc` 改为 `vlc-x64`，部署脚本若只复制固定的 `vlc` 目录，需要同步改为复制整个 x64 输出目录。
4. PE 校验只能证明 DLL 位数匹配，不能替代真实 RTSP/车牌预览的启动、停止、切换和窗口生命周期验证。
5. 本次隔离构建未替换或停止正在压力测试的 x86 运行目录和进程。

## 验证状态

- [x] Proxy + Tests `Release|x64|net46`：编译通过，0 warning，0 error。
- [x] x64 非集成回归：94/94 通过。
- [x] x64 输出布局：存在 `vlc-x64`，不存在项目发布的 `vlc`；`libvlccore.dll` 和 `libvlc.dll` 均为 `0x8664`；无 `libsftp_plugin.dll`。
- [x] Proxy + Tests `Release|x86|net46`：编译通过，0 warning，0 error。
- [x] x86 非集成回归：94/94 通过。
- [x] x86 输出布局：存在 `vlc`，不存在 `vlc-x64`；两个 VLC 核心 DLL 均为 `0x014C`；无 `libsftp_plugin.dll`。
- [x] DLL `Release|Win32` 隔离编译：通过，0 warning，0 error；产物为 x86。
- [x] DLL 导出表：24 个既有导出名称和 stdcall 参数装饰保持不变。
- [ ] x64 Proxy 真实 VLC 预览：待现场验证。
- [ ] 停止抓拍后立即退出、存在一个在途抓拍时退出、异常断网时退出：待 C# Demo 验证。

## 下一步计划

- [ ] 使用独立 x64 发布目录验证三路 VLC 预览的启动、停止、重复初始化和 Proxy 正常退出。
- [ ] 在 C# Demo 中分别验证 `active=0` 正常退出、最后一次抓拍仍在返回时退出以及终端断网时退出。
- [ ] 若仍出现 20 秒超时，根据新增 `details` 日志直接定位具体导出函数和调用线程，再决定是否需要对该调用增加主动取消，不直接扩大 Release 强制清理范围。
- [ ] 当前 x86 方案 B 压力测试结束后，对比 Gen0/Gen1/Gen2、Private Bytes、Managed Heap、成功率和 P95/P99。

## 回退方式

- VLC 部分可恢复 `HZCYKJTHardWare.Proxy.csproj` 的 x64 输出映射和 `VlcPreviewPlayer` 的目录/PE 校验逻辑，回到旧的统一 `vlc` 输出行为。
- Release 部分可恢复 `SdkRuntime` 的无名称计数和 `ReleaseSdk` 原等待预算；不涉及 ABI、配置或数据迁移。
- 部署回退只需停止新 x64 Proxy 并恢复上一版独立 x64 发布目录；DLL、C# Demo 和配置文件无需迁移。

# DLL 日志 UTF-8 BOM 与授权调用方编码核对（2026-07-15）

## 当前阶段

- [x] 对 x64 Proxy 测试产生的 DLL/EXE 日志执行原始字节级编码检查。
- [x] 确认 DLL 日志主体为无 BOM UTF-8，仅两条授权日志混入 GBK 姓名字节。
- [x] 确认姓名原始字节 `B8 DB C2 C3 BF CD B6 FE` 按 GBK 解码为“港旅客二”，乱码在 DLL 导出调用边界已经产生，不是 x64 Proxy 转换导致。
- [x] 确认仓库 C# Demo 从最初提交起即通过 `Utf8NativeString` 为授权参数分配 UTF-8 非托管内存。
- [x] Native Logger 在创建空白新日志文件时写入 UTF-8 BOM，初始化和跨日轮转路径均覆盖。
- [ ] 现场实际调用 Demo/压力工具二进制无法在当前工作机访问，待使用重新生成产物的 SHA-256 核对。

## 本次修改内容

1. Native Logger 打开日志文件后检查实际文件长度；仅当长度为 0 时写入 `EF BB BF`。
2. 已存在且非空的日志继续原样追加，不在文件中间插入 BOM，也不重写历史日志。
3. C# Demo 源码无需修改；重新生成 x86 Release 产物并提供哈希用于现场核对。
4. 未对非 UTF-8 `char*` 自动猜测或转换为 GBK，避免在 DLL ABI 边界引入不确定编码行为。

## 涉及文件

- `src/logger.cpp`：空白新日志文件写入 UTF-8 BOM。
- `PROGRESS.md`、根目录 `todo.md`：编码事实、兼容性和验证记录。

## 兼容性说明

- DLL 导出函数名、参数、调用约定、结构体、错误码和回调签名均未改变。
- Proxy x86/x64、HTTP 请求/响应、终端协议和授权字段格式均未改变。
- UTF-8 BOM 仅增加在新日志文件开头的 3 个字节，常规 UTF-8/Unicode 日志查看器可直接识别。
- C# Demo 仍为 x86，并继续显式传入 UTF-8；不增加 Delphi 示例修改。

## 风险与注意事项

1. BOM 只能解决日志查看器编码识别，不能修复调用方已经传入的 GBK数据。
2. 当前日期已经存在的非空日志不会补写 BOM；需等跨日创建新文件，或停机后归档旧日志再启动验证。
3. 如果现场实际调用方继续传入 GBK，授权姓名仍会在 DLL→Proxy→终端链路损坏；必须替换为当前 UTF-8 Demo/调用代码，或后续单独确认是否增加显式编码配置。
4. 不自动将“看起来像 GBK”的字节转码，避免 ASCII、其他代码页或本来合法 UTF-8 被误判。

## 验证状态

- [x] DLL `Release|Win32`：编译通过，0 warning、0 error；产物保持 x86。
- [x] C# Demo `Release|x86|net46`：隔离编译通过，0 warning、0 error。
- [x] C# Demo UTF-8 字节验证：“港旅客二”编码为 `E6 B8 AF E6 97 85 E5 AE A2 E4 BA 8C`，UTF-8 反解一致。
- [x] 空白日志 BOM、已有日志不重复写 BOM：使用新 DLL 在两个独立 32 位进程连续初始化同一日志，文件头为 `EF BB BF`、BOM 总数为 1、严格 UTF-8 解码通过。
- [x] 现场核对 SHA-256：C# Demo=`B78D958AEBA52BE0230B92295682BC4B20F0F9B9653F5C1C32244603B59B02E9`；DLL=`6ECC8BD7D2F69CC2272C9A727DF306B606487E445FBB4EF2359B9CA03ECD7C2D`。
- [ ] 使用“港旅客二”执行真实授权，确认 DLL、EXE、终端回调均保持中文：待现场验证。

## 回退方式

- 删除 `src/logger.cpp` 的 `WriteUtf8BomIfEmpty` 及两个调用点即可恢复无 BOM 日志，不涉及日志数据迁移或业务代码回退。

# DLL 第三方输入编码自动兼容（2026-07-15）

## 当前阶段

- [x] 已确认正式 Delphi 第三方传给 DLL 的 `char*` 参数为 GBK。
- [x] 已确认 DLL 返回第三方的回调 `eventJson` 继续使用 UTF-8，不做 GBK 转换。
- [x] 采用方案 C：默认自动识别，同时允许显式强制 `gbk` 或 `utf8`。

## 本次修改内容

1. `HZCYKJTHardWare.json` 新增顶层配置 `third_party_input_encoding`，支持：
   - `auto`：ASCII 原样使用；非 ASCII 先严格校验 UTF-8，失败后按 Windows CP936/GBK 转为 UTF-8。
   - `gbk`：将非 ASCII 输入按 CP936/GBK 强制转换为 UTF-8。
   - `utf8`：严格校验 UTF-8，不合法时返回参数错误。
2. 配置缺失时默认 `auto`，旧配置文件无需立即迁移。
3. 在 DLL 公开业务接口边界对输入只归一化一次，覆盖：
   - `StartProcess`、人脸、指纹、虹膜、OCR、NFC 的路径参数；
   - `RequestAuthorize` 的 `ZJHM`、`ZJLB`、`GJDQDM`、`XM`、`XB`、`CSRQ`、`KADM`。
4. 转换失败仅记录字段名和编码模式，不记录原始无效字节；不改变回调编码。

## 涉及文件

- `src/config_manager.h/.cpp`：配置默认值、校验和访问器。
- `src/path_helper.h/.cpp`：严格 UTF-8 校验及 CP936→UTF-8 转换。
- `src/hzsjkjt_context.h/.cpp`：运行期只读编码模式及 Release 重置。
- `src/exports.cpp`：公开业务接口输入边界归一化。
- `HZCYKJTHardWare.json`：默认设置为 `auto`。
- `todo.md`、本文件：实施和验证记录。

## 兼容性说明

- DLL 导出函数名、参数、`__stdcall`、结构体、返回值、错误码和回调签名均未改变。
- DLL 内部文本、日志、DLL→Proxy HTTP/JSON、Proxy→终端协议以及第三方回调继续统一为 UTF-8。
- C# Demo 继续传入 UTF-8；默认 `auto` 可直接识别，无需修改源码。
- 正式 Delphi 第三方可使用默认 `auto`，也可在部署确认后设置为 `gbk` 获得确定性转换。
- 未修改 Delphi 示例源码及两份 Delphi Demo 配置副本；缺少新字段时自动使用 `auto`。

## 风险与注意事项

1. `char*` 不携带编码元数据；`auto` 采用“严格 UTF-8 优先”规则，极少数恰好构成合法 UTF-8 的 GBK 字节序列理论上仍可能误判。
2. 正式部署调用方已明确固定为 GBK 时，建议将配置设为 `gbk`，消除自动识别歧义；C# Demo 测试环境保持 `auto` 或 `utf8`。
3. `gbk` 模式不能用于传入 UTF-8 中文的调用方，否则 UTF-8 字节会被当作 CP936 转换。
4. `utf8` 模式收到 GBK 中文时会在发起 HTTP 请求前返回失败，并记录字段名，不会把损坏文本发送给 Proxy。

## 验证状态

- [x] `git diff --check`：无空白错误。
- [x] DLL `Release|Win32`：编译通过，0 warning、0 error。
- [x] x86 假 Proxy 运行验证：`auto+GBK`，中文路径与“港旅客二”均以严格 UTF-8 到达 HTTP 请求体。
- [x] x86 假 Proxy 运行验证：`auto+UTF-8`，中文路径与姓名保持不变。
- [x] x86 假 Proxy 运行验证：`gbk+GBK` 与 `utf8+UTF-8` 均通过。
- [x] 删除配置字段后验证默认行为：按 `auto` 成功处理 GBK。
- [ ] 正式 Delphi 第三方 + 真实 Proxy + 终端授权全链路：待现场验证。
- [ ] 中文保存路径的人脸、指纹、OCR、NFC 文件落盘：待现场验证。

## 下一步计划

- [ ] 正式环境先使用 `auto` 复测日志中的“港旅客二”，确认 DLL 与 EXE 日志均为正常中文。
- [ ] 若正式第三方始终固定 GBK，将生产配置锁定为 `gbk` 后执行 24～72 小时长稳测试。

## 回退方式

- 配置回退：将 `third_party_input_encoding` 设置为 `utf8`，即可恢复只接受 UTF-8 的行为。
- 代码回退：移除配置字段、上下文字段、`NormalizeExternalTextToUtf8` 和各导出入口归一化调用；不涉及 ABI、数据文件或 Proxy 回退。

# Start/End 终端推送控制语义修正（2026-07-15）

## 当前阶段

- [x] `Start/End` 定义修正为“通知终端开始/停止向回调地址推送数据”。
- [x] Proxy 不再把本地 Session 状态作为回调接收开关。
- [x] DLL 不再在 `EndProcess` 时取消独立 OCR/NFC/虹膜/授权请求，也不再用 `process_active` 拒绝流程回调。
- [x] 预览、人脸抓拍和指纹抓拍仍与 `Start/End` 完全独立。
- [ ] 真实双终端、真实 VLC 预览及 24～72 小时长稳待现场执行。

## 本次修改内容

1. `ProcessEndCoordinator` 每次都把当前调用的 `request_id` 同步转发到当前终端，并继续校验 HTTP 202、`status=accepted` 和一致的响应 `request_id`。
2. 删除 `EndUnknown`、结束前回调围栏以及 `RequestRegistry.CancelByTerminal`；End 失败或超时不建立本地阻塞状态，后续可直接再次 Start 或 End。
3. `TerminalProcessRegistry` 改为有界的回调路由记录：End 成功只清除当前/UI 默认状态，不删除原 `request_id` 路由；在途回调仍可按来源终端和请求标识转交。
4. 已被新 Start 替代以及 Start 响应未确认的路由保留 10 分钟，最多保留 256 条；按当前 15～30 秒一个业务的频率，正常窗口约保留 20～40 条。
5. Start 请求发出后立即建立路由，不等待同步响应才允许匹配；若终端实际受理但响应丢失，后续合法回调仍能被处理。
6. DLL `EndProcessBody` 删除 `RequestSessionManager.CancelAll()`；`EventDispatcher` 删除 `process_active` fallback 门禁，`process_active` 内部字段一并移除。
7. 保留回调来源 IP、`request_id`、资源类型和当前终端路由检查；这些属于安全与双终端隔离，不属于 Start/End 接收开关。
8. 测试 Mock 回调监听地址改为实际使用的 `127.0.0.1`，终端路由测试从运行配置读取终端地址，消除测试顺序污染。

## 涉及文件

- `Core/TerminalProcessRegistry.cs`、`Core/RequestRegistry.cs`：回调路由保留、容量边界和取消语义修正。
- `Server/Coordinator/ProcessEndCoordinator.cs`：End 纯终端控制转发与响应校验。
- `Server/TerminalCallbackHandler.cs`：移除本地流程激活等待，保留来源与路由校验。
- `Server/DllCommandHandler.cs`、`Server/Coordinator/BizOperationHandler.cs`、`Server/ProxyServer.cs`、`Server/Runtime/*`：接入新路由语义和指标名称。
- `src/exports.cpp`、`src/event_dispatcher.cpp`、`src/hzsjkjt_context.*`、`src/request_session_manager.*`：DLL 侧取消和本地活动门禁修正。
- `HZCYKJTHardWare.Proxy.Tests`：End 后流程回调、独立 NFC 回调、路由保留和双终端回归。

## 兼容性说明

- Delphi/DLL 导出函数名、参数、`__stdcall`、结构体、错误码和回调签名未改变。
- DLL 仍为 Win32/x86；Proxy 仍按 x64 独立进程部署，DLL 与 Proxy 的本机 HTTP 路径和 JSON 格式未改变。
- `HZCYKJTHardWare_EndProcess(void)` 外部签名保持不变；DLL 内部仍生成 `request_id` 并同步等待 Proxy/终端结果。
- Preview、同步人脸/指纹抓拍、图片落盘和 VLC 生命周期未与 Start/End 绑定。
- 未增加新依赖、后台 End 重试、全局业务锁、对象池或批处理。

## 风险与注意事项

1. 终端是停止“新回调产生”的唯一状态源；End 后已在网络或处理队列中的合法回调仍会转交第三方，这是本次明确要求的行为。
2. 为避免跨终端数据混淆，切换到另一终端后，旧终端迟到回调仍会被当前终端路由检查拒绝；End 本身不会触发此拒绝。
3. Proxy 重启会丢失内存中的 `request_id` 路由，重启前已经在途的流程回调可能无法匹配；本次没有改变部署或持久化协议。
4. 路由保留为 10 分钟/256 条的有界缓存；如果真实设备可能在十分钟后才发送某一流程回调，需要根据设备实测延长，但不能改成无界保存。
5. 终端文档“请求体字段无”与示例中的 `request_id` 仍有矛盾；当前继续按示例和必填响应字段实施。

## 验证状态

- [x] Proxy + Tests `Release|x64|net46` 隔离输出编译：0 error；仅 1 个 `NU1900` 环境警告。
- [x] x64 MSTest：104/104 通过，包含 End 后流程回调和独立 NFC 回调两项新增集成测试。
- [x] DLL `Release|Win32`：编译通过，0 warning、0 error。
- [x] DLL 导出检查：24 项导出仍存在；`StartProcess@4`、`EndProcess@0`、`SwitchTerminal@4` 保持不变。
- [ ] 正式 Delphi + 真实双终端 Start/End 推送控制：待验证。
- [ ] End 后在途 OCR/NFC/虹膜/授权回调及双终端切换竞争：待真实设备验证。
- [ ] x64 VLC 三路预览、抓拍延迟基线和 24～72 小时长稳：待验证。

## 下一步计划

- [ ] 真实终端验证：未 Start 时抓拍/预览正常；Start 后开始推送；End 后停止产生新推送；End 后在途回调仍能到达第三方。
- [ ] 覆盖 End 400、超时、响应丢失、重复 End，以及失败后立即 Start/End。
- [ ] 双终端交替 1000 个业务循环，确认旧终端回调不会污染当前终端且抓拍延迟无回退。
- [ ] 完成 2 小时短稳后再执行 24～72 小时长稳，记录路由缓存数量、请求数量、内存、GC、线程、句柄和磁盘趋势。

## 回退方式

- 本次语义修正可按上述文件整体回退；无需修改 Delphi 代码、DLL ABI、终端协议或配置格式。
- x64 部署回退仍按原方案停止 x64 Proxy，恢复上一版 Proxy 与对应 VLC 目录。

# x86 DLL + x64 Proxy 全流程长稳测试工具（2026-07-16）

## 当前阶段

- [x] 按方案 B 新增独占运行的全流程测试脚本，模拟 Delphi 单客户端的真实同步调用方式。
- [x] 测试宿主固定为 x86/STA，加载 Win32 DLL；Proxy 单独以 x64 进程运行。
- [x] 每个业务周期覆盖终端切换、Start、15～20 秒高频抓拍、End 和周期间隔。
- [x] 预览跨 Start/End 持续运行，并增加 End 后主动人脸/指纹抓拍，验证抓拍不依赖流程状态。
- [x] 增加调用、周期、回调、资源指标和最终汇总五类 CSV。
- [ ] 真实双终端 2 小时短稳及 24～72 小时长稳：待现场执行并分析输出。

## 本次修改内容

1. 新增 `scripts/stress_test_full_flow.ps1`：
   - 自动进入 x86 STA Windows PowerShell，并校验 DLL PE 为 x86、目标/运行中 Proxy PE 为 x64；
   - 默认每 15～30 秒一个业务周期，每周期高频抓拍 15～20 秒，双终端逐周期交替；
   - 每周期真实调用 `SwitchTerminal`、`StartProcess` 和 `EndProcess`；Start 结果不确定时仍执行 End 清理；
   - 相机和指纹预览在首次 Start 前启动并跨周期保留；
   - 默认每周期 End 后各执行一次人脸和指纹抓拍，单独标记为 `PostEndCapture`；
   - OCR、NFC、虹膜和授权为显式可选项，可按周期提交并采集回调；
   - 识别失败回调以及 End 宽限期后到达的终端流程推送，预览事件不计入该违规指标；
   - 每分钟记录 Proxy/测试宿主 CPU、工作集、私有内存、线程、句柄、GC 内存和磁盘余量；
   - 成功抓拍默认不逐条输出控制台，调用 CSV 每 1000 条、周期每 10 条、回调每 50 条、资源指标每条落盘。
2. 修正 `scripts/stress_test_dll.ps1` 的指纹 P/Invoke，使其与公开 ABI 一致，传入 `saveDir` 和 `saveDirHk` 两个参数；旧脚本默认第二参数为 `null`，维持原压测行为。
3. 新脚本检测 DLL 回调端口占用，防止与 Delphi 第三方或另一 DLL 测试宿主同时操作 Proxy/终端状态。

## 涉及文件

- `scripts/stress_test_full_flow.ps1`：新增全流程长稳测试驱动和 CSV 指标采集。
- `scripts/stress_test_dll.ps1`：修正指纹抓拍 P/Invoke 双参数签名。
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/PROGRESS.md`：记录测试范围、兼容性及验证状态。

## 兼容性说明

- 未修改 DLL 导出函数、参数、`__stdcall`、回调签名、返回值和错误码。
- 未修改 Proxy HTTP API、终端 API、配置格式和第三方调用行为。
- Delphi 第三方程序仍为 x86；测试脚本只是独立替代调用方，不能与第三方程序并行执行。
- Proxy 使用 x64；脚本启动前和连接现有进程时都会检查其 PE 架构，拒绝误用 x86 Proxy。
- Start/End 仅用于通知终端开始/停止主动推送；预览与主动抓拍不使用本地流程门禁。

## 风险与注意事项

1. 本脚本会真实切换终端、启动/结束采集流程并覆盖抓拍文件，必须独占测试电脑上的 DLL/Proxy/终端调用链。
2. OCR、NFC、虹膜和授权默认关闭，只有现场具备对应介质、终端状态和合法测试数据时再显式启用；异步请求受理不等于一定产生成功回调。
3. End 后保留 250ms 默认宽限期，用于区分已在途数据和新的终端推送；是否需要调整应依据终端时间分布，不应直接设为 0。
4. 抓拍不自动重试，失败会原样计数；增加重试会改变真实压力并可能掩盖设备 busy/timeout。
5. 当前已经启动的 PowerShell 测试进程不会热加载脚本后续修改；需在安全完成并 Release 后重新启动，才能覆盖 End 后主动抓拍等最新逻辑。

## 验证状态

- [x] `stress_test_full_flow.ps1` 与 `stress_test_dll.ps1`：Windows PowerShell 语法解析通过。
- [x] 新脚本 `-ValidateOnly`：自动切换 x86/STA、x86 DLL 全部 P/Invoke 编译、x64 Proxy PE 校验通过。
- [x] 旧脚本 `-ValidateOnly`：x86 DLL PE 和修正后的 P/Invoke 编译通过。
- [ ] 真实设备功能验证：待验证。
- [ ] 双终端切换、End 后主动抓拍和 End 后流程推送边界：待验证。
- [ ] 2 小时短稳及 24～72 小时资源趋势：待验证。

## 下一步计划

- [ ] 先运行 10～30 分钟基线：只启用预览、Start/End、高频人脸/指纹和 End 后抓拍。
- [ ] 基线无流程错误后，按现场条件加入 `-EnableOcr -EnableNfc -EnableIris -EnableAuthorize`。
- [ ] 先分析 `full_flow_summary/cycles/calls/callbacks/metrics`，再决定是否进入 24～72 小时长稳。

## 回退方式

- 删除 `scripts/stress_test_full_flow.ps1` 即可移除新测试工具。
- 将旧脚本指纹 P/Invoke 和包装调用恢复为单参数即可回退该脚本修正；业务 DLL、Proxy 和第三方部署均无需回退。

# x64 Proxy 句柄来源隔离验证（2026-07-16）

## 当前阶段

- [x] 已通过独立负载将句柄增长定位到“预览开启时切换终端”的预览/VLC 生命周期。
- [x] 已排除静置、`Start/End`、固定终端高频抓拍、无预览终端切换是主要句柄增长源。
- [ ] Proxy 生产代码尚未修改；预览线程生命周期优化等待方案确认。

## 本次测试工具修改

1. `scripts/stress_test_full_flow.ps1` 新增 `WorkloadMode`：
   - `Idle`：仅初始化和资源采样；
   - `SwitchOnly`：仅重复选择/切换终端；
   - `CaptureOnly`：固定终端执行 `Start -> 高频抓拍 -> End`，不切换终端；
   - `FullFlow`：保留原有默认整流程行为。
2. 新增 `SwitchIntervalSeconds` 和 `RestartProxy`，用于控制切换频率并保证每组测试从全新的 x64 Proxy 进程开始。
3. 新增 `scripts/get_process_handle_type_counts.ps1`，只读统计指定进程的 Windows 句柄类型，用于区分 `Thread`、`Event`、`File` 等资源。

## 隔离验证结果

| 负载 | 时长/规模 | 预览 | 主要结果 |
|---|---:|---:|---|
| Idle | 3 分钟 | 关闭 | 句柄 553 起步，随后稳定并下降至约 536；无线性增长 |
| 同终端 SwitchOnly | 99 次 | 关闭 | 全部成功，句柄最终低于预热值 |
| 双终端交替 SwitchOnly | 99 次 | 关闭 | 句柄在 575～599 波动并有批量回收，无按切换次数持续累积 |
| 双终端交替 SwitchOnly | 99 次 | 相机+指纹 | 预热后句柄从 712 增至 942，约 `+230`；趋势约 `+37.4/min` |
| 固定终端 CaptureOnly | 818 次调用 | 关闭 | 395 次人脸、395 次指纹全部成功；句柄趋势约 `-0.75/min` |
| 单次预览打开/关闭 | 1 次 | 相机+指纹 | 有一次性资源抬升，但频率远低于切换时重复重建，不是当前主要增长源 |

句柄类型快照对照：预览切换组约有 242 个 `Thread` 句柄，抓拍基线约 39 个，相差约 203 个；`203 / 99 = 2.05` 个 `Thread` 句柄/次切换。该数值与整流程压力测试观察到的约 2.1～2.3 个句柄/次切换一致。

## 代码交叉验证

1. `Preview/VlcPreviewController.cs` 每个控制器创建一个专用 STA `Thread`。
2. `PreviewManager.StopAllCore(preserveRestartInfo: true)` 在切换时释放两路播放器，然后 `RestartPreviewsOnTerminalSwitch` 为新终端重新创建控制器。
3. `VlcPreviewController.DisposeAsync` 当前等待“停止动作执行完毕”，但没有等待专用线程的 `ThreadMain` 真正退出，也没有 `Join`/线程退出完成信号。
4. `_restartInfo` 保存完整 `PreviewSession`，其中仍包含已经释放的 `Player` 引用，延长旧控制器和 `Thread` 对象的可达时间。

因此当前证据支持的精确结论是：增长路径位于预览/VLC 线程生命周期；其中管理线程未被同步等待退出、已释放控制器被短期保留是直接优化点。是否还包含 libVLC 内部线程延迟回收，需要修正后用同一测试做 A/B 才能最终区分。

## 兼容性说明

- 未修改 DLL 导出名称、参数、调用约定、返回值、错误码或回调签名。
- 未修改 DLL/Proxy HTTP API、终端 API、配置文件和第三方调用行为。
- `Start/End` 与预览/主动抓拍的既有独立语义保持不变。
- 测试脚本新增参数均有默认值；原 `FullFlow` 调用方式保持兼容。

## 建议优化顺序

1. P0 最小修正：为 `VlcPreviewController` 增加线程退出完成信号，`DisposeAsync` 有界等待线程真正退出；释放后清空旧 `Player` 引用，并将重启信息改为不持有控制器的轻量数据。
2. P1 结构优化：每个预览资源保留一个长生命周期 STA 工作线程，切换终端时在线程内替换媒体/URL，不再每次销毁和创建控制器线程。
3. P1 防护：同终端重复选择直接返回；短时间连续切换做合并，避免无意义重启。不得用强制 `GC.Collect` 或延时休眠代替生命周期修复。

## 验证状态

- [x] Windows PowerShell 语法解析与四种 `ValidateOnly` 模式通过。
- [x] x64 Proxy 真实双终端隔离测试完成。
- [x] 高频人脸/指纹抓拍实时性基线通过：0 次失败；人脸 P95 约 162ms，指纹 P95 约 310ms。
- [x] 句柄类型快照与代码生命周期交叉验证完成。
- [ ] 生产代码修正、编译和单元测试：未执行，等待确认。
- [ ] 修正后 99 次预览切换 A/B、2 小时短稳及 24～72 小时长稳：待验证。

## 回退方式

- 本次仅扩展测试工具，没有业务代码回退需求。
- 如需回退测试工具，移除 `WorkloadMode`、`SwitchIntervalSeconds`、`RestartProxy` 及三个隔离分支，并删除 `scripts/get_process_handle_type_counts.ps1`；原默认 `FullFlow` 行为不受影响。

# 预览句柄释放方案 A 实施与 A/B 验证（2026-07-16）

## 当前阶段

- [x] 已实施预览线程有界退出等待和轻量重启信息。
- [x] 已完成 x64 Release 编译、单元测试和真实双终端预览切换验证。
- [ ] 句柄净增长未达到验收标准，等待确认是否进入固定 STA 线程复用方案。

## 本次修改内容

1. `VlcPreviewController.DisposeAsync` 改为等待 `ThreadMain` 的 `finally` 完成，不再只等待停止动作执行。
2. 启动超时和启动异常路径统一进入有界释放流程。
3. 重复、并发释放共享同一个线程退出结果，避免首次调用后其他调用方直接返回。
4. 增加创建线程数、活动线程数和退出超时数诊断日志。
5. `_restartInfo` 改为轻量 `PreviewRestartInfo`，不再持有旧 `Player`、控制器和 `Thread` 引用。
6. 测试脚本兼容 x86 测试宿主启动并校验 x64 Proxy；外部已有进程仍执行严格路径和 PE 架构校验。

## 涉及文件

- `Preview/VlcPreviewController.cs`：线程退出信号、幂等释放、异常路径释放和诊断计数。
- `Preview/PreviewManager.cs`：轻量 `PreviewRestartInfo`。
- `HZCYKJTHardWare.Proxy.Tests/Preview/PreviewRestartInfoTests.cs`：验证重启信息不保留播放器或会话。
- `scripts/stress_test_full_flow.ps1`：修正 x86 PowerShell 无法读取 x64 进程模块路径时的自启动进程校验。

## 兼容性说明

- DLL 导出函数、参数、调用约定、结构体、错误码和回调格式均未修改。
- DLL/Proxy HTTP 协议、终端 API、配置和第三方调用方式均未修改。
- DLL 继续使用 x86；本次 Proxy 验证构建为 x64 Release、目标框架 `net46`。

## 验证结果

- [x] Proxy x64 Release：0 warning、0 error。
- [x] Tests x64 Release：编译成功；仅有 NuGet 漏洞源不可访问的 `NU1900`，不影响编译。
- [x] 新增测试：2/2 通过。
- [x] 全量测试：96 项通过；10 项 Integration 因 x64 测试宿主构造 `HttpListener` 抛出 `PlatformNotSupportedException` 未执行，与本次预览修改无关。
- [x] 真实双终端预览切换：89 次切换、97 次 DLL 调用、0 失败。
- [x] 线程退出超时：0 次。
- [ ] 句柄指标未通过：预览前 541，预热后约 678，峰值 896，停止预览后 862；后半段趋势约 `+59.29/min`。
- [ ] 空闲 3 分钟后总句柄约 774，仍有 211 个 `Thread` 句柄，而实际活动线程约 30 个。

验证数据：
`scripts/stress_results/handle_release_after/handle_switchonly_summary_20260716_194625.csv`
`scripts/stress_results/handle_release_after/handle_switchonly_metrics_20260716_194625.csv`

## 结论与风险

方案 A 修正了停止顺序、异常释放和旧对象引用，但未消除每次切换创建新 STA 线程造成的线程句柄滞留。日志证明线程均在超时内执行完释放；剩余句柄主要属于已终止线程，依赖 CLR/GC 延迟回收。不得用 `GC.Collect()`、定时重启或额外休眠代替结构修复。

## 下一步计划

- [ ] 经确认后实施固定 STA 工作线程复用：每个预览资源复用工作线程，切换时只替换播放器/URL。
- [ ] 使用同一脚本再次执行约 100 次预览切换 A/B，验收 `Thread` 句柄净增不超过 5～10。
- [ ] 通过后执行 2 小时短稳和 24～72 小时长稳。

## 回退方式

- 仅反向撤销上述三个生产/测试代码文件及测试脚本中的本轮差异；不得覆盖工作区其他未提交修改。

# 预览句柄释放方案 B VLC 实验与回退（2026-07-16，结论已更正）

## 当前阶段

- [x] 已按授权实施并验证固定 STA 工作线程复用。
- [x] 已继续验证 libVLC 实例、media player 和停止状态等待等复用层级。
- [x] 各层级均未达到句柄验收目标，实验实现已完整回退，仅保留方案 A 的生命周期修正。
- [x] 后续核对确认人脸、指纹返回 HTTP MJPEG，以下 VLC 实验没有覆盖实际泄漏链路；“4 路 RTSP 常驻”提议已撤回。

> 结论更正：真实压测日志中 RTSP URL 为 0，VLC 记录仅来自 Proxy 启动预热。句柄增长实际来自 `MjpegPreviewController` 在每次终端切换时反复创建渲染线程和读取线程，以及读取线程退出等待不完整。以下数据仅保留为误归因排查记录，不再作为 VLC 泄漏证据。

## 方案 B 实验范围

1. B1：相机、指纹各保留一个长生命周期 STA worker，终端切换时在原线程内停止并重播。
2. B2：在 B1 基础上复用 `libvlc_instance_t`。
3. B3：继续复用 `libvlc_media_player_t`，通过 `libvlc_media_player_set_media` 替换 RTSP 媒体。
4. B4：停止后轮询 `libvlc_media_player_get_state`，确认进入停止状态再替换或释放媒体。

## 真实终端验证结果

每个变体均执行 x64 Release、双终端相机与指纹预览、约 3 分钟、89 次交替切换，功能调用均为 0 失败。

| 版本 | 预热后句柄 | 结束句柄 | 峰值句柄 | 后半段趋势 |
| --- | ---: | ---: | ---: | ---: |
| 方案 A | 约 678 | 862 | 896 | `+59.29/min` |
| B1 固定 STA worker | 713 | 882 | 889 | `+56.91/min` |
| B2 复用 libVLC instance | 683 | 886 | 889 | `+54.22/min` |
| B3 复用 media player | 717 | 833 | 892 | `+44.21/min` |
| B4 等待 stopped 状态 | 684 | 856 | 884 | `+65.34/min` |

- [x] B1 编译和定向测试通过；非 Integration 回归 97/97。
- [x] B1～B4 真实预览切换功能均正常，无 DLL 调用失败。
- [ ] B1～B4 句柄指标全部未通过。
- [x] B4 结束后总句柄约 828；`Thread` 句柄 234，实际活动线程约 25。
- [x] 未发现方案 A 的 VLC 控制器线程退出超时。

验证数据目录：

- `scripts/stress_results/handle_release_scheme_b`
- `scripts/stress_results/handle_release_scheme_b2`
- `scripts/stress_results/handle_release_scheme_b3`
- `scripts/stress_results/handle_release_scheme_b4`

## 结论

本轮实验修改的是 VLC 控制器，而真实人脸、指纹预览使用 `MjpegPreviewController`，因此 B1～B4 指标未改善不能证明 VLC 内部存在泄漏，只能证明这些修改与实际增长路径无关。正确修复方向是复用 MJPEG worker，并在切换时取消旧 HTTP 请求、替换 URL。

## 回退与回归状态

- [x] 已删除 B1～B4 的 persistent worker、`set_media/get_state` 和专用复用测试代码。
- [x] 已恢复方案 A 的 `VlcPreviewController` 有界线程退出和轻量 `PreviewRestartInfo`。
- [x] 回退后 Proxy x64 Release：0 warning、0 error。
- [x] 回退后 Tests x64 Release：编译成功；仅 NuGet 漏洞源不可访问产生 `NU1900`。
- [x] 方案 A 定向测试 2/2；非 Integration 回归 96/96。
- [x] DLL ABI、HTTP/终端协议、回调、配置和第三方调用行为未改变。

## 下一步计划

- [x] 已转入 MJPEG worker 复用方案，结果见下一节。

# MJPEG 长生命周期 worker 方案 B 实施与验证（2026-07-17）

## 当前阶段

- [x] 已完成按资源与会话复用 MJPEG 渲染/读取 worker。
- [x] 已完成 x64 Release、定向测试、非 Integration 回归、3 分钟首轮、10 分钟短稳和 2 小时真实硬件验证。
- [x] 句柄增长验收通过。
- [ ] 24～72 小时长稳及设备断开/恢复验证待后续安排。

## 本次修改内容

1. `MjpegPreviewController` 的渲染线程和 HTTP 读取线程改为 worker 全生命周期只创建一次。
2. 终端切换时执行 `PauseAsync`，取消旧 `HttpWebRequest` 并等待请求脱离；恢复时通过 `SwitchStreamAsync` 替换 URL 和媒体代次。
3. 每一帧绑定媒体代次，旧请求延迟返回的数据不能覆盖新终端画面。
4. 渲染和读取线程分别提供退出完成信号，最终释放必须等待两条线程退出；移除 `Task.Run(Thread.Join)` 和未检查返回值的读取线程 `Join(1000)`。
5. `PreviewManager` 按 `ResourceType + SessionType` 保存 MJPEG worker；终端切换只暂停，显式停止、目标窗口失效和 Proxy 关闭仍完整移除并释放。
6. MJPEG 流故障恢复复用原 worker；HTTP MJPEG 失败时原有 VLC 回退兼容路径保留。

## 涉及文件

- `Preview/MjpegPreviewController.cs`：长期 worker、媒体代次、请求暂停、双线程退出信号。
- `Preview/PreviewManager.cs`：MJPEG worker 池及停止、切换、恢复、关闭生命周期。
- `HZCYKJTHardWare.Proxy.Tests/Preview/MjpegWorkerReuseTests.cs`：本地双 MJPEG 流切换、暂停、复用和最终退出验证。

## 验证结果

- [x] Proxy x64 Release 编译成功，0 warning、0 error；PE Machine 为 `0x8664`。
- [x] Tests x64 Release 编译成功；仅包漏洞源不可访问产生 `NU1900`。
- [x] 定向测试 3/3：两次流切换不新增 worker，暂停保留线程，最终释放后渲染/读取线程均回到基线。
- [x] 非 Integration 首次运行 96/97；既有 `ActiveTasksTrackerTests` 时序断言瞬时失败，单项重跑通过，完整重跑 97/97。
- [x] 3 分钟真实双终端测试：88 次切换、96 次 DLL 调用、0 失败；预热后句柄 598～622，斜率约 `+6.36/min`，结束后 `Thread` 句柄 43。
- [x] 10 分钟真实双终端短稳：297 次切换、305 次 DLL 调用、0 失败。
- [x] 10 分钟预热后总句柄 593～633；整体斜率 `-1.20/min`，后半段 `+0.57/min`；结束后总句柄 581、`Thread` 句柄 43、实际线程 26。
- [x] 对比旧版：空闲后仍有 211～234 个 `Thread` 句柄；本版没有按切换次数累积。
- [x] MJPEG pause timeout、worker stop timeout、同 URL 恢复失败、VLC 回退和 RTSP 使用均为 0。
- [x] 2 小时真实双终端短稳：运行 120.07 分钟，1437 次切换、1445 次 DLL 调用、0 失败、0 UI 阻塞告警。
- [x] 2 小时共取得 241 个 Proxy 采样点，30 秒采样无超过 31 秒的缺口；后半程句柄范围 673～709，线性斜率 `+0.0236/min`，`R²=0.0104`；最后 30 分钟斜率 `+0.0578/min`，未出现持续线性增长。
- [x] 测试停止预览并释放 SDK 后，句柄类型快照为 47 个 `Thread` 句柄、26 个实际线程；截至 2026-07-18 15:54，测试 Proxy 继续空闲时为 633 个句柄、26 个线程、Private Memory 58.96MB。
- [x] Private Memory 后半程斜率约 `+0.037MB/min`，但 `R²=0.027`；89.3MB 单点峰值在下一个 30 秒采样回落到 53.84MB，空闲后稳定在约 59MB，当前不呈现单向累积。
- [x] DLL 切换响应 P50/P95/P99/最大值分别为 3/12/25/44ms；1436 次后台预览恢复全部成功，整体恢复 P95 1269ms，摄像头/指纹单路 P95 分别为 131/129ms。
- [x] 测试窗口 Proxy 日志 21608 行，警告、错误、MJPEG pause/stop timeout、恢复失败均为 0；仅有启动阶段 VLC 预热记录，没有 RTSP URL 或 VLC 实际预览记录。

验证数据：

- `scripts/stress_results/handle_release_mjpeg_scheme_b/handle_switchonly_summary_20260717_105739.csv`
- `scripts/stress_results/handle_release_mjpeg_scheme_b/handle_switchonly_metrics_20260717_105739.csv`
- `scripts/stress_results/handle_release_mjpeg_scheme_b_10min/handle_switchonly_summary_20260717_110305.csv`
- `scripts/stress_results/handle_release_mjpeg_scheme_b_10min/handle_switchonly_metrics_20260717_110305.csv`
- `scripts/stress_results/handle_release_mjpeg_scheme_b_2hour_real_20260718/handle_switchonly_summary_20260718_090009.csv`
- `scripts/stress_results/handle_release_mjpeg_scheme_b_2hour_real_20260718/handle_switchonly_metrics_20260718_090009.csv`

## 兼容性和风险

- DLL 导出函数、参数、调用约定、结构体、错误码和回调格式未修改。
- Proxy HTTP/终端协议、配置文件和第三方调用行为未修改。
- 人脸、指纹仍只保持当前终端的两路 HTTP MJPEG，不增加为四路常驻连接。
- VLC/RTSP 路径保持原行为，供未来车牌镜头使用。
- 2 小时内 Private Memory 在预热后进入约 58～62MB 平台，未伴随句柄或线程持续增长；仍需在 24～72 小时长稳中复核平台期。

## 下一步计划

- [x] 已完成 2 小时双终端相机+指纹预览切换短稳，句柄、线程、功能和恢复时延验收通过。
- [ ] 安排 24～72 小时长稳及设备断开/恢复验证。
- [ ] 补测外部预览 HWND 销毁/重建和第三方程序退出/重启后的 worker 最终释放。

## 回退方式

- 反向撤销上述两个生产文件中的本轮 MJPEG worker 差异，并删除 `MjpegWorkerReuseTests.cs`。
- 回退不会涉及 DLL、配置、终端协议或第三方接口文件；不得覆盖工作区其他未提交修改。
# MJPEG 16小时真实硬件长稳收尾核查（2026-07-18）

## 数据边界与完成判定

- 本节仅使用 `scripts/stress_results/handle_release_mjpeg_scheme_b_16hour_real_restart_20260718_1750`；人工中断的 `handle_release_mjpeg_scheme_b_16hour_real_20260718` 未参与任何统计。
- 正式目录只有 `handle_switchonly_cycles_20260718_175129.csv` 和 `handle_switchonly_metrics_20260718_175129.csv`，没有本轮 `summary`、`calls` 或 `callbacks` CSV；不能据称已取得完整调用汇总。
- 目录于 17:51:32 开始写入。18:04:16 按收尾指令关闭测试 Proxy 前，连续有效片段只有 152 次、12.70 分钟，远未达到 960 分钟；本轮 16 小时验证为**未完成，不能通过**。
- 关闭后测试宿主 PowerShell（PID 39624）仍在运行并继续向 cycles CSV 追加记录；首个失败为 cycle 159（18:04:43，终端 1，切换返回 0、耗时 2026ms）。这些失败由关闭 Proxy 后产生，不计入关闭前有效片段，也不能作为产品长稳失败归因。

## 关闭前有效片段结果

- 总切换 152 次：终端 1 为 76/76 成功，终端 2 为 76/76 成功，失败 0。
- 切换耗时（CSV 线性插值分位数）：P50 3ms、P95 18.45ms、P99 52.64ms、最大 72ms。
- Proxy 日志窗口（17:51:30～18:04:16）无 warning/error、MJPEG pause/stop timeout 或流恢复失败；两路 MJPEG 预览启动各 153 次（摄像头 153、指纹 153）。
- 同窗口仅有 3 条 VLC 预热日志（17:51:30，预热 229ms），没有 VLC 实际预览证据；RTSP 日志计数为 0。

## 资源趋势与句柄快照

- 预热后可用 Proxy 采样仅 16 个（17:56:31～18:04:02，约 7.5 分钟），HandleCount 为 668～687，线性斜率 `+15.70 handles/hour`、`R²=0.0144`；线程数为 30～35，Private Memory 为 53.04～54.98MB，首末增加 1.84MB。
- 该短窗口呈有界波动、没有单调线性增长证据，但样本量和持续时间不足，不能外推为 16 小时结论。后半 8 小时和最后 4 小时均无数据，斜率/R²不可计算。
- 18:03:50 的关闭前句柄类型快照：共 678 个句柄，其中 Event 269、Thread 61、Key 46、File 31、Semaphore 24、IoCompletion 21、Section 14、ALPC Port 12；另有 159 个受权限/对象限制未能解析类型。
- 18:04:16 已再次核对 PID 47668 路径为 `E:\SZBJ\皇岗开发\车道\HZCYJKTHardWare\_codex_build\mjpeg_worker_scheme_b_x64\proxy\HZCYKJTHardWare.Proxy.exe`，快照为 684 句柄、31 线程、Private Memory 54.93MB；随后仅终止该 Proxy，未终止测试宿主或其他进程。

## 验证状态与建议

- [ ] 真实硬件 960 分钟 MJPEG 句柄长稳：未完成，待重新安排独占的完整测试窗口。
- [ ] 后半 8 小时及最后 4 小时 HandleCount 斜率/R²：无数据，待验证。
- [ ] 本轮 summary/calls 汇总及完整失败归因：文件缺失且测试宿主在 Proxy 关闭后继续写入，待验证。
- [x] 关闭前约 12.7 分钟双终端切换功能片段：152/152 成功。

建议当前不要仅凭此轮数据修改生产代码；先修正/确认测试宿主在 Proxy 意外退出后的停止与结果封存行为，再重新执行一次不中断的 16 小时独占测试。

# 同名抓拍文件一致性方案 A（2026-07-20）

## 当前阶段

- [x] 保留既有 `WorkerQueue` 抓拍队列和同名文件覆盖行为。
- [x] 在 `FileSaver` 公共保存层增加同目标路径互斥，覆盖 Base64 图片和无畸变 BMP 的“临时文件写入—刷盘—原子提交”完整过程。
- [x] 对 `MoveFileEx` 的短时占用错误增加有限重试。
- [ ] 尚未部署到真实硬件环境执行 FullFlow 长稳验证。

## 本次修改

- `Storage/FileSaver.cs`
  - 使用 64 个固定分片锁，按 `Path.GetFullPath` 规范化后的路径进行不区分大小写映射；锁对象数量有界，不随抓拍文件数量增长。
  - 同一路径写入串行化，不同路径通常仍可并发；不修改上层队列容量、顺序和待执行任务替换规则。
  - `MoveFileEx` 遇到 Win32 错误 5、32、33、1224 时按 10/20/40/80ms 退避重试，累计等待上限 150ms；其他错误立即失败。
  - 所有重试耗尽后仍返回保存失败，原目标文件保持为上一次完整成功版本，临时文件由 `finally` 清理。
- `Storage/FileSaverTests.cs`
  - 新增 12 路同路径并发覆盖测试。
  - 新增目标文件短时被读取占用、释放后重试成功测试。

## 兼容性说明

- DLL 导出函数、调用约定、参数、返回值、错误码和回调格式均未修改。
- Proxy HTTP/终端协议、抓拍队列和第三方调用方式均未修改。
- 继续使用调用方指定的同名文件；成功返回表示本次完整图片已原子提交，后续同路径成功抓拍仍会按既有规则覆盖。
- 同路径等待和重试会增加最多约 150ms 的文件提交等待；不同路径仅在哈希分片碰撞时可能短暂互斥。

## 验证状态

- [x] Proxy + Tests `Release|x64` 独立目录编译：0 error；仅首次离线构建出现 NuGet 漏洞源不可达警告，不影响编译。
- [x] Proxy + Tests `Release|x86` 独立目录编译：0 warning、0 error。
- [x] `FileSaverTests`：x64 7/7、x86 7/7 通过。
- [x] x64 非 Integration 回归：99/99 通过。
- [ ] 真实硬件连续抓拍、FullFlow 150 分钟及更长长稳：待验证。
- [ ] 外部程序持续占用同名文件超过 150ms 的现场行为：待验证；预期当前请求明确失败且旧完整文件保留。

## 风险与下一步

- 单一共享文件名只能保证成功提交时文件属于该请求；下一次同路径抓拍成功后必然覆盖。调用方应在本次成功返回后及时读取。
- 建议停止现有 Proxy 后部署本次独立构建，先做少量人工抓拍核对，再执行 150 分钟 FullFlow；重点统计抓拍失败、原子替换重试/失败日志、`.tmp` 残留以及响应文件内容。
- 本次未执行真实硬件测试，不将方案 A 标记为现场验证通过。

## 回退方式

- 仅撤销 `FileSaver.cs` 中的路径分片锁、有限重试及对应两个测试；无需修改抓拍队列、DLL、配置或第三方程序。
