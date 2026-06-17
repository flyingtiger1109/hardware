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

## 2026-06-15 方案A修复记录

### 本次修改内容

1. 为指纹预览、视频预览增加按 `terminalBaseUrl + resource_type` 维度缓存的预览URL。
2. 首次获取预览URL后写入缓存，后续启动预览优先使用缓存地址，减少切换终端后重新请求URL导致的旧请求覆盖风险。
3. 增加60秒定时校验逻辑，发现终端返回的预览URL变化时自动更新缓存；校验失败时保留旧缓存，不中断现有预览流程。
4. VLC首次播放失败时清理对应缓存并强制刷新URL重试一次，降低缓存地址失效导致预览失败的概率。
5. 外部预览启动和终端切换重启预览流程增加 `generation` 有效性校验，过期请求不再继续启动预览，也不再回调 `preview_failed`。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 预览启动外部行为：保持原接口，内部增加缓存、校验和过期请求拦截。

### 验证状态

- [x] C# Proxy x86 Release 编译验证：通过临时输出目录构建，0错误、0警告。
- [ ] 第三方切换终端后预览同步验证：待验证。
- [ ] 高频抓拍+切换终端组合验证：待验证。
- [ ] 长时间运行内存/句柄/线程数监控：待验证。

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

## 2026-06-15 方案B优化记录：HTTP MJPEG专用播放器 + VLC fallback

### 本次修改内容

1. 新增内部预览播放器接口 `IPreviewController`，用于统一 VLC 播放器和 HTTP MJPEG 播放器的生命周期管理。
2. 新增 `MjpegPreviewController`，HTTP/HTTPS 预览地址优先走专用 MJPEG 拉流与绘制逻辑，只保留最新 JPEG 帧，避免 VLC/TCP/MJPEG 缓冲积压导致 3-5 秒旧画面延迟。
3. `PreviewManager` 内部改为按预览 URL 类型选择播放器：HTTP/HTTPS 先用 MJPEG 专用播放器，启动失败时自动回退 VLC；RTSP/其他协议继续使用原 VLC 逻辑。
4. HTTP MJPEG 预览启动跳过 VLC 预热，避免首次预览额外打开隐藏 VLC 连接。
5. HTTP/HTTPS 预览 URL 缓存命中时改为绕过缓存重新向终端请求，降低复用旧 MJPEG 流会话的概率。
6. 日志增加 `Preview player selected: mjpeg/vlc` 和启动明细 `player=` 字段，便于现场确认实际播放器路径。

### 涉及文件

- `Preview/IPreviewController.cs`：新增内部播放器生命周期接口。
- `Preview/MjpegPreviewController.cs`：新增 HTTP MJPEG 专用播放器。
- `Preview/VlcPreviewController.cs`：实现 `IPreviewController`，保持原 VLC 行为。
- `Preview/PreviewManager.cs`：新增 HTTP MJPEG/VLC fallback 选择逻辑和 HTTP URL 缓存绕过。
- `todo.md`：记录本次优化。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 回调格式和错误码：未修改。
- VLC保留：保留，作为 HTTP MJPEG 启动失败 fallback，同时继续承担 RTSP/其他协议预览。
- 部署方式：未新增第三方依赖；仍为 C# Proxy 内部实现。

### 风险与注意事项

1. MJPEG 专用播放器当前按 JPEG SOI/EOI 标记解析帧，适用于标准 HTTP MJPEG；如果现场终端返回非标准私有封装，需要根据实际响应头和流格式补充解析。
2. 绘制逻辑使用 GDI 在子窗口直接绘制，已通过编译验证，但仍需在第三方外部 HWND、海光 C86 现场环境下验证窗口嵌入和刷新表现。
3. 如果 MJPEG 专用播放器启动超时或解析不到首帧，会自动 fallback 到 VLC；现场日志应检查是否真正出现 `Preview player selected: mjpeg`。

### 验证状态

- [x] C# Proxy x86 Release 编译验证：通过，0 错误，0 警告。
- [ ] 第三方电脑外部 HWND 预览验证：待验证。
- [ ] 海光 C86 高频抓拍 + 切换终端组合验证：待验证。
- [ ] HTTP MJPEG 长时间预览延迟验证：待验证。
- [ ] VLC fallback 验证：待验证。

### 回退方式

