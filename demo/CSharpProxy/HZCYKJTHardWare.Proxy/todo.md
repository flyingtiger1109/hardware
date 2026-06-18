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

## 2026-06-18 后端服务 UI 方案B布局优化

### 本次修改内容

1. 将 `MainForm.Designer.cs` 从固定坐标按钮平铺改为“顶部状态条 + 分组操作区 + 三列预览卡片 + 底部日志面板”的控制台布局。
2. 增加服务状态、DLL监听、回调监听、当前终端的状态展示。
3. 为摄像头、指纹、虹膜预览区增加标题栏和状态标签，预览宿主控件仍使用原 `panelCamera` / `panelFingerprint` / `panelIris`。
4. 将日志区改为独立面板，保留原 `memoLog` 写入、限行和自动滚动逻辑。
5. `MainForm.cs` 仅增加 UI 状态标签刷新，不修改 `_server.*` 业务调用、请求参数、线程模型或接口行为。

### 涉及文件

- `MainForm.Designer.cs`：重排 UI 布局、按钮分组、预览卡片和日志区域样式。
- `MainForm.cs`：增加服务/终端/预览状态标签刷新。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- SDK/终端调用逻辑：未修改。
- 预览宿主 HWND：仍使用原三个预览 Panel，未改变传入 `StartLocalPreviewAsync` 的控件对象。

### 风险与注意事项

1. WinForms 视觉布局已通过编译，但仍需实际启动后检查不同 DPI 和窗口尺寸下的显示效果。
2. 顶部操作区使用 `FlowLayoutPanel` 横向排列，小尺寸窗口下可横向滚动查看完整按钮分组。
3. 预览区域容器增加标题栏，视频实际渲染区域为标题栏下方的黑色宿主 Panel。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动视觉检查：待验证。
- [ ] 摄像头/指纹/虹膜预览回归：待验证。
- [ ] 高DPI 125%/150%显示检查：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 本次修改即可恢复旧布局；未涉及配置、接口、DLL或业务模块。

## 2026-06-18 后端服务 UI 紧凑化与缩放调整

### 本次修改内容

1. 将默认窗口从 `1180x800` 调整为 `1100x740`，避免启动后占用过多屏幕空间。
2. 顶部功能区改为固定两行完整显示：第一行服务、流程、采集、测试，第二行完整预览控制。
3. 移除顶部功能区横向滚动，压缩按钮宽度、分组宽度、字体和间距。
4. 将主区域分隔条设为固定，避免用户通过拖动分隔条导致区域显示不完整。
5. 日志区域改为自动换行和垂直滚动，去掉横向滚动条。
6. 新增 UI 缩放输入框，支持 `90%`、`100%`、`110%`、`125%`，也支持用户输入 `85%` 到 `125%` 的自定义比例。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 业务按钮事件：未修改原调用链，只增加 UI 缩放事件。
- 预览宿主 Panel：未改名、未替换，仍由原预览逻辑使用。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动视觉检查：待验证。
- [ ] UI 缩放 90%/100%/110%/125% 切换检查：待验证。
- [ ] 预览区实际渲染回归：待验证。
- [ ] 高DPI 125%/150%显示检查：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 本次修改即可恢复上一个 UI 版本；未涉及业务模块、接口或配置文件。

## 2026-06-18 后端服务 UI 日志区与圆角卡片调整

### 本次修改内容

1. 将底部日志区高度加大，默认显示更多日志行。
2. 预览区高度相应收缩，但仍保留三路预览完整显示。
3. 增加 `RoundedPanel`，用于顶部状态卡、操作分组卡、预览卡片和日志卡片的圆角绘制。
4. 圆角半径控制为 8px，不引入第三方 UI 依赖。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 业务按钮事件：未修改。
- 预览宿主 Panel：未改名、未替换，仍由原预览逻辑使用。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动视觉检查：待验证。
- [ ] 圆角裁剪在高DPI下显示效果：待验证。
- [ ] 三路预览实际渲染回归：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 本次修改即可恢复上一版布局；未涉及业务模块、接口或配置文件。

