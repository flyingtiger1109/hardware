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
