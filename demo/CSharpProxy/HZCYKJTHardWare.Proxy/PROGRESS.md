# 项目进度记录

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
