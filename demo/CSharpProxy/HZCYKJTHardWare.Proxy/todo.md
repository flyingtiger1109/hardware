# HZCYKJTHardWare.Proxy 开发进度

## 当前阶段

Code Review + 长期运行稳定性修复（已完成）

## 修改文件清单

| 文件 | 修改内容 |
|------|----------|
| `Program.cs` | 全局异常处理 (UnhandledException/ThreadException/UnobservedTaskException) |
| `MainForm.cs` | 预览按钮改为 async/await，打开预览时禁用按钮防止重复点击 |
| `MainForm.Designer.cs` | （未修改） |
| `Infrastructure/AppConfig.cs` | 独立配置文件加载、save 配置项、兼容旧字段名 |
| `Infrastructure/Logger.cs` | 中文级别标签、跨天日志自动切换、线程安全写锁 |
| `Infrastructure/EncodingHelper.cs` | （未修改） |
| `Server/ProxyServer.cs` | SemaphoreSlim 并发限制(20)、火后不理异常保护、连接排空、VLC预热、ProcessSaveDir跟踪、优雅停止、202回调响应码 |
| `Server/DllCommandHandler.cs` | FSwitchingTerminal守卫、/preview/*/url路由、/authorize 2.21/2.22协议、ProcessSaveDir回退、性能计时、字典大小限制、IsWindow校验、预览失败回调 |
| `Server/DllCallbackSender.cs` | PostCallbackRaw公开方法 |
| `Server/TerminalCallbackHandler.cs` | 回调去重、孤儿回调跳过(不转发旧数据)、OCR事件中文化、OCR证据图片中文命名、async void改为同步、异常保护 |
| `Terminal/TerminalClient.cs` | ConnectionLimit连接池优化、HttpClient超时10s、CancelPendingRequests、HttpRequestException细分处理 |
| `Terminal/TerminalManager.cs` | ProcessSaveDir/ProcessActive状态跟踪、线程安全lock保护 |
| `Terminal/NetworkDetector.cs` | （未修改） |
| `Preview/PreviewManager.cs` | SynchronizationContext UI线程封送、VLC Play/Stop/Dispose主线程化 |
| `Preview/VlcPreviewPlayer.cs` | Form改为CreateWindowEx STATIC子窗口、libvlc_video_set_scale(0.0)、ApplyCoverLayout移至Play后、CleanupPartial资源释放、InvokeRequired判断 |
| `Preview/VlcResourceExtractor.cs` | （未修改） |
| `Storage/FileSaver.cs` | （未修改） |
| `Storage/PathHelper.cs` | 遵循CreateDateFolder/CreateRequestFolder配置开关 |
| `Parsing/*.cs` | （未修改） |
| `HZCYKJTHardWare.Proxy.csproj` | 独立配置文件引用（不再从Delphi目录复制） |
| `HZCYKJTHardWare.Proxy.json` | 新增独立配置文件 |

## 已完成事项

- [x] VLC视频预览改为STATIC子窗口 + 封面铺满布局
- [x] VLC预热（减少首帧延迟）
- [x] 终端切换守卫 _switchingTerminal（拦截切换期间新请求）
- [x] /preview/*/url 预览URL查询路由（对齐Delphi）
- [x] /authorize 改为2.21/2.22协议流程（字段映射 + port_code）
- [x] 连接排空机制（503 busy）
- [x] ProcessSaveDir / ProcessActive 状态跟踪
- [x] 终端切换性能计时日志
- [x] OCR事件类型中文化（20种事件）
- [x] OCR证据图片中文命名（红外/可见光/紫外）
- [x] 请求并发限制 SemaphoreSlim(20)
- [x] Task.Run异常保护（try/catch/finally）
- [x] 回调异常保护（try-catch包裹Handle）
- [x] 字典内存泄漏防护（超2000条自动清理）
- [x] TerminalManager线程安全（lock保护所有共享字段）
- [x] 全局异常处理器（UnhandledException/ThreadException/UnobservedTaskException）
- [x] 日志中文化（信息/警告/错误/致命）
- [x] 日志跨天自动切换
- [x] 回调去重（request_id + resource_type 组合键）
- [x] 孤儿回调跳过（request_id不在字典时不转发，防止旧终端数据回调）
- [x] HttpClient连接池优化（ConnectionLimit/MaxConnectionsPerServer/KeepAlive）
- [x] HttpClient超时10s + 细分异常捕获(HttpRequestException)
- [x] 预览失败时发送DLL错误回调（对齐Delphi TAsyncStartPreviewThread）
- [x] HWND有效性校验（IsWindow）
- [x] 独立配置文件 HZCYKJTHardWare.Proxy.json
- [x] 保存目录配置开关（create_date_folder / create_request_folder）

## 待验证事项

- [ ] 7x24小时连续运行（每秒5-8次请求）
- [ ] 终端切换期间高频抓拍不崩溃
- [ ] 终端切换后旧数据不回传DLL
- [ ] 回调去重验证（相同数据只回调一次）
- [ ] VLC预览铺满句柄验证
- [ ] 日志跨天自动切换验证
- [ ] 第三方调用兼容性验证（DLL接口不变）
- [ ] 内存/句柄/线程数72小时监控
- [ ] Win7 32位兼容性验证
- [ ] 高DPI环境验证（125%/150%缩放）

## 已知风险

1. `/authorize` 改为2.21/2.22协议流程，与Delphi不同（Delphi不转发终端直接回调）
   - 风险：DLL授权调用改为异步等待终端
   - 用户已确认暂不修改Delphi，C#先实施新协议
2. 预览启动为同步等待（await），不同于Delphi的异步线程
   - 风险：DLL等待HTTP响应时间略长（约1-2秒，仍在5s超时内）
   - 可后续改为Delphi的TAsyncStartPreviewThread模式
3. 字典清理为全量清空策略（超2000条），可能导致正在处理的请求丢失回调URL
   - 风险极低（2000条阈值正常操作不会达到）

## 下一步建议

1. 压力测试：每秒5-8次请求持续运行
2. 终端切换+高频抓拍组合测试
3. Win7 32位兼容性验证
4. 如需完全对齐Delphi异步预览启动模式，可将HandlePreviewStart改为fire-and-forget + 回调
