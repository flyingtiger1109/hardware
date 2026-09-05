# Stage 4.4：2 小时稳定性基线与资源泄漏验收

## 验收结论

- 核心长稳曲线：`PASS`
- Stage 4.4 总体：`PASS WITH OBSERVATIONS`
- 原因：正式核心曲线已运行 120.12 分钟且 0 失败；但本次没有真实 OCR/NFC/虹膜/授权样本，且未能对前台 UI 做人工观察，另有 4 次超过 500 ms 的脚本级 UI 阻塞告警。

## 1. 测试环境

- 日期：2026-09-05（Asia/Shanghai）
- OS：`Microsoft Windows NT 10.0.26200.0`，64-bit，AMD64
- .NET Framework 注册表：`v4\Full Release=533509`
- Git 分支：`优化版`
- 测试时 HEAD：`e3ad5f07`
- 工作树：测试时存在既有未提交修改；本次未修改业务代码
- x86 TestHost：Windows PowerShell x86，STA
- Native DLL：Release x86
- C# Proxy：Release x64，目标输出为 `net46`
- 终端 1：`192.168.20.30:9098`，测试前/后 TCP 可达
- 终端 2：`192.168.20.31:9098`，测试前/后 TCP 可达

被测二进制 SHA-256：

- Native DLL：`F39635B31DB6455CE23BCB1B9B687F89F0DD96C9781D1EAA3A58937F914485FC`
- C# Proxy：`D817B6D1495CF44E6BEE59980A5DB12467966C6601849455D56E5A3E32F70824`

## 2. 正式测试配置与时间