## 2026-06-18 后端服务 UI 圆角可见性与日志区二次调整

### 本次修改内容

1. 再次加大底部日志区高度，默认可显示更多日志内容。
2. 相应压缩三路预览区高度，但三路预览卡片仍完整显示。
3. 修正 `RoundedPanel` 的圆角背景绘制，避免默认矩形背景削弱圆角视觉。
4. 预览卡片底部增加内边距，避免黑色预览宿主面板盖住卡片底部圆角。
5. 普通按钮增加 4px 轻微圆角，保持与卡片风格一致。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 业务按钮事件：未修改。
- 预览宿主 Panel：未改名、未替换，仍由原预览逻辑使用。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动视觉检查：待验证。
- [ ] 圆角在真实窗口和高DPI下显示效果：待验证。
- [ ] 三路预览实际渲染回归：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 本次布局和圆角绘制修改即可恢复上一版 UI；未涉及业务模块、接口或配置文件。

## 2026-06-18 后端服务 UI 圆角伪影、按钮配色与日志区三次调整

### 本次修改内容

1. 继续加大底部日志区高度，默认显示更多日志行。
2. 去除 `RoundedPanel` 的 `Region` 裁剪，改为先绘制父背景再绘制圆角白底和浅边框，减少右侧/底部黑边伪影。
3. 统一按钮颜色体系：主按钮使用同一主蓝色，普通按钮使用白底蓝字浅蓝边。
4. 终端选中状态改为同一主蓝色，避免与其他主按钮颜色不一致。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 业务按钮事件：未修改。
- 预览宿主 Panel：未改名、未替换，仍由原预览逻辑使用。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动视觉检查：待验证。
- [ ] 圆角黑边伪影现场检查：待验证。
- [ ] 按钮配色一致性检查：待验证。
- [ ] 三路预览实际渲染回归：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 本次 UI 修改即可恢复上一版；未涉及业务模块、接口或配置文件。

## 2026-06-18 后端服务 UI 服务按钮与按钮锯齿修正

### 本次修改内容

1. 调整服务控制卡片宽度，确保“启动服务”和“停止服务”两个按钮完整显示。
2. 服务按钮不再切换 `Enabled=false`，避免 WinForms 系统禁用态导致蓝底按钮文字颜色不统一；重复点击仍由现有 `_server == null` / `_server != null` 逻辑保护。
3. 移除按钮 `Region` 圆角裁剪，避免 GDI 裁剪导致按钮角出现锯齿/黑灰伪影。
4. 保留卡片圆角，按钮恢复为统一的直角浅边框风格。
5. 收窄采集按钮宽度，保证顶部第一行布局不挤出。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 业务按钮事件：未修改。
- 服务启动/停止保护：仍保留原 `_server` 判空逻辑。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动视觉检查：待验证。
- [ ] 服务按钮显示和颜色一致性：待验证。
- [ ] 按钮角锯齿现场检查：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 本次 UI 修改即可恢复上一版；未涉及接口、配置或业务模块。

## 2026-06-18 终端显示名称中文化

### 本次修改内容

1. 将界面按钮“终端 1 / 终端 2”改为“左通道 / 右通道”。
2. `AppConfig` 新增 `Terminal1Name` / `Terminal2Name`，默认值为“左通道 / 右通道”。
3. `TerminalManager` 改为从配置读取终端显示名称，启动日志和切换日志使用中文通道名。
4. `ProxyServer.SwitchTerminal` 返回文本改为中文通道名。
5. 同步更新 `HZCYKJTHardWare.json` 和 `HZCYKJTHardWare.Proxy.json` 中 terminal name 值。

### 兼容性说明

