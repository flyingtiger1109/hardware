# Stage 5.1 / D1：Runtime State Snapshot

日期：2026-09-05

状态：`CODE DONE / VERIFIED / FIELD PENDING`

## 1. 结论

D1 已在 C# Proxy 侧完成最小实现：记录终端通信事实，复用现有健康结果和 Preview 状态，并提供线程安全的内部只读快照及低频诊断摘要。

本阶段只做 Observe / Measure / Expose / Log，不做 Control / Recover / Restart / Block。

## 2. 现有状态审查

- `TerminalManager` 已提供当前路由、终端索引、终端配置和路由 epoch；没有可直接复用的每终端通信历史。
- `TerminalClient` 是 Proxy 终端 HTTP 的统一入口，适合作为最小请求观察点；不需要修改各业务 Controller。
- `TerminalHealthChecker` 已有健康轮询、退避和设备状态解析；其轮询失败/重试计数属于健康轮询内部语义，没有直接复用为 D1 通信计数。
- `PreviewManager` 已维护 Session、RestartInfo、Active Recovery、Desired State 以及 MJPEG Recovery Episode；D1 只读投影这些现有状态，没有创建第二套 Preview 状态机。
- 未发现现有外部 Diagnostics Endpoint；因此本阶段选择方案 A：内部 Snapshot + 低频日志。

## 3. D1 所在侧

D1 只落在 C# Proxy。终端通信和 Preview 生命周期均由 Proxy 掌握；Native DLL、DLL ABI、跨进程状态同步和新协议均不需要参与本阶段。

## 4. Snapshot 字段

### Terminal

- `TerminalIndex`、`TerminalName`、`Configured`、`Endpoint`
- `Reachable`、`LastSuccessUtc`、`LastFailureUtc`
- `FailureCount`、`ConsecutiveFailures`、`LastErrorCode`、`LastLatencyMs`
- `HealthHealthy`、`LastHealthObservedUtc`、`LastHealthError`
- 现有 `HealthStatus.Devices` 的 `Id`、`Status`、`Message`、`IsOnline` 副本

### Preview

- 现有资源类型和会话类型
- 终端绑定及解析后的终端索引
- `DesiredState`、`RuntimeState`、`Recovering`
- 现有 Recovery 的 `Attempt`、`FailureCount`、`LastFailureUtc`、短 `LastError`

未加入 OCR/证件/IC/Base64/Authorization Payload/密码/Token，也未保存完整 Session、HWND 对象或完整异常堆栈。

## 5. 关键语义

- `Reachable=true`：最近一次 HTTP 请求收到了响应，包括非 2xx；它只表示通信层可达，不表示设备业务健康。
- `Reachable=false`：最近一次请求未收到响应，例如 timeout/network error。
- 初始状态为 nullable unknown，不把尚未通信误报为在线。
- HTTP 2xx 才更新 `LastSuccessUtc` 并将 `ConsecutiveFailures` 清零；HTTP/传输失败更新 `LastFailureUtc`、失败计数和短错误码。
- 成功后保留最后一次失败时间和错误码，便于排查历史故障；计数为当前 Proxy 进程生命周期内的内存值，不持久化。
- 新增 D1 时间字段使用 UTC；既有 `HealthStatus.Timestamp` 的原有行为未改动。

## 6. 并发与读取策略

- `RuntimeStateTracker` 使用单个轻量 `lock` 保护 T1/T2 可变诊断状态。
- 更新路径只写标量和有界设备状态副本；Snapshot 路径在锁内复制终端 DTO，锁外读取终端元数据并投影现有 Preview 状态。
- 观察回调异常被隔离，不能改变原有 HTTP 返回值、日志和异常行为。
- Snapshot 不发 HTTP、不访问硬件、不读磁盘、不等待 Preview 操作锁、不执行 Start/Stop/Recovery。
- 既有 5 分钟长稳指标输出只追加一行短摘要，不增加每请求成功日志。

## 7. 修改文件