正式命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/stress_test_full_flow.ps1" -DurationMinutes 120 -TerminalMode Alternate -InitialTerminal 1 -WorkloadMode FullFlow -RestartProxy -MetricsIntervalSeconds 60 -SaveDir "E:\SZBJ\皇岗开发\车道\HZCYJKTHardWare\scripts\stress_results\stage4_4_core_captures_20260905" -ResultsDir "E:\SZBJ\皇岗开发\车道\HZCYJKTHardWare\scripts\stress_results\stage4_4_core_20260905"
```

- 开始：`2026-09-05T11:36:12.2686980+08:00`
- 结束：`2026-09-05T13:36:19.5704488+08:00`
- 实际时长：`120.12 min`
- `WorkloadMode=FullFlow`
- `TerminalMode=Alternate`，T1/T2 交替
- `PreviewEnabled=True`
- 脚本启动并重启的 Proxy PID：`42936`
- 正式曲线未启用 OCR、NFC、虹膜、授权提交；原因是现场没有可持续提供的真实样本

每轮包含终端切换、`StartProcess`、人脸/指纹抓拍、`EndProcess`，并进行结束后的残留回调检查。测试结束时执行了停止指纹预览、停止相机预览和 `ReleaseSdk`。

## 3. 业务与通信结果

| 指标 | 结果 |
|---|---:|
| 总轮次 | 307 |
| 失败轮次 | 0 |
| 总调用数 | 18,047 |
| 失败调用数 | 0 |
| 人脸 | 8,559 / 8,559 |
| 人脸 P50 / P95 / P99 | 119 / 183 / 215 ms |
| 指纹 | 8,559 / 8,559 |
| 指纹 P50 / P95 / P99 | 294 / 416 / 441 ms |
| 回调总数 | 2 |
| 回调错误 | 0 |
| End 后进程推送回调 | 0 |
| 异步请求 | 0（本次核心基线未覆盖） |

2 个回调均为预览阶段的成功回调；回调 CSV 中没有失败事件、解析错误或残留推送。

### 成功但较慢的调用

脚本阈值 `UiBlockWarningMs=500`。正式曲线有 4 次成功但超过阈值的指纹采集：

| 时间 | 轮次/终端 | 耗时 |
|---|---|---:|
| 12:29:48.654 | C138 / T2 | 509 ms |
| 12:51:47.174 | C195 / T1 | 561 ms |
| 13:29:54.935 | C292 / T2 | 501 ms |
| 13:29:55.943 | C292 / T2 | 639 ms |

上述调用均返回成功，没有转化为失败轮次。Proxy 日志同时记录了一次 575 ms、HTTP 状态 200 的指纹同步响应。

### 未纳入正式曲线的资格试跑

曾进行过一次包含 OCR/NFC/虹膜/授权的短试跑，因现场没有文档、卡片和虹膜样本而产生业务失败回调，随后主动停止；该试跑没有正式汇总文件，不计入本次 2 小时核心验收，也不作为通信层失败结论。

## 4. 资源采样

资源监控从正式测试约 5 分钟后建立 T0，1 分钟采样一次。正式 T0 至 T120 共 116 个运行态样本/进程；13:37 的测试结束后额外样本未纳入下表。

| 检查点 | 时间 | Proxy 私有/工作集 MB | Proxy 线程/句柄 | Proxy GDI/USER | TestHost 私有/工作集 MB | TestHost 线程/句柄 | TestHost GDI/USER |
|---|---|---:|---:|---:|---:|---:|---:|
| T0 | 11:41:05 | 84.73 / 112.76 | 30 / 638 | 40 / 158 | 68.95 / 102.56 | 22 / 999 | 54 / 63 |
| T30 | 12:06:06 | 93.67 / 122.77 | 31 / 647 | 40 / 158 | 72.99 / 106.19 | 20 / 930 | 54 / 62 |
| T60 | 12:36:07 | 96.13 / 125.32 | 30 / 638 | 41 / 157 | 73.75 / 107.05 | 20 / 841 | 54 / 61 |
| T90 | 13:06:07 | 101.05 / 130.45 | 31 / 645 | 41 / 157 | 79.79 / 113.04 | 20 / 835 | 54 / 60 |
| T120 | 13:36:08 | 102.61 / 131.01 | 33 / 667 | 41 / 161 | 77.40 / 110.57 | 20 / 837 | 54 / 60 |

正式 T0-T120 运行态范围：

- Proxy：私有内存 `67.41–141.66 MB`，工作集 `96.58–162.57 MB`，线程 `30–34`，句柄 `638–673`，GDI `38–42`，USER `153–161`
- TestHost：私有内存 `68.95–80.23 MB`，工作集 `102.56–113.51 MB`，线程 `20–22`，句柄 `835–1006`，GDI `54`，USER `60–63`
- T30-T120 采样中 Proxy CPU 约 `5.32–5.51%`，TestHost CPU 约 `0.12–0.15%`；T0 CPU 因首次采样没有前一时间点，留空

资源判断：

- 句柄、线程、GDI、USER 均在有限范围内波动，没有持续单调增长。
- Proxy 私有内存 T0 到 T120 净增加 `17.88 MB`，TestHost 净增加 `8.45 MB`；简单线性拟合斜率约分别为 `0.03 MB/min` 和 `0.04 MB/min`。
- 私有内存存在 GC/工作集波动，10 分钟分箱均值并非单调上升；当前证据不支持“明确持续线性泄漏”的结论，但 T120 高于 T0，建议后续重复长稳或 24 小时曲线继续观察。

## 5. N1/N2/N3-A 与日志检查

正式时间窗内：

- Native DLL 日志：36,414 行，警告/错误级别 0 行
- C# Proxy 日志：18,665 行，警告 2 行、错误 0 行
- Proxy 的 2 条警告：1 条为启动阶段被更新请求作废的 `request_superseded`；1 条为 575 ms、HTTP 200 的慢指纹响应
- 日志和调用 CSV 未发现 `12002` 错误码、请求体不完整、Unexpected EOF、Socket timeout/reset 或响应读取失败
- `FailedCalls=0`、`CallbackErrors=0`、`ProcessPushCallbacksAfterEnd=0`

因此，本次核心曲线未见 N1 CallbackServer、N2 响应读取或 N3-A Proxy 请求体读取的系统性异常；慢响应作为性能观察项保留。

## 6. UI、清理与连接状态

- 正式配置启用了预览，TestHost 为 x86/STA；脚本完成了停止预览和 SDK 释放。
- 未能通过当前桌面自动化环境对前台 UI 做人工可视化观察，因此“无 UI 卡死”不能标记为完全验证。
- 4 次 `>=500 ms` 调用已记录为 UI 阻塞告警；它们均成功，但仍需真实前台 UI 观察确认用户界面体验。
- 测试宿主结束后退出；由本次测试启动的 Proxy PID 42936 已核验路径后停止。
- 收尾检查：测试 Proxy 不存在，8088/8089/9098/39091 均无监听；两个终端 9098 端口仍可达。

## 7. 验收结论与后续

本次正式核心长稳满足“运行至少 2 小时、核心业务 0 失败、无进程异常退出、无回调残留、N1/N2/N3-A 无系统性异常”的证据要求。由于真实样本驱动的 OCR/NFC/虹膜/授权路径未测试、前台 UI 未直接观察、私有内存存在小幅净上移，Stage 4.4 总体结论为 `PASS WITH OBSERVATIONS`，不将其扩大解释为全功能或 24 小时泄漏验收通过。

建议后续：

1. 使用真实 OCR/NFC/虹膜/授权样本补跑对应业务路径。
2. 在实际前台应用窗口下补做 UI 响应观察，重点复核 500 ms 以上采集调用。
3. 如需关闭资源泄漏风险，至少再做一次独立 2 小时或 24 小时曲线，比较稳定平台和末端斜率。

## 8. 证据文件

- `scripts/stress_results/stage4_4_core_20260905/full_flow_summary_20260905_113612.csv`
- `scripts/stress_results/stage4_4_core_20260905/full_flow_calls_20260905_113612.csv`
- `scripts/stress_results/stage4_4_core_20260905/full_flow_cycles_20260905_113612.csv`
- `scripts/stress_results/stage4_4_core_20260905/full_flow_callbacks_20260905_113612.csv`
- `scripts/stress_results/stage4_4_core_20260905/full_flow_metrics_20260905_113612.csv`
- `scripts/stress_results/stage4_4_core_20260905/resource_samples_20260905.csv`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/bin/x64/Release/net46/HZCYKJTHardWareExe_Logs/HZCYKJTHardWareExe_Logs_20260905.log`
- `demo/CSharpProxy/HZCYKJTHardWare.Proxy/bin/x86/Release/net46/HZCYKJTHardWareDLL_Logs/HZCYKJTHardWareDLL_Logs_20260905.log`