- 终端索引：仍然使用 `1 / 2`，未修改。
- 第三方调用参数：未修改。
- JSON字段结构：未修改，只调整 `name` 字段值。
- 终端 IP/端口/host_suffix：未修改。
- DLL导出函数：未修改。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动后当前终端显示：待验证。
- [ ] 左/右通道切换日志显示：待验证。
- [ ] 第三方终端切换请求兼容性：待验证。

### 回退方式

- 回退 `AppConfig.cs`、`TerminalManager.cs`、`ProxyServer.cs`、`MainForm.*` 和两个 JSON 文件中的本次显示名称修改即可恢复上一版。

## 2026-06-18 后端服务 UI 状态按钮配色调整

### 本次修改内容

1. 增加统一的状态按钮刷新逻辑，使用同一套主蓝色和普通白底蓝字样式。
2. 服务控制按钮改为随服务状态显示：运行中高亮“启动服务”，已停止高亮“停止服务”。
3. 流程按钮改为随流程状态显示：流程开始后高亮“开始流程”，流程结束后高亮“结束流程”。
4. 摄像头、指纹、虹膜预览按钮改为随预览状态显示：预览中高亮“开始”，已停止或启动失败时高亮“停止”。
5. 人脸抓拍、指纹抓拍、OCR 阅读、IC 卡识别、虹膜抓拍、授权测试等单次触发按钮保持普通样式。

### 兼容性说明

- DLL导出函数：未修改。
- 第三方调用参数：未修改。
- JSON请求/响应字段：未修改。
- 业务调用链：未修改，仅调整 WinForms 内部按钮颜色刷新。
- 车牌预览：当前业务逻辑仅提示“不支持”，未纳入状态高亮，避免误导运行状态。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动后按钮状态显示：待验证。
- [ ] 服务启动/停止按钮高亮切换：待验证。
- [ ] 流程开始/结束按钮高亮切换：待验证。
- [ ] 三路预览开始/停止按钮高亮切换：待验证。

### 回退方式

- 回退 `MainForm.cs` 中本次状态按钮刷新逻辑即可恢复上一版配色行为；未涉及接口、配置或业务模块。

## 2026-06-18 后端服务 UI 状态一致性问题修复

### 本次修改内容

1. 修复终端切换 UI 提前显示成功的问题：新增 `SwitchTerminalAsync`，UI 等待停止预览、切换终端、恢复预览完成后再刷新当前通道。
2. 保留原 `SwitchTerminal(int)` 方法签名，改为内部等待异步切换完成，不改变外部接口名称和参数。
3. 修复预览启动异常保护不足的问题：摄像头、指纹、虹膜预览启动统一进入 `StartPreviewFromUiAsync`，异常会写日志并恢复按钮状态。
4. 预览启动过程中不再使用 `Enabled=false`，避免 WinForms 禁用态覆盖按钮颜色。
5. 增加预览启动/停止状态锁：启动中重复点击会被忽略，启动中点击停止会标记“启动后停止”，避免 UI 状态错乱。
6. UI 缩放后对顶部功能分组、按钮尺寸、主分隔高度进行目标比例归一，减少反复缩放导致的取整偏移。
7. 顶部状态栏增加响应式布局和 `AutoEllipsis`，当前终端长文本优先自适应，空间不足时省略显示，避免挤压缩放控件。

### 兼容性说明

- DLL导出函数：未修改。
- HTTP API 请求/响应字段：未修改。
- JSON字段结构：未修改。
- 业务请求参数：未修改。
- UI 内部行为：终端切换现在等待真实切换完成后再刷新状态，预览按钮颜色不再依赖禁用态。

### 风险与注意事项

1. `SwitchTerminal(int)` 现在会等待切换完成后返回；目前静态检查只发现 UI 使用该方法，未发现第三方直接调用 C# 类方法。
2. 预览启动中点击停止会在启动返回后立即执行停止，仍需在真实设备上确认 SDK 对该时序的响应。
3. UI 缩放布局已编译通过，但仍需实际启动后检查 85%/90%/100%/110%/125% 的视觉效果。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动视觉检查：待验证。
- [ ] 左/右通道切换后状态与实际通道一致性：待验证。
- [ ] 摄像头/指纹/虹膜预览启动异常恢复：待验证。
- [ ] 预览启动中点击停止的状态一致性：待验证。
- [ ] UI 缩放 85%/90%/100%/110%/125% 切换检查：待验证。