如现场 MJPEG 专用播放器出现兼容问题，可回退 `PreviewManager` 中 HTTP/HTTPS 分支选择逻辑，让 HTTP 地址重新直接走 `VlcPreviewController.StartAsync`；新增 `MjpegPreviewController.cs` 和 `IPreviewController.cs` 可保留但不再调用。

## 2026-06-15 HTTP MJPEG屏闪修复记录

### 本次修改内容

1. `MjpegPreviewController` 改为内存双缓冲绘制：先在内存 Bitmap 完成黑底和图像绘制，再一次性贴到预览子窗口。
2. 子窗口增加 `WM_ERASEBKGND` 拦截，阻止 Windows/父窗口重绘时擦白或擦黑。
3. 子窗口增加 `WM_PAINT` 处理，窗口被遮挡、切换或重绘时使用最后一帧补画，避免暴露空白背景。
4. 渲染轮询间隔从 15ms 调整为 20ms，降低 GDI 绘制压力，仍覆盖常见 MJPEG 帧率。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- VLC fallback：未修改。
- 新增依赖：无。

### 验证状态

- [x] C# Proxy x86 Release 临时输出目录编译验证：通过，0 错误，0 警告。
- [ ] 正式 Release 输出目录覆盖编译：待停止当前运行中的 `HZCYKJTHardWare.Proxy.exe` 后验证。
- [ ] 第三方电脑外部 HWND 屏闪验证：待验证。
- [ ] 海光 C86 高频抓拍 + 切换终端屏闪验证：待验证。

### 注意事项

当前正式 Release exe 正在运行，编译时无法覆盖 `bin\x86\Release\net46\HZCYKJTHardWare.Proxy.exe`；本次使用 `bin_compilecheck` 临时输出目录完成编译验证，临时目录已清理。

## 2026-06-16 HTTP MJPEG布局调整记录

### 本次修改内容

1. 按现场反馈，`MjpegPreviewController.ApplyCoverLayout` 恢复为 `cover` 铺满逻辑。
2. 视频子窗口重新按源比例放大到覆盖父句柄客户区，超出部分由父句柄裁剪。
3. 保留前一次屏闪修复中的双缓冲、`WM_ERASEBKGND` 和 `WM_PAINT` 处理。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- VLC fallback：未修改。
- 新增依赖：无。

### 验证状态

- [x] C# Proxy x86 Release 临时输出目录编译验证：通过，0 错误，0 警告。
- [ ] 正式 Release 输出目录覆盖编译：待停止当前运行中的 `HZCYKJTHardWare.Proxy.exe` 后验证。
- [ ] 第三方 Demo 外部 HWND `cover` 铺满效果：待现场验证。

## 2026-06-16 Preview HWND debug logging

### Change summary

1. DLL preview entries now log the valid third-party `HWND` geometry for `StartCameraPreview`, `StartFingerprintPreview`, and `StartIrisPreview`.
2. C# Proxy `HandlePreviewStart` logs the same target `HWND` before starting external preview, so DLL-side and exe-side handles can be compared.
3. `MjpegPreviewController` logs HTTP MJPEG render window creation, cover-layout results, and the first JPEG frame size.

### Compatibility

- DLL exports: unchanged.
- Third-party parameters: unchanged.
- JSON request/response fields: unchanged.
- Preview behavior: unchanged; this only adds low-frequency debug logs.
- Performance impact: logs are emitted only on preview start, layout changes, and first frame arrival, not on every frame.

### Verification

- [x] C# Proxy x86 Release compile check with temp output: passed, 0 errors, 0 warnings.
- [x] DLL Release|Win32 build: passed, 0 errors, 0 warnings.
- [ ] Third-party Demo现场日志比对：待验证。

## 2026-06-16 MJPEG stretch layout adjustment

### Change summary

1. `MjpegPreviewController` now sizes the internal render child window to exactly match the current parent `HWND` client area.
2. JPEG frames are stretched to fill the render window, so the full frame is visible inside the third-party handle without cover cropping.
3. VLC fallback and all exported DLL/API behavior remain unchanged.

### Verification

- [x] C# Proxy x86 Release temp-output compile check: passed, 0 errors, 0 warnings.
- [x] C# Proxy x86 Release formal `bin\x86\Release\net46` build: passed, 0 errors, 0 warnings.
- [ ] Third-party Demo现场拉伸铺满效果：待验证。

## 2026-06-16 HTTP preview rollback to VLC path

### Change summary

