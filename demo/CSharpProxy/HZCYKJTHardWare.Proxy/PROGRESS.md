# 项目进度记录

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