| 文件 | 类/函数 | 变更 | 原因 |
|---|---|---|---|
| `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/Runtime/RuntimeStateSnapshot.cs` | `RuntimeStateSnapshot`、`RuntimeStateTracker` | 新增内部 DTO、状态更新和快照复制 | 集中提供可诊断事实 |
| `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Terminal/TerminalClient.cs` | POST/GET 统一 HTTP 结果分支 | 增加内部观察回调 | 避免修改几十个业务调用点 |
| `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Terminal/TerminalHealthChecker.cs` | `HealthStatus`、轮询结果 | 增加内部终端归属 | 将既有健康结果按 T1/T2 隔离 |
| `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Preview/PreviewManager.cs` | `CaptureRuntimeStateSnapshot` | 只读投影现有 Preview 状态 | 不建立第二套状态机 |
| `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/ProxyServer.cs` | 构造和 `GetRuntimeStateSnapshot` | 绑定观察点和低频报告提供器 | 统一接入 Proxy 生命周期 |
| `demo/CSharpProxy/HZCYKJTHardWare.Proxy/Server/Runtime/RuntimeMetricsReporter.cs` | `BuildSnapshot` | 追加低频状态摘要 | 服务于长稳排障 |
| `demo/CSharpProxy/HZCYKJTHardWare.Proxy.Tests/Runtime/RuntimeStateSnapshotTests.cs` | D1 定向测试 | 覆盖初始、成功/失败/恢复、隔离、健康副本、并发读取 | 验证状态语义和锁保护 |
| `todo.md`、本报告 | 阶段记录 | 同步 D1 状态和边界 | 保持进度可追踪 |

## 8. 业务行为与兼容性

业务行为：`NO CHANGE`。

- 没有新增 Retry、Circuit Breaker、Fail Fast、自动故障转移、请求阻断、自动终端切换、Watchdog、Proxy/SDK/设备/Preview 自动重启。
- 没有修改 Preview Recovery、HTTP 协议、设备协议、现有 Timeout、Callback ACK、Native N1/N2/N3-A 逻辑。
- 没有修改 DLL 导出、ABI、调用约定、结构体布局或第三方调用方式。
- 观察回调失败会被吞掉，保证诊断能力不是业务依赖。

## 9. 测试结果

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| Initial State | PASS | T1/T2 初始 `Reachable=null`，无成功/失败时间 |
| Success / Failure / Recovery | PASS | 失败计数累加、连续失败清零、历史错误保留 |
| Terminal Isolation / Switch | PASS | T1 失败不改变 T2；当前终端切换后历史保留 |
| Health Device Snapshot | PASS | 设备列表深拷贝，不受原对象后续修改影响 |
| Concurrent Snapshot Read | PASS | 8 个并发 worker，共 4,000 次读取/写入交错，无异常 |
| Existing Preview state source regression | PASS | `PreviewRestartInfoTests` + `PreviewRecoveryPolicyTests` 16/16 |
| D1 targeted tests | PASS | x86 Debug 5/5；x64 Release 5/5 |
| Proxy x64 Release | PASS | 0 errors / 0 warnings |
| Test build warning | OBSERVED | 测试项目保留 `NU1900`：当前无法访问 NuGet 漏洞源，不影响编译或 D1 定向测试 |
| Existing full integration suite | NOT USED as D1 gate | 当前测试宿主的 `HttpListener` mock setup 抛出 `PlatformNotSupportedException`；未据此宣称全量通过 |
| Real T1/T2 field run | NOT TESTED | 需在用户已连接的两个终端上执行现场验证 |
| Real Preview Recovery/Stop snapshot | NOT TESTED | 需现场故障注入，不能用代码单测替代 |
| D1 30 min/2 h regression | NOT TESTED | Stage 4.4 结果不作为 D1 长稳回归结果 |

## 10. 后续

- 在两个已连接终端上验证 T1/T2 轮换、失败→恢复、健康设备副本和 Snapshot 当前终端。
- 在真实 Camera/Fingerprint/CJ/RJ2/RJ3 Preview 运行、Recovery、Stop 期间确认投影与现有状态一致。
- 如需自动决策，另起 Stage 5.2 / D2 评估 Circuit Breaker；Stage 5.3 / D3 再单独评估 Watchdog。D1 不预先实现这两类控制能力。

## 11. 回退

只回退本阶段列出的 C# Proxy D1 源码、定向测试和文档；不执行宽范围 `git reset`、`clean` 或覆盖其他工作树修改。回退后原有 HTTP、健康轮询、Preview Recovery 和终端业务链路保持原逻辑。