### 回退方式

- 回退 `MainForm.cs` 和 `ProxyServer.cs` 中本次状态同步、预览状态锁和缩放归一化修改即可恢复上一版行为；未涉及 DLL、HTTP API 或 JSON 字段结构。

## 2026-06-18 顶部状态栏响应式布局回退

### 本次修改内容

1. 回退顶部状态栏响应式重排逻辑，移除 `InitializeResponsiveStatusBar` 和 `LayoutStatusBar`。
2. 移除顶部状态栏相关 `AutoEllipsis` 设置。
3. 移除 UI 缩放结束后强制重排顶部状态栏的调用。
4. 保留终端切换状态同步、预览异常保护、预览启动/停止状态锁和缩放归一化等其他修复。

### 兼容性说明

- DLL导出函数：未修改。
- HTTP API 请求/响应字段：未修改。
- JSON字段结构：未修改。
- 业务请求参数：未修改。
- UI影响范围：仅恢复顶部状态栏固定坐标布局。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动后顶部状态栏显示：待验证。
- [ ] UI 缩放后顶部状态栏显示：待验证。

### 回退方式

- 如需恢复响应式状态栏，可重新引入 `InitializeResponsiveStatusBar` / `LayoutStatusBar` 及构造函数调用。

## 2026-06-18 顶部状态栏固定布局放大

### 本次修改内容

1. 将默认窗口从 `1100x740` 调整为 `1250x760`，增加顶部状态栏横向空间。
2. 将最小窗口宽度调整为 `1250`，避免用户缩小后再次导致顶部状态栏显示不全。
3. 扩大顶部状态栏宽度，并重新分配固定坐标：标题、服务状态、DLL监听、回调监听、当前终端、缩放控件之间保留更大间距。
4. 将当前终端显示值宽度从 `300` 扩大到 `390`，减少通道名和 URL 被截断的概率。
5. 同步调整预览区、日志区、表格和日志文本框的设计尺寸，保持整体布局完整。

### 兼容性说明

- DLL导出函数：未修改。
- HTTP API 请求/响应字段：未修改。
- JSON字段结构：未修改。
- 业务请求参数：未修改。
- UI影响范围：仅调整默认窗口和固定布局尺寸。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动后顶部状态栏显示：待验证。
- [ ] 100% 缩放下当前终端和缩放控件显示：待验证。
- [ ] 90%/110%/125% UI 缩放显示：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 中本次窗口尺寸、顶部状态栏坐标和最小窗口尺寸调整即可恢复上一版。

## 2026-06-18 回退默认窗口尺寸并重排顶部状态字段

### 本次修改内容

1. 回退上一版放大窗口调整，默认窗口恢复为 `1100x740`。
2. 最小窗口尺寸恢复为 `1100x700`，保持之前不铺满屏幕的窗口大小。
3. 保留顶部状态栏固定坐标方案，不恢复响应式布局。
4. 重新压缩顶部状态栏内部间距：标题和服务状态向左收，DLL监听、回调监听、当前终端重新排布。
5. 当前终端显示区域调整为 `325` 宽，缩放控件固定靠右显示。

### 兼容性说明

- DLL导出函数：未修改。
- HTTP API 请求/响应字段：未修改。
- JSON字段结构：未修改。
- 业务请求参数：未修改。
- UI影响范围：仅调整默认窗口尺寸和顶部状态栏固定坐标。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动后顶部状态栏显示：待验证。
- [ ] 100% 缩放下 DLL监听/回调监听/当前终端显示：待验证。
- [ ] 90%/110%/125% UI 缩放显示：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 中本次窗口尺寸与顶部状态栏坐标调整即可恢复上一版放大窗口方案。

