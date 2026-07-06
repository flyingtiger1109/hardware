using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Server;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy
{
    public partial class MainForm : Form
    {
        private ProxyServer _server;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private Icon _appIcon;
        private System.Windows.Forms.Timer _uiLogTimer;
        private readonly ConcurrentQueue<string> _pendingUiLogs = new ConcurrentQueue<string>();
        private int _pendingUiLogCount;
        private int _pendingFaceCaptureSuccessCount;
        private int _pendingFingerprintCaptureSuccessCount;
        private DateTime _lastCaptureSummaryUtc = DateTime.UtcNow;
        private bool _exitRequested;
        private int _headerTerminalIndex = 1;

        private System.Windows.Forms.Timer _monitorTimer;
        private System.Threading.Timer _midnightClearTimer;
        private string _lastClearDate;
        private DateTime _processStartTime = DateTime.Now;
        private TimeSpan _lastCpuTime;
        private DateTime _lastCpuSample;

        private string _historyCurrentFile;
        private int _historyLoading;
        private readonly Font _logFont = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);
        private readonly LinkedList<LogLine> _historyLines = new LinkedList<LogLine>();
        private readonly LinkedList<LogLine> _activeLines = new LinkedList<LogLine>();
        private bool _historyMode;

        private sealed class LogLine
        {
            public DateTime Timestamp;
            public string Text;
            public Color ForeColor;
        }

        public MainForm()
        {
            InitializeComponent();
            memoLog.Font = _logFont;
            DisableLogUndoBuffer();

            // 1. 设置工具栏安全高度 (稍微加大到 60，给高 DPI 留足垂直空间)
            panelLogToolbar.Height = 60;

            // 2. 恢复原生勾选状态，并开启 AutoSize 测量真实文字宽高
            chkAutoScroll.Appearance = Appearance.Normal;
            chkErrorOnly.Appearance = Appearance.Normal;
            chkAutoScroll.AutoSize = true;
            chkErrorOnly.AutoSize = true;

            // 动态计算需要的宽度和高度！(核心修复：不再写死 32 高度)
            int targetHeight = Math.Max(32, chkAutoScroll.PreferredSize.Height + 12);
            int autoWidth = chkAutoScroll.PreferredSize.Width + 16;
            int errWidth = chkErrorOnly.PreferredSize.Width + 16;

            // 3. 动态创建带边框的 Panel 作为包裹外壳
            Panel pnlAuto = new Panel
            {
                Size = new Size(autoWidth, targetHeight), // 宽度和高度全部动态分配
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(8, 0, 0, 0)
            };
            chkAutoScroll.AutoSize = false;
            chkAutoScroll.Parent = pnlAuto;
            chkAutoScroll.Dock = DockStyle.Fill;
            pnlAuto.Parent = panelLogToolbar;

            Panel pnlError = new Panel
            {
                Size = new Size(errWidth, targetHeight),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(8, 0, 0, 0)
            };
            chkErrorOnly.AutoSize = false;
            chkErrorOnly.Parent = pnlError;
            chkErrorOnly.Dock = DockStyle.Fill;
            pnlError.Parent = panelLogToolbar;

            // 4. 动态设置右侧按钮的宽度，高度与前面的复选框外壳保持一致
            btnClearLog.AutoSize = true;
            btnExportLog.AutoSize = true;
            int clearWidth = btnClearLog.PreferredSize.Width + 24;
            int exportWidth = btnExportLog.PreferredSize.Width + 24;

            btnClearLog.AutoSize = false;
            btnExportLog.AutoSize = false;
            btnClearLog.Size = new Size(clearWidth, targetHeight);
            btnExportLog.Size = new Size(exportWidth, targetHeight);

            // 统一按钮的扁平化边框风格
            Button[] buttons = { btnClearLog, btnExportLog };
            foreach (var btn in buttons)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
                btn.BackColor = Color.White;
            }

            // 5. 绝对对齐与锚定 (排队逻辑)
            pnlAuto.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            pnlError.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnExportLog.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            int gap = 20;
            // 核心修复：根据实际算出的高度，动态计算 Y 坐标，确保完美垂直居中，绝不触底！
            int alignY = (panelLogToolbar.Height - targetHeight) / 2;

            pnlAuto.Location = new Point(16, alignY);
            pnlError.Location = new Point(pnlAuto.Right + gap, alignY);
            btnClearLog.Location = new Point(pnlError.Right + gap, alignY);
            btnExportLog.Location = new Point(btnClearLog.Right + gap, alignY);

            // 硬件状态标签
            var lblHardwareStatus = new Label
            {
                AutoSize = true,
                Text = "硬件状态：等待检测",
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Parent = panelLogToolbar
            };
            lblHardwareStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            lblHardwareStatus.Location = new Point(btnExportLog.Right + gap, alignY);

            InitCardLayouts();
            ApplyUIPolish();
            InitializeTrayIcon();
            InitializeUiLogTimer();
            InitializeMonitorTimer();
            InitializeMidnightClearTimer();
            UpdateMonitorInfo();
        }

        private void InitCardLayouts()
        {
            SetupGrid2x3(tlpService);
            AddToGrid(tlpService, 0, 0, btnStartServer, "启动服务", btnStartServer_Click);
            AddToGrid(tlpService, 1, 0, btnStopServer, "停止服务", btnStopServer_Click);
            AddToGrid(tlpService, 0, 1, btnStartProcess, "开始流程", btnStartProcess_Click);
            AddToGrid(tlpService, 1, 1, btnEndProcess, "结束流程", btnEndProcess_Click);
            AddToGrid(tlpService, 0, 2, btnSwitchTerminal1, "左通道", btnSwitchTerminal1_Click);
            AddToGrid(tlpService, 1, 2, btnSwitchTerminal2, "右通道", btnSwitchTerminal2_Click);

            SetupGrid2x3(tlpOperation);
            AddToGrid(tlpOperation, 0, 0, btnFaceCapture, "人脸抓拍", btnFaceCapture_Click);
            AddToGrid(tlpOperation, 1, 0, btnFingerprintCapture, "指纹抓拍", btnFingerprintCapture_Click);
            AddToGrid(tlpOperation, 0, 1, btnOCR, "OCR 阅读", btnOCR_Click);
            AddToGrid(tlpOperation, 1, 1, btnNfcCard, "IC 卡识别", btnNfcCard_Click);
            AddToGrid(tlpOperation, 0, 2, btnIrisCapture, "虹膜抓拍", btnIrisCapture_Click);
            AddToGrid(tlpOperation, 1, 2, btnAuthorize, "授权测试", btnAuthorize_Click);

            SetupGrid2x6(tlpPreviewControl);
            AddToGrid(tlpPreviewControl, 0, 0, btnStartCameraPreview, "开始摄像头预览", btnStartCameraPreview_Click);
            AddToGrid(tlpPreviewControl, 1, 0, btnStopCameraPreview, "停止摄像头预览", btnStopCameraPreview_Click);
            AddToGrid(tlpPreviewControl, 0, 1, btnStartFingerprintPreview, "开始指纹预览", btnStartFingerprintPreview_Click);
            AddToGrid(tlpPreviewControl, 1, 1, btnStopFingerprintPreview, "停止指纹预览", btnStopFingerprintPreview_Click);
            AddToGrid(tlpPreviewControl, 0, 2, btnStartIrisPreview, "开始虹膜预览", btnStartIrisPreview_Click);
            AddToGrid(tlpPreviewControl, 1, 2, btnStopIrisPreview, "停止虹膜预览", btnStopIrisPreview_Click);
            AddToGrid(tlpPreviewControl, 0, 3, btnStartPlatePreviewCJ, "开始出境车牌预览", btnStartPlatePreviewCJ_Click);
            AddToGrid(tlpPreviewControl, 1, 3, btnStopPlatePreviewCJ, "停止出境车牌预览", btnStopPlatePreviewCJ_Click);
            AddToGrid(tlpPreviewControl, 0, 4, btnStartPlatePreviewRJ2, "开始入境车牌预览 2", btnStartPlatePreviewRJ2_Click);
            AddToGrid(tlpPreviewControl, 1, 4, btnStopPlatePreviewRJ2, "停止入境车牌预览 2", btnStopPlatePreviewRJ2_Click);
            AddToGrid(tlpPreviewControl, 0, 5, btnStartPlatePreviewRJ3, "开始入境车牌预览 3", btnStartPlatePreviewRJ3_Click);
            AddToGrid(tlpPreviewControl, 1, 5, btnStopPlatePreviewRJ3, "停止入境车牌预览 3", btnStopPlatePreviewRJ3_Click);
        }

        private void ApplyUIPolish()
        {
            // 1. Card title separators
            AddSeparator(cardService);
            AddSeparator(cardOperation);
            AddSeparator(cardPreviewControl);

            // 2. Force 50/50 columns + percent row heights
            ResetGridStyles(tlpService, 3);
            ResetGridStyles(tlpOperation, 3);
            ResetGridStyles(tlpPreviewControl, 6);
            tlpPreviewControl.Padding = new Padding(0, 2, 0, 2);

            // 3. Video container
            panelPreview.BackColor = Color.FromArgb(249, 250, 251);
            panelPreview.Padding = new Padding(12, 8, 12, 12);
            panelCamera.Margin = new Padding(0, 0, 0, 6);
            panelFingerprint.Margin = new Padding(0, 0, 0, 6);
            panelIris.Margin = new Padding(0, 0, 0, 6);
            panelPlateCJ.Margin = new Padding(0, 6, 0, 0);
            panelPlateRJ2.Margin = new Padding(0, 6, 0, 0);
            panelPlateRJ3.Margin = new Padding(0, 6, 0, 0);
        }

        private static void AddSeparator(Control parent)
        {
            var sep = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(229, 231, 235) };
            parent.Controls.Add(sep);
            parent.Controls.SetChildIndex(sep, 1);
        }

        private static void ResetGridStyles(TableLayoutPanel tlp, int rowCount)
        {
            tlp.ColumnStyles.Clear();
            tlp.ColumnCount = 2;
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            tlp.RowStyles.Clear();
            float pct = 100f / rowCount;
            for (int i = 0; i < rowCount; i++)
                tlp.RowStyles.Add(new RowStyle(SizeType.Percent, pct));
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            UpdateHeaderStatus();
            Logger.Info("应用程序启动中...");
            memoLog.ScrolledToTop += OnLogScrolledToTop;
            memoLog.ScrolledToBottom += OnLogScrolledToBottom;
            chkAutoScroll.CheckedChanged += (s, ev) =>
            {
                memoLog.AutoScroll = chkAutoScroll.Checked;
                if (chkAutoScroll.Checked)
                    EnterLiveMode();
            };
            btnClearLog.Click += (s, ev) => ClearLog();
            btnExportLog.Click += (s, ev) => ExportLog();
            // Auto-start server on launch (direct call for immediate listener startup)
            btnStartServer_Click(null, null);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                var action = ShowCloseActionDialog();
                if (action == CloseAction.MinimizeToTray)
                {
                    e.Cancel = true;
                    AppendLog("关闭窗口：最小化到托盘，后台服务继续运行");
                    HideToTray();
                    return;
                }

                if (action == CloseAction.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                _exitRequested = true;
            }

            if (_trayIcon != null)
                _trayIcon.Visible = false;

            _uiLogTimer?.Stop();
            FlushPendingUiLogs();
            StopServer();
            Logger.Flush(1000);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
                HideToTray();
        }

        private void InitializeTrayIcon()
        {
            _appIcon = LoadApplicationIcon();
            if (_appIcon != null)
                Icon = _appIcon;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("显示主窗口", null, (s, e) => RestoreFromTray());
            _trayMenu.Items.Add("退出程序", null, (s, e) => RequestApplicationExit());

            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon ?? SystemIcons.Application,
                Text = "HZCYJKTHardWare 后端服务",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private enum CloseAction
        {
            Cancel,
            MinimizeToTray,
            Exit
        }

        private CloseAction ShowCloseActionDialog()
        {
            var selectedAction = CloseAction.Cancel;

            using (var dialog = new Form())
            using (var message = new Label())
            using (var buttonsPanel = new FlowLayoutPanel())
            using (var btnMinimize = new Button())
            using (var btnExit = new Button())
            {
                dialog.Text = "关闭后端服务";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ClientSize = new Size(620, 230);
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.Font = Font;

                message.AutoSize = false;
                message.Location = new Point(24, 34);
                message.Size = new Size(572, 64);
                message.Text = "请选择关闭方式：\r\n最小化到托盘会保持后台服务继续运行。";
                message.TextAlign = ContentAlignment.MiddleCenter;

                buttonsPanel.AutoSize = false;
                buttonsPanel.FlowDirection = FlowDirection.LeftToRight;
                buttonsPanel.Location = new Point(92, 142);
                buttonsPanel.Size = new Size(436, 52);
                buttonsPanel.WrapContents = false;

                btnMinimize.Text = "最小化到托盘";
                btnMinimize.AutoSize = true;
                btnMinimize.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                btnMinimize.MinimumSize = new Size(230, 40);
                btnMinimize.Margin = new Padding(0, 4, 32, 4);
                btnMinimize.Click += (s, e) =>
                {
                    selectedAction = CloseAction.MinimizeToTray;
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                btnExit.Text = "退出程序";
                btnExit.AutoSize = true;
                btnExit.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                btnExit.MinimumSize = new Size(170, 40);
                btnExit.Margin = new Padding(0, 4, 0, 4);
                btnExit.Click += (s, e) =>
                {
                    selectedAction = CloseAction.Exit;
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                dialog.AcceptButton = btnMinimize;
                buttonsPanel.Controls.Add(btnMinimize);
                buttonsPanel.Controls.Add(btnExit);
                dialog.Controls.Add(message);
                dialog.Controls.Add(buttonsPanel);
                dialog.ShowDialog(this);
            }

            return selectedAction;
        }

        private void SetupGrid2x3(TableLayoutPanel tlp)
        {
            tlp.ColumnCount = 2;
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.RowCount = 3;
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334F));
            tlp.Dock = DockStyle.Fill;
        }

        private void SetupGrid2x6(TableLayoutPanel tlp)
        {
            tlp.ColumnCount = 2;
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.RowCount = 6;
            for (var row = 0; row < 6; row++)
                tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 6F));
            tlp.Dock = DockStyle.Fill;
        }

        private void AddToGrid(TableLayoutPanel tlp, int col, int row, Button btn, string text, EventHandler handler)
        {
            btn.BackColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(219, 229, 254);
            btn.Font = new Font("Microsoft YaHei", 9F);
            btn.ForeColor = Color.FromArgb(13, 110, 253);
            btn.Text = text;
            btn.UseVisualStyleBackColor = false;
            btn.Dock = DockStyle.Fill;
            btn.Margin = new Padding(4);
            btn.Click += handler;
            tlp.Controls.Add(btn, col, row);
        }

        private void RequestApplicationExit()
        {
            _exitRequested = true;
            Close();
        }

        private static Icon LoadApplicationIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }

        internal void HideToTrayForExternalPreview()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(HideToTrayForExternalPreview));
                return;
            }

            // Match the v0.9 external-preview behavior: remove the Proxy top-level
            // window from the taskbar so an embedded child cannot activate it.
            HideToTray();
        }

        private void HideToTray()
        {
            try
            {
                ShowInTaskbar = false;
                Hide();
                _trayIcon.Visible = true;
            }
            catch
            {
                // 托盘隐藏失败不能影响后台服务运行
            }
        }

        private void RestoreFromTray()
        {
            try
            {
                ShowInTaskbar = true;
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            }
            catch
            {
                // 托盘恢复失败不能影响后台服务运行
            }
        }

        private void InitializeUiLogTimer()
        {
            _uiLogTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _uiLogTimer.Tick += (s, e) => FlushPendingUiLogs();
            _uiLogTimer.Start();
        }

        private void InitializeMonitorTimer()
        {
            _lastCpuSample = DateTime.Now;
            _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            _monitorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _monitorTimer.Tick += (s, e) => UpdateMonitorInfo();
            _monitorTimer.Start();
        }

        private void InitializeMidnightClearTimer()
        {
            _lastClearDate = DateTime.Now.ToString("yyyyMMdd");
            _midnightClearTimer = new System.Threading.Timer(MidnightCheckCallback, null,
                30_000, 30_000);
        }

        private void MidnightCheckCallback(object state)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyyMMdd");
                if (_lastClearDate == today) return;
                _lastClearDate = today;

                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (IsDisposed) return;
                        ClearLog();
                        Logger.Info("[日志管理] 每日0点自动清空UI日志区");
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private void btnStartServer_Click(object sender, EventArgs e)
        {
            if (_server != null) return;
            try
            {
                _server = new ProxyServer(AppendLog, OnProcessStateChanged, OnTerminalChanged, OnHealthChanged);
                _server.Start();
                btnStartServer.Enabled = true;
                btnStopServer.Enabled = true;
                UpdateHeaderStatus();
                AppendLog("服务已启动");
            }
            catch (Exception ex)
            {
                try { _server?.Dispose(); } catch { }
                _server = null;
                UpdateHeaderStatus();
                AppendLog("启动服务失败: " + ex.Message);
            }
        }

        private void btnStopServer_Click(object sender, EventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            if (_server == null) return;
            try
            {
                var server = _server;
                _server = null;
                server.Dispose();
                btnStartServer.Enabled = true;
                btnStopServer.Enabled = false;
                UpdateHeaderStatus();
                ResetPreviewButtonStates();
                SetPersistentButtonStyle(btnStartProcess, false);
                AppendLog("服务已停止");
            }
            catch (Exception ex)
            {
                UpdateHeaderStatus();
                AppendLog("停止服务失败: " + ex.Message);
            }
        }

        private void UpdateHeaderStatus()
        {
            var isRunning = btnStopServer != null && btnStopServer.Enabled;
            lblServiceStatus.Text = isRunning ? "● 运行中" : "● 已停止";
            lblServiceStatus.ForeColor = isRunning
                ? Color.FromArgb(22, 163, 74)
                : Color.FromArgb(100, 116, 139);
            SetPersistentButtonStyle(btnStartServer, isRunning);
            SetPersistentButtonStyle(btnSwitchTerminal1, _headerTerminalIndex == 1);
            SetPersistentButtonStyle(btnSwitchTerminal2, _headerTerminalIndex == 2);
            SetBusinessButtonsEnabled(isRunning);

            try
            {
                var config = AppConfig.Instance;
                lblDllListenValue.Text = string.Format("{0}:{1}", config.DllServerHost, config.DllServerPort);
                lblCallbackListenValue.Text = string.Format("{0}:{1}", config.CallbackListenHost, config.CallbackListenPort);

                var terminalName = _headerTerminalIndex == 1 ? "终端 1 / 左通道" : "终端 2 / 右通道";
                var hostSuffix = _headerTerminalIndex == 1
                    ? config.Terminal1HostSuffix
                    : config.Terminal2HostSuffix;
                var terminalUrl = string.Format(
                    "{0}://{1}.{2}:{3}",
                    config.TerminalScheme,
                    config.SubnetPrefix,
                    hostSuffix,
                    config.TerminalPort);
                lblTerminalValue.Text = string.Format("{0} ({1})", terminalName, terminalUrl);
            }
            catch (Exception ex)
            {
                lblDllListenValue.Text = "配置读取失败";
                lblCallbackListenValue.Text = "配置读取失败";
                lblTerminalValue.Text = "配置读取失败";
                Logger.Warn("Header 配置状态更新失败: " + ex.Message);
            }
        }

        private void OnProcessStateChanged(bool active)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(OnProcessStateChanged), active);
                return;
            }
            SetPersistentButtonStyle(btnStartProcess, active);
        }

        private void OnTerminalChanged(int terminalIndex)
        {
            if (terminalIndex < 1 || terminalIndex > 2 || IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;
                try
                {
                    BeginInvoke(new Action<int>(OnTerminalChanged), terminalIndex);
                }
                catch (ObjectDisposedException)
                {
                    // The application is shutting down; no UI update is required.
                }
                catch (InvalidOperationException)
                {
                    // The form handle was destroyed while the switch worker completed.
                }
                return;
            }

            _headerTerminalIndex = terminalIndex;
            UpdateHeaderStatus();
        }

        private void OnHealthChanged(HealthStatus status)
        {
            if (InvokeRequired)
            {
                if (!IsHandleCreated) return;
                try
                {
                    BeginInvoke(new Action<HealthStatus>(OnHealthChanged), status);
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            try
            {
                var label = FindHardwareStatusLabel();
                if (label == null) return;

                if (status.IsHealthy)
                {
                    label.Text = "硬件状态：正常";
                    label.ForeColor = Color.FromArgb(22, 163, 74);
                }
                else
                {
                    var count = status.Devices.Count(d => !d.IsOnline);
                    label.Text = $"硬件状态：{count} 项异常";
                    label.ForeColor = Color.FromArgb(220, 38, 38);
                }
            }
            catch { }
        }

        private Label FindHardwareStatusLabel()
        {
            foreach (Control c in panelLogToolbar.Controls)
            {
                if (c is Label l && l.Text != null && l.Text.StartsWith("硬件状态"))
                    return l;
            }
            return null;
        }

        private void SetPersistentButtonStyle(Button button, bool active)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = active
                ? Color.FromArgb(13, 110, 253)
                : Color.White;
            button.ForeColor = active
                ? Color.White
                : Color.FromArgb(13, 110, 253);
            button.FlatAppearance.BorderColor = active
                ? Color.FromArgb(13, 110, 253)
                : Color.FromArgb(191, 219, 254);
            button.FlatAppearance.MouseOverBackColor = active
                ? Color.FromArgb(11, 94, 215)
                : Color.FromArgb(239, 246, 255);
        }

        private void SetPreviewButtonState(Button startButton, Button stopButton, bool isPreviewing)
        {
            SetPersistentButtonStyle(startButton, isPreviewing);
            SetPersistentButtonStyle(stopButton, false);
        }

        private void ResetPreviewButtonStates()
        {
            SetPreviewButtonState(btnStartCameraPreview, btnStopCameraPreview, false);
            SetPreviewButtonState(btnStartFingerprintPreview, btnStopFingerprintPreview, false);
            SetPreviewButtonState(btnStartIrisPreview, btnStopIrisPreview, false);
            SetPreviewButtonState(btnStartPlatePreviewCJ, btnStopPlatePreviewCJ, false);
            SetPreviewButtonState(btnStartPlatePreviewRJ2, btnStopPlatePreviewRJ2, false);
            SetPreviewButtonState(btnStartPlatePreviewRJ3, btnStopPlatePreviewRJ3, false);
        }

        private void SetBusinessButtonsEnabled(bool enabled)
        {
            btnStartProcess.Enabled = enabled;
            btnEndProcess.Enabled = enabled;
            btnSwitchTerminal1.Enabled = enabled;
            btnSwitchTerminal2.Enabled = enabled;
            btnFaceCapture.Enabled = enabled;
            btnFingerprintCapture.Enabled = enabled;
            btnOCR.Enabled = enabled;
            btnNfcCard.Enabled = enabled;
            btnIrisCapture.Enabled = enabled;
            btnAuthorize.Enabled = enabled;
            btnStartCameraPreview.Enabled = enabled;
            btnStopCameraPreview.Enabled = enabled;
            btnStartFingerprintPreview.Enabled = enabled;
            btnStopFingerprintPreview.Enabled = enabled;
            btnStartIrisPreview.Enabled = enabled;
            btnStopIrisPreview.Enabled = enabled;
            btnStartPlatePreviewCJ.Enabled = enabled;
            btnStopPlatePreviewCJ.Enabled = enabled;
            btnStartPlatePreviewRJ2.Enabled = enabled;
            btnStopPlatePreviewRJ2.Enabled = enabled;
            btnStartPlatePreviewRJ3.Enabled = enabled;
            btnStopPlatePreviewRJ3.Enabled = enabled;
        }

        private void UpdateMonitorInfo()
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                var now = DateTime.Now;
                var cpuTime = proc.TotalProcessorTime;

                var elapsed = (now - _lastCpuSample).TotalMilliseconds;
                int cpu = 0;
                if (elapsed > 0)
                {
                    var cpuUsed = (cpuTime - _lastCpuTime).TotalMilliseconds;
                    cpu = (int)(cpuUsed / (Environment.ProcessorCount * elapsed) * 100);
                }
                _lastCpuTime = cpuTime;
                _lastCpuSample = now;

                long memMb = 0;
                try { memMb = proc.PrivateMemorySize64 / 1024 / 1024; } catch { }

                var uptime = now - _processStartTime;
                var uptimeStr = uptime.TotalDays >= 1
                    ? $"{(int)uptime.TotalDays}d {(int)uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
                    : $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

                var queueText = "";
                try
                {
                    if (_server?.QueueManager != null)
                    {
                        var stats = _server.QueueManager.GetAllStats();
                        if (!string.IsNullOrEmpty(stats))
                        {
                            var lines = stats.Split('\n');
                            int queued = 0;
                            foreach (var line in lines)
                            {
                                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (parts[i].StartsWith("当前="))
                                    {
                                        var val = parts[i].Substring(3).Split('/')[0];
                                        int.TryParse(val, out var q);
                                        queued += q;
                                    }
                                }
                            }
                            if (queued > 0)
                                queueText = $" | 队列: {queued}";
                        }
                    }
                }
                catch { }

                lblMonitorValue.Text = $"CPU: {cpu}% \r\n内存: {memMb}MB\r\n运行时间: {uptimeStr}{queueText}";
            }
            catch
            {
                lblMonitorValue.Text = "CPU: -- | 内存: --\r\n运行时间: --";
            }
        }

        // --- Terminal operations ---

        private async void btnStartProcess_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.StartProcess(AppConfig.Instance.DefaultSaveDir));
            SetPersistentButtonStyle(btnStartProcess, string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase));
            AppendLog("开始流程: " + result);
        }

        private async void btnEndProcess_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.EndProcess());
            if (string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase))
                SetPersistentButtonStyle(btnStartProcess, false);
            AppendLog("结束流程: " + result);
        }

        private async void btnSwitchTerminal1_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.SwitchTerminal(1));
            AppendLog("切换到左通道: " + result);
        }

        private async void btnSwitchTerminal2_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.SwitchTerminal(2));
            AppendLog("切换到右通道: " + result);
        }

        private async void btnFaceCapture_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            await Task.Run(() => _server.CaptureFace(AppConfig.Instance.DefaultSaveDir));
        }

        private async void btnFingerprintCapture_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            await Task.Run(() => _server.CaptureFingerprint(AppConfig.Instance.DefaultSaveDir));
        }

        private async void btnOCR_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var requestId = await Task.Run(() => _server.RequestOCR(AppConfig.Instance.DefaultSaveDir));
            AppendLog("OCR 已下发, request_id: " + requestId);
        }

        private async void btnNfcCard_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var requestId = await Task.Run(() => _server.RequestNfc(AppConfig.Instance.DefaultSaveDir));
            AppendLog("IC 卡已下发, request_id: " + requestId);
        }

        private async void btnIrisCapture_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var requestId = await Task.Run(() => _server.CaptureIris(AppConfig.Instance.DefaultSaveDir));
            AppendLog("虹膜抓拍已下发, request_id: " + requestId);
        }

        // --- Preview operations ---

        private async void btnStartCameraPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            btnStartCameraPreview.Enabled = false;
            var ok = await _server.StartLocalPreviewAsync("camera", panelCamera);
            btnStartCameraPreview.Enabled = true;
            SetPreviewButtonState(btnStartCameraPreview, btnStopCameraPreview, ok);
            lblCameraPlaceholder.Visible = !ok;
            AppendLog(ok ? "摄像头预览已启动" : "摄像头预览启动失败");
        }

        private void btnStopCameraPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            _server.StopLocalPreview("camera");
            SetPreviewButtonState(btnStartCameraPreview, btnStopCameraPreview, false);
            lblCameraPlaceholder.Visible = true;
            AppendLog("摄像头预览已停止");
        }

        private async void btnStartFingerprintPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            btnStartFingerprintPreview.Enabled = false;
            var ok = await _server.StartLocalPreviewAsync("fingerprint", panelFingerprint);
            btnStartFingerprintPreview.Enabled = true;
            SetPreviewButtonState(btnStartFingerprintPreview, btnStopFingerprintPreview, ok);
            lblFingerprintPlaceholder.Visible = !ok;
            AppendLog(ok ? "指纹预览已启动" : "指纹预览启动失败");
        }

        private void btnStopFingerprintPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            _server.StopLocalPreview("fingerprint");
            SetPreviewButtonState(btnStartFingerprintPreview, btnStopFingerprintPreview, false);
            lblFingerprintPlaceholder.Visible = true;
            AppendLog("指纹预览已停止");
        }

        private async void btnStartIrisPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            btnStartIrisPreview.Enabled = false;
            var ok = await _server.StartLocalPreviewAsync("iris", panelIris);
            btnStartIrisPreview.Enabled = true;
            SetPreviewButtonState(btnStartIrisPreview, btnStopIrisPreview, ok);
            lblIrisPlaceholder.Visible = !ok;
            AppendLog(ok ? "虹膜预览已启动" : "虹膜预览启动失败");
        }

        private void btnStopIrisPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            _server.StopLocalPreview("iris");
            SetPreviewButtonState(btnStartIrisPreview, btnStopIrisPreview, false);
            lblIrisPlaceholder.Visible = true;
            AppendLog("虹膜预览已停止");
        }

        private static string GetPlatePreviewDisplayName(string plateCode)
        {
            switch (plateCode)
            {
                case "cj": return "出境车牌";
                case "rj2": return "入境车牌 2";
                case "rj3": return "入境车牌 3";
                default: return "车牌";
            }
        }

        private async Task StartLocalPlatePreviewAsync(string plateCode, Panel panel,
            Label placeholder, Button startButton, Button stopButton)
        {
            if (_server == null) return;
            var displayName = GetPlatePreviewDisplayName(plateCode);
            startButton.Enabled = false;
            var ok = await _server.StartLocalPreviewAsync("plate_" + plateCode, panel);
            startButton.Enabled = true;
            SetPreviewButtonState(startButton, stopButton, ok);
            placeholder.Visible = !ok;
            AppendLog(ok ? $"{displayName}预览已启动" : $"{displayName}预览启动失败");
        }

        private void StopLocalPlatePreview(string plateCode, Label placeholder,
            Button startButton, Button stopButton)
        {
            if (_server == null) return;
            _server.StopLocalPreview("plate_" + plateCode);
            SetPreviewButtonState(startButton, stopButton, false);
            placeholder.Visible = true;
            AppendLog($"{GetPlatePreviewDisplayName(plateCode)}预览已停止");
        }

        private async void btnStartPlatePreviewCJ_Click(object sender, EventArgs e) =>
            await StartLocalPlatePreviewAsync("cj", panelPlateCJ, lblPlateCJPlaceholder,
                btnStartPlatePreviewCJ, btnStopPlatePreviewCJ);

        private void btnStopPlatePreviewCJ_Click(object sender, EventArgs e) =>
            StopLocalPlatePreview("cj", lblPlateCJPlaceholder,
                btnStartPlatePreviewCJ, btnStopPlatePreviewCJ);

        private async void btnStartPlatePreviewRJ2_Click(object sender, EventArgs e) =>
            await StartLocalPlatePreviewAsync("rj2", panelPlateRJ2, lblPlateRJ2Placeholder,
                btnStartPlatePreviewRJ2, btnStopPlatePreviewRJ2);

        private void btnStopPlatePreviewRJ2_Click(object sender, EventArgs e) =>
            StopLocalPlatePreview("rj2", lblPlateRJ2Placeholder,
                btnStartPlatePreviewRJ2, btnStopPlatePreviewRJ2);

        private async void btnStartPlatePreviewRJ3_Click(object sender, EventArgs e) =>
            await StartLocalPlatePreviewAsync("rj3", panelPlateRJ3, lblPlateRJ3Placeholder,
                btnStartPlatePreviewRJ3, btnStopPlatePreviewRJ3);

        private void btnStopPlatePreviewRJ3_Click(object sender, EventArgs e) =>
            StopLocalPlatePreview("rj3", lblPlateRJ3Placeholder,
                btnStartPlatePreviewRJ3, btnStopPlatePreviewRJ3);

        private async void btnAuthorize_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.RequestAuthorize(
                "H111111111", "24", "HKG", "TEST", "M", "19950101"));
            if (result.Ok)
                AppendLog("授权已下发, request_id: " + result.RequestId);
            else
                AppendLog("授权下发失败: " + result.Message + ", request_id: " + result.RequestId);
        }

        // --- Logging ---

        private void AppendLog(string message)
        {
            Logger.Info(message);

            if (TryAggregateCaptureSuccess(message))
                return;

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            EnqueueUiLog(line);
        }

        private const int MaxPendingUiLogLines = 5000;
        private const int MaxUiLogFlushBatch = 300;
        private const int CaptureSummaryIntervalMs = 1000;
        private const string FaceCaptureSuccessMessage = "[人脸抓拍] 图片保存成功";
        private const string FingerprintCaptureSuccessMessage = "[指纹抓拍] 图片保存成功";

        private bool TryAggregateCaptureSuccess(string message)
        {
            if (string.Equals(message, FaceCaptureSuccessMessage, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _pendingFaceCaptureSuccessCount);
                return true;
            }

            if (string.Equals(message, FingerprintCaptureSuccessMessage, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _pendingFingerprintCaptureSuccessCount);
                return true;
            }

            return false;
        }

        private void EnqueueCaptureSuccessSummaryIfDue()
        {
            var nowUtc = DateTime.UtcNow;
            if ((nowUtc - _lastCaptureSummaryUtc).TotalMilliseconds < CaptureSummaryIntervalMs)
                return;

            _lastCaptureSummaryUtc = nowUtc;
            var faceCount = Interlocked.Exchange(ref _pendingFaceCaptureSuccessCount, 0);
            var fingerprintCount = Interlocked.Exchange(ref _pendingFingerprintCaptureSuccessCount, 0);
            if (faceCount == 0 && fingerprintCount == 0)
                return;

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [抓拍汇总] 1秒内成功：人脸={faceCount}，指纹={fingerprintCount}";
            EnqueueUiLog(line);
        }

        private void EnqueueUiLog(string line)
        {
            _pendingUiLogs.Enqueue(line);
            var pending = Interlocked.Increment(ref _pendingUiLogCount);
            if (pending <= MaxPendingUiLogLines)
                return;

            if (_pendingUiLogs.TryDequeue(out _))
                Interlocked.Decrement(ref _pendingUiLogCount);
        }

        private void FlushPendingUiLogs()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke(new Action(FlushPendingUiLogs));
                return;
            }

            EnqueueCaptureSuccessSummaryIfDue();

            var sb = new StringBuilder();
            var count = 0;
            while (count < MaxUiLogFlushBatch && _pendingUiLogs.TryDequeue(out var line))
            {
                Interlocked.Decrement(ref _pendingUiLogCount);
                sb.AppendLine(line);
                count++;
            }

            if (count > 0)
                AppendLogToMemo(sb.ToString());
        }

        private const int MaxActiveLogLines = 3000;  // Realtime window size.
        private const int TrimActiveLogLinesBatch = 300;
        private const int MaxHistoryLogLines = 5000; // Prevent unbounded history prepend memory growth.

        private void AppendLogToMemo(string text)
        {
            try
            {
                var lines = SplitLogLines(text);
                if (chkErrorOnly.Checked)
                {
                    var filtered = new List<string>();
                    foreach (var line in lines)
                    {
                        if (line.Contains("[错误]") || line.Contains("[警告]"))
                            filtered.Add(line);
                    }
                    lines = filtered;
                }

                if (lines.Count == 0)
                    return;

                var entries = CreateLogLines(lines);
                memoLog.RefreshScrollState();
                bool shouldScrollToBottom = memoLog.AutoScroll;

                BeginLogUpdate();
                try
                {
                    AppendActiveLines(entries);
                    TrimExcessActiveLines();
                }
                finally
                {
                    EndLogUpdate();
                }

                if (shouldScrollToBottom)
                    memoLog.ScrollToBottomProgrammatically();
                else
                    memoLog.RefreshScrollState();
            }
            catch
            {
                // Log area must never crash the program
            }
        }

        private List<string> SplitLogLines(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
                return result;

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            result.AddRange(lines);
            return result;
        }

        private List<LogLine> CreateLogLines(IList<string> lines)
        {
            var result = new List<LogLine>(lines.Count);
            foreach (var line in lines)
                result.Add(CreateLogLine(line));
            return result;
        }

        private LogLine CreateLogLine(string line)
        {
            if (!TryParseLogTimestamp(line, out var timestamp))
                timestamp = DateTime.Now;

            return new LogLine
            {
                Timestamp = timestamp,
                Text = line,
                ForeColor = ResolveLogColor(line)
            };
        }

        private void AppendActiveLines(IList<LogLine> lines)
        {
            foreach (var line in lines)
                _activeLines.AddLast(line);

            InsertFormattedLines(memoLog.TextLength, lines);
        }

        private int InsertFormattedLines(int index, IList<LogLine> lines)
        {
            var insertedLength = 0;
            var lineIndex = 0;
            while (lineIndex < lines.Count)
            {
                var color = lines[lineIndex].ForeColor;
                var text = new StringBuilder();
                while (lineIndex < lines.Count &&
                    lines[lineIndex].ForeColor.ToArgb() == color.ToArgb())
                {
                    text.AppendLine(lines[lineIndex].Text);
                    lineIndex++;
                }

                var chunk = text.ToString();
                memoLog.Select(index + insertedLength, 0);
                ApplyLogSelectionStyle(color);
                memoLog.SelectedText = chunk;
                insertedLength = memoLog.SelectionStart - index;
            }

            ResetLogSelectionStyle();
            return insertedLength;
        }

        private void ApplyLogSelectionStyle(Color foreColor)
        {
            memoLog.SelectionFont = _logFont;
            memoLog.SelectionColor = foreColor;
            memoLog.SelectionBackColor = memoLog.BackColor;
            memoLog.SelectionCharOffset = 0;
        }

        private void ResetLogSelectionStyle()
        {
            memoLog.SelectionFont = _logFont;
            memoLog.SelectionColor = memoLog.ForeColor;
            memoLog.SelectionBackColor = memoLog.BackColor;
            memoLog.SelectionCharOffset = 0;
        }

        private Color ResolveLogColor(string line)
        {
            if (line.Contains("[错误]") || line.Contains("失败"))
                return Color.FromArgb(239, 68, 68);
            if (line.Contains("[警告]"))
                return Color.FromArgb(234, 179, 8);
            return memoLog.ForeColor;
        }

        private void TrimExcessActiveLines(bool force = false)
        {
            int excess = _activeLines.Count - MaxActiveLogLines;
            if (excess <= 0 || (!force && excess <= TrimActiveLogLinesBatch))
                return;

            if (!RemoveLineRangeFromUi(_historyLines.Count, excess))
                return;

            for (int i = 0; i < excess && _activeLines.Count > 0; i++)
                _activeLines.RemoveFirst();
        }

        private void DisableLogUndoBuffer()
        {
            try
            {
                SendMessage(memoLog.Handle, EM_SETUNDOLIMIT, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                // Undo is not used by the read-only log view; failure is non-fatal.
            }
        }

        private bool RemoveLineRangeFromUi(int startLine, int lineCount)
        {
            if (lineCount <= 0)
                return true;

            int start = memoLog.GetFirstCharIndexFromLine(startLine);
            if (start < 0)
                return false;

            int endLine = startLine + lineCount;
            int end = memoLog.GetFirstCharIndexFromLine(endLine);
            if (end < start)
                end = memoLog.TextLength;

            memoLog.Select(start, end - start);
            memoLog.SelectedText = string.Empty;
            ResetLogSelectionStyle();
            return true;
        }

        private void BeginLogUpdate()
        {
            memoLog.SuppressScrollDetection = true;
            SendMessage(memoLog.Handle, WM_SETREDRAW, false, 0);
        }

        private void EndLogUpdate()
        {
            try
            {
                SendMessage(memoLog.Handle, WM_SETREDRAW, true, 0);
                memoLog.Invalidate();
                memoLog.Update();
            }
            finally
            {
                memoLog.SuppressScrollDetection = false;
                memoLog.RefreshScrollState();
            }
        }

        private void DisposeTrayResources()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayMenu?.Dispose();
            _trayMenu = null;
            _appIcon?.Dispose();
            _appIcon = null;
        }

        private void DisposeUiLogResources()
        {
            if (_uiLogTimer != null)
            {
                _uiLogTimer.Stop();
                _uiLogTimer.Dispose();
                _uiLogTimer = null;
            }
            if (_monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer.Dispose();
                _monitorTimer = null;
            }
            if (_midnightClearTimer != null)
            {
                _midnightClearTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _midnightClearTimer.Dispose();
                _midnightClearTimer = null;
            }
        }

        // --- History loading from log file ---

        private const int HistoryLoadBatch = 500;
        private const int WM_SETREDRAW = 0x000B;
        private const int EM_SETUNDOLIMIT = 0x0452;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, bool wParam, int lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void OnLogScrolledToTop(object sender, EventArgs e)
        {
            if (memoLog.VerticalScrollPos > 1)
                return;

            int remainingHistoryCapacity = MaxHistoryLogLines - _historyLines.Count;
            if (remainingHistoryCapacity <= 0)
                return;

            if (Interlocked.Exchange(ref _historyLoading, 1) == 1)
                return;

            var beforeTimestamp = GetFirstDisplayedLogTimestamp();
            var pageSize = Math.Min(HistoryLoadBatch, remainingHistoryCapacity);
            Task.Run(() => LoadHistoryFromLog(beforeTimestamp, pageSize)).ContinueWith(t =>
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated)
                        return;

                    if (t.Status == TaskStatus.RanToCompletion && t.Result.Count > 0)
                        PrependHistoryLines(t.Result);
                }
                finally
                {
                    Interlocked.Exchange(ref _historyLoading, 0);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnLogScrolledToBottom(object sender, EventArgs e)
        {
            if (memoLog.AutoScroll)
                EnterLiveMode();
        }

        private List<string> LoadHistoryFromLog(DateTime beforeTimestamp, int pageSize)
        {
            try
            {
                var logDir = Logger.LogDirectory;
                if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir)) return new List<string>();

                if (string.IsNullOrEmpty(_historyCurrentFile))
                {
                    _historyCurrentFile = GetCurrentLogFile(logDir);
                    if (string.IsNullOrEmpty(_historyCurrentFile)) return new List<string>();
                }

                return ReadLinesFromFile(_historyCurrentFile, pageSize, beforeTimestamp);
            }
            catch { return new List<string>(); }
        }

        private DateTime GetFirstDisplayedLogTimestamp()
        {
            foreach (var line in memoLog.Lines)
            {
                if (TryParseLogTimestamp(line, out var timestamp))
                    return timestamp;
            }

            return DateTime.MaxValue;
        }

        private List<string> ReadLinesFromFile(string filePath, int maxLines, DateTime beforeTimestamp)
        {
            var result = new List<string>();
            try
            {
                string text;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    text = sr.ReadToEnd();
                }

                var allLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in allLines)
                {
                    if (!TryParseLogTimestamp(line, out var timestamp))
                        continue;

                    if (timestamp.Date != _processStartTime.Date ||
                        timestamp < _processStartTime ||
                        timestamp >= beforeTimestamp)
                        continue;

                    result.Add(line);
                    if (result.Count > maxLines)
                        result.RemoveAt(0);
                }

                return result;
            }
            catch { return result; }
        }

        private static bool TryParseLogTimestamp(string line, out DateTime timestamp)
        {
            timestamp = DateTime.MinValue;
            if (string.IsNullOrEmpty(line) || line.Length < 25 || line[0] != '[')
                return false;

            int end = line.IndexOf(']');
            if (end <= 1)
                return false;

            var value = line.Substring(1, end - 1);
            return DateTime.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out timestamp);
        }

        private void PrependHistoryLines(List<string> lines)
        {
            if (lines.Count == 0) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => PrependHistoryLines(lines)));
                return;
            }

            try
            {
                int oldFirstVisibleChar = memoLog.GetCharIndexFromPosition(new Point(1, 1));
                int insertedLength = 0;
                var entries = CreateLogLines(lines);

                BeginLogUpdate();
                try
                {
                    for (int i = entries.Count - 1; i >= 0; i--)
                        _historyLines.AddFirst(entries[i]);

                    insertedLength = InsertFormattedLines(0, entries);

                    _historyMode = true;
                    memoLog.Select(Math.Min(oldFirstVisibleChar + insertedLength, memoLog.TextLength), 0);
                    memoLog.ScrollToCaret();
                    ResetLogSelectionStyle();
                }
                finally
                {
                    EndLogUpdate();
                }
            }
            catch
            {
                try
                {
                    if (memoLog.SuppressScrollDetection)
                        EndLogUpdate();
                }
                catch { }
            }
        }

        private void EnterLiveMode()
        {
            if (!_historyMode && _historyLines.Count == 0)
            {
                TrimExcessActiveLines(true);
                memoLog.ScrollToBottomProgrammatically();
                return;
            }

            BeginLogUpdate();
            try
            {
                RemoveHistoryFromUi();
                _historyLines.Clear();
                _historyMode = false;
                TrimExcessActiveLines(true);
            }
            finally
            {
                EndLogUpdate();
            }

            memoLog.ScrollToBottomProgrammatically();
        }

        private void RemoveHistoryFromUi()
        {
            int historyCount = _historyLines.Count;
            if (historyCount <= 0)
                return;

            if (_activeLines.Count == 0)
            {
                memoLog.Clear();
                return;
            }

            int activeStart = memoLog.GetFirstCharIndexFromLine(historyCount);
            if (activeStart <= 0)
                return;

            memoLog.Select(0, activeStart);
            memoLog.SelectedText = string.Empty;
            ResetLogSelectionStyle();
        }

        private string GetCurrentLogFile(string logDir)
        {
            try
            {
                var pattern = $"*_{_processStartTime:yyyyMMdd}.log";
                var files = Directory.GetFiles(logDir, pattern);
                return files.Length > 0 ? files[0] : null;
            }
            catch { return null; }
        }

        private void ClearLog()
        {
            try
            {
                memoLog.Clear();
                _activeLines.Clear();
                _historyLines.Clear();
                _historyMode = false;
                while (_pendingUiLogs.TryDequeue(out _)) { }
                _pendingUiLogCount = 0;
            }
            catch { }
        }

        private void ExportLog()
        {
            try
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "日志文件|*.log|文本文件|*.txt";
                    dlg.FileName = $"日志导出_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(dlg.FileName, memoLog.Text, Encoding.UTF8);
                        AppendLog("日志已导出: " + dlg.FileName);
                    }
                }
            }
            catch { }
        }
    }
}