1. Disabled the HTTP MJPEG dedicated player selection in `PreviewManager`.
2. HTTP/HTTPS preview URLs now use `VlcPreviewController` directly, matching the previous VLC playback path.
3. `MjpegPreviewController` is kept in the source tree but is no longer selected by `PreviewManager`.

### Compatibility

- DLL exports: unchanged.
- Third-party parameters: unchanged.
- JSON request/response fields: unchanged.
- VLC fallback/runtime dependencies: unchanged.

### Verification

- [x] C# Proxy x86 Release temp-output compile check: passed, 0 errors, 0 warnings.
- [x] C# Proxy x86 Release formal `bin\x86\Release\net46` build: passed, 0 errors, 0 warnings.
- [ ] Third-party Demo现场回退效果：待验证。

## 2026-06-16 rollback to Scheme B initial MJPEG path

### Change summary

1. Restored HTTP/HTTPS preview selection to the Scheme B path: `MjpegPreviewController` first, `VlcPreviewController` fallback only when MJPEG start fails.
2. Reverted `MjpegPreviewController` to the initial low-latency direct GDI paint shape: 15ms render loop, latest-frame-only buffer, no later double-buffer/subclass/stretch-layout changes.
3. Formal x86 Release `net46` output was rebuilt for immediate verification.

### Compatibility

- DLL exports: unchanged.
- Third-party parameters: unchanged.
- JSON request/response fields: unchanged.
- VLC fallback: retained.

### Verification

- [x] C# Proxy x86 Release formal `bin\x86\Release\net46` build: passed, 0 errors, 0 warnings.
- [ ] Third-party Demo现场方案B初版效果：待验证。

## 2026-06-16 MJPEG anti-flicker on Scheme B fill layout

### Change summary

1. Kept Scheme B player selection unchanged: HTTP/HTTPS still uses `MjpegPreviewController` first and VLC only as fallback.
2. Kept the current `ApplyFillLayout` behavior unchanged, so the render child window still fills the third-party `HWND`.
3. Changed MJPEG rendering from direct clear-and-draw on the window DC to memory backbuffer drawing followed by a single blit to the window.
4. Added child-window subclass handling for `WM_ERASEBKGND` and `WM_PAINT`, so Windows repaint does not expose a white/black background between frames.

### Compatibility

- DLL exports: unchanged.
- Third-party parameters: unchanged.
- JSON request/response fields: unchanged.
- Current fill display behavior: unchanged.
- VLC fallback: retained.

### Verification

- [x] C# Proxy x86 Release temp-output compile check: passed, 0 errors, 0 warnings.
- [x] C# Proxy x86 Release formal `bin\x86\Release\net46` build: passed, 0 errors, 0 warnings.
- [ ] Third-party Demo现场闪屏验证：待验证。

## 2026-06-16 MJPEG anti-flicker adjustment without subclass paint

### Change summary

1. Removed the child-window subclass `WM_ERASEBKGND` / `WM_PAINT` handling that caused the preview to regress to partial display in the field.
2. Kept the current `ApplyFillLayout` behavior unchanged.
3. Kept memory backbuffer rendering, but only uses it in the normal frame path: draw frame to memory, then blit once to the render window.
4. Added `ValidateRect` after frame blit to reduce stale paint requests without taking over window paint messages.

### Verification

- [x] C# Proxy x86 Release temp-output compile check: passed, 0 errors, 0 warnings.
- [x] C# Proxy x86 Release formal `bin\x86\Release\net46` build: passed, 0 errors, 0 warnings.
- [ ] Third-party Demo现场铺满与闪屏验证：待验证。

## 2026-06-16 MJPEG direct paint without clear

### Cause

The previous backbuffer-only anti-flicker change removed flicker but caused partial display in the field. In the cross-process preview window, the off-screen bitmap size and the actually visible child-window area can diverge, so blitting the memory bitmap showed only the upper-left part of the rendered frame.

### Change summary

1. Removed the memory backbuffer path.
2. Restored direct drawing to the render window DC, matching the Scheme B initial display behavior.
3. Removed the per-frame `Clear(Color.Black)` call, so the frame is no longer erased before the new image is drawn.
4. Kept `ValidateRect` after drawing to reduce extra repaint requests without taking over `WM_PAINT`.

### Verification

- [x] C# Proxy x86 Release temp-output compile check: passed, 0 errors, 0 warnings.
- [x] C# Proxy x86 Release formal `bin\x86\Release\net46` build: passed, 0 errors, 0 warnings.
- [ ] Third-party Demo现场铺满与闪屏验证：待验证。