## 2026-06-18 流程与终端卡片拆分

### 本次修改内容

1. 将原“流程与终端”卡片拆分为两个独立卡片：“流程控制”和“终端切换”。
2. “流程控制”仅保留“开始流程 / 结束流程”按钮。
3. “终端切换”仅保留“左通道 / 右通道”按钮。
4. 收窄服务、流程、终端、采集卡片宽度，保证默认 `1100x740` 窗口下第一行仍能完整显示。
5. 同步更新 UI 缩放归一化参数，避免切换缩放后恢复旧卡片宽度。

### 兼容性说明

- DLL导出函数：未修改。
- HTTP API 请求/响应字段：未修改。
- JSON字段结构：未修改。
- 业务请求参数：未修改。
- UI影响范围：仅调整顶部操作卡片分组和尺寸。

### 验证状态

- [x] C# Proxy Release 编译验证：`dotnet build --no-restore` 通过，0错误、0警告。
- [ ] 程序启动后第一行卡片完整显示：待验证。
- [ ] 流程控制和终端切换按钮状态高亮：待验证。
- [ ] UI 缩放 90%/100%/110%/125% 后卡片布局：待验证。

### 回退方式

- 回退 `MainForm.Designer.cs` 和 `MainForm.cs` 中本次卡片拆分与缩放归一化参数修改即可恢复“流程与终端”合并卡片。
## 2026-06-18 方案A稳定性优化记录

### 本次修改内容

1. `MjpegPreviewController.AbortRequest` 改为锁内仅交换并清空 `_request`，锁外调用 `Abort()`，避免在 `_requestLock` 内执行可能阻塞的外部 API。
2. `MjpegPreviewController.JoinReaderThread` 增加 reader 线程停止超时日志，记录线程名、停止标志、取消状态和 URL，便于高频启停问题定位。
3. `MjpegPreviewController.AppendBytes` 改为批量追加读取缓冲，减少高频 MJPEG 数据逐字节写入开销。
4. `Logger` 从每条日志立即 `Flush()` 改为按 100 条或 1 秒空闲批量刷盘，保留 `Logger.Flush()` 主动立即刷盘语义。
5. `src/preview_manager.cpp` 中 `PreviewManager::Instance()` 改为 C++ 函数内静态单例，消除裸指针懒初始化的线程安全风险。

### 兼容性说明

- DLL 导出函数：未修改。
- 第三方调用参数：未修改。
- JSON 请求/响应字段：未修改。
- 错误码和回调格式：未修改。
- 新增第三方依赖：无。
- 部署方式：未修改。

### 风险与注意事项

1. `Logger` 改为批量刷盘后，异常进程退出时最后 1 秒内的少量日志仍依赖 `ProcessExit` 中的 `Flush(1000)`，需要长稳验证。
2. `MjpegPreviewController.AppendBytes` 使用 `ArraySegment<byte>` 批量追加，需要通过 .NET Framework 4.6 编译验证确认目标框架兼容。
3. `PreviewManager::Instance()` 改为静态局部对象后，进程卸载时会执行析构；正常 `ReleaseSdk()` 仍会先 `StopAllRenderers()`，需要通过重复 Init/Release 验证析构时序无副作用。

### 验证状态

- [ ] C# Proxy x86 Release 编译验证：待验证。
- [ ] DLL Win32 Release 编译验证：待验证。
- [ ] MJPEG 预览高频启停 100 次：待验证。
- [ ] InitSdk/ReleaseSdk 重复调用：待验证。
- [ ] 高频日志写入：待验证。

### 回退方式

- 回退本次修改文件：`Preview/MjpegPreviewController.cs`、`Infrastructure/Logger.cs`、`src/preview_manager.cpp`、`todo.md`。
- 如现场出现 MJPEG 停止异常，可先回退 `MjpegPreviewController.cs`。
- 如日志丢失排查困难，可先恢复 `Logger.cs` 每条写入后立即 `Flush()` 的旧逻辑。
