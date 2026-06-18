using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Server;

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
        private bool _exitRequested;
        private float _uiScaleFactor = 1.0f;
        private bool _applyingUiScale;
        private bool _switchingTerminal;
        private readonly PreviewUiState _cameraPreviewUiState = new PreviewUiState();
        private readonly PreviewUiState _fingerprintPreviewUiState = new PreviewUiState();
        private readonly PreviewUiState _irisPreviewUiState = new PreviewUiState();

        public MainForm()
        {
            InitializeComponent();
            InitializeStatusSummary();
            InitializeTrayIcon();
            InitializeUiLogTimer();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Logger.Info("应用程序启动中...");
            // Auto-start server on launch
            BeginInvoke(new Action(() => btnStartServer_Click(null, null)));
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

        private sealed class PreviewUiState
        {
            public bool Starting;
            public bool Running;
            public bool StopRequested;
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
                buttonsPanel.Location = new Point(64, 142);
                buttonsPanel.Size = new Size(492, 52);
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
                btnExit.MinimumSize = new Size(230, 40);
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

        private void InitializeStatusSummary()
        {
            try
            {
                var cfg = AppConfig.Instance;
                lblDllEndpointValue.Text = $"{cfg.DllServerHost}:{cfg.DllServerPort}";
                lblCallbackEndpointValue.Text = $"{cfg.CallbackListenHost}:{cfg.CallbackListenPort}";
                SetCurrentTerminalStatus(1);
            }
            catch
            {
                lblDllEndpointValue.Text = "配置未加载";
                lblCallbackEndpointValue.Text = "配置未加载";
                lblTerminalValue.Text = "配置未加载";
            }

            SetServiceStatus(false);
            SetProcessStatus(false);
            SetPreviewState(lblCameraPreviewState, btnStartCameraPreview, btnStopCameraPreview, false, "待预览", Color.FromArgb(107, 114, 128));
            SetPreviewState(lblFingerprintPreviewState, btnStartFingerprintPreview, btnStopFingerprintPreview, false, "待预览", Color.FromArgb(107, 114, 128));
            SetPreviewState(lblIrisPreviewState, btnStartIrisPreview, btnStopIrisPreview, false, "待预览", Color.FromArgb(107, 114, 128));
        }

        private void SetServiceStatus(bool running)
        {
            lblServiceIndicator.ForeColor = running
                ? Color.FromArgb(22, 163, 74)
                : Color.FromArgb(156, 163, 175);
            lblServiceState.ForeColor = running
                ? Color.FromArgb(21, 128, 61)
                : Color.FromArgb(75, 85, 99);
            lblServiceState.Text = running ? "运行中" : "已停止";
            ApplyStateButtonPair(btnStartServer, btnStopServer, running);
            if (!running)
            {
                SetProcessStatus(false);
                ResetPreviewUiState(_cameraPreviewUiState);
                ResetPreviewUiState(_fingerprintPreviewUiState);
                ResetPreviewUiState(_irisPreviewUiState);
                SetPreviewState(lblCameraPreviewState, btnStartCameraPreview, btnStopCameraPreview, false, "已停止", Color.FromArgb(107, 114, 128));
                SetPreviewState(lblFingerprintPreviewState, btnStartFingerprintPreview, btnStopFingerprintPreview, false, "已停止", Color.FromArgb(107, 114, 128));
                SetPreviewState(lblIrisPreviewState, btnStartIrisPreview, btnStopIrisPreview, false, "已停止", Color.FromArgb(107, 114, 128));
            }
        }

        private void SetProcessStatus(bool active)
        {
            ApplyStateButtonPair(btnStartProcess, btnEndProcess, active);
        }

        private void SetCurrentTerminalStatus(int index)
        {
            var cfg = AppConfig.Instance;
            var suffix = index == 1 ? cfg.Terminal1HostSuffix : cfg.Terminal2HostSuffix;
            var name = index == 1 ? cfg.Terminal1Name : cfg.Terminal2Name;
            lblTerminalValue.Text = $"{name} ({cfg.TerminalScheme}://{cfg.SubnetPrefix}.{suffix}:{cfg.TerminalPort})";
            ApplyTerminalButtonState(btnSwitchTerminal1, index == 1);
            ApplyTerminalButtonState(btnSwitchTerminal2, index == 2);
        }

        private void ApplyTerminalButtonState(Button button, bool selected)
        {
            ApplyStateButtonStyle(button, selected);
        }

        private void ApplyStateButtonPair(Button startButton, Button stopButton, bool started)
        {
            ApplyStateButtonStyle(startButton, started);
            ApplyStateButtonStyle(stopButton, !started);
        }

        private void ApplyStateButtonStyle(Button button, bool active)
        {
            button.BackColor = active ? Color.FromArgb(37, 99, 235) : Color.White;
            button.ForeColor = active ? Color.White : Color.FromArgb(37, 99, 235);
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(37, 99, 235) : Color.FromArgb(191, 219, 254);
        }

        private void SetPreviewStatus(Label label, string text, Color color)
        {
            label.Text = text;
            label.ForeColor = color;
        }

        private void SetPreviewState(Label label, Button startButton, Button stopButton, bool started, string text, Color color)
        {
            SetPreviewStatus(label, text, color);
            ApplyStateButtonPair(startButton, stopButton, started);
        }

        private async Task StartPreviewFromUiAsync(
            string resourceType,
            string displayName,
            Control hostPanel,
            Label stateLabel,
            Button startButton,
            Button stopButton,
            PreviewUiState state)
        {
            if (_server == null || state.Starting || state.Running)
                return;

            state.Starting = true;
            state.StopRequested = false;
            SetPreviewState(stateLabel, startButton, stopButton, true, "启动中", Color.FromArgb(37, 99, 235));

            try
            {
                var server = _server;
                var ok = await server.StartLocalPreviewAsync(resourceType, hostPanel);
                if (state.StopRequested || _server == null)
                {
                    try
                    {
                        server.StopLocalPreview(resourceType);
                    }
                    catch (Exception stopEx)
                    {
                        AppendLog(displayName + "预览启动后停止失败: " + stopEx.Message);
                    }

                    state.Running = false;
                    SetPreviewState(stateLabel, startButton, stopButton, false, "已停止", Color.FromArgb(107, 114, 128));
                    AppendLog(displayName + "预览已停止");
                    return;
                }

                state.Running = ok;
                SetPreviewState(stateLabel, startButton, stopButton, ok, ok ? "预览中" : "启动失败",
                    ok ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38));
                AppendLog(ok ? displayName + "预览已启动" : displayName + "预览启动失败");
            }
            catch (Exception ex)
            {
                state.Running = false;
                SetPreviewState(stateLabel, startButton, stopButton, false, "启动失败", Color.FromArgb(220, 38, 38));
                AppendLog(displayName + "预览启动异常: " + ex.Message);
            }
            finally
            {
                state.Starting = false;
                state.StopRequested = false;
            }
        }

        private void StopPreviewFromUi(
            string resourceType,
            string displayName,
            Label stateLabel,
            Button startButton,
            Button stopButton,
            PreviewUiState state)
        {
            if (_server == null)
                return;

            if (state.Starting)
            {
                state.StopRequested = true;
                SetPreviewState(stateLabel, startButton, stopButton, false, "停止中", Color.FromArgb(37, 99, 235));
                AppendLog(displayName + "预览正在启动，已标记启动后停止");
                return;
            }

            try
            {
                _server.StopLocalPreview(resourceType);
                state.Running = false;
                SetPreviewState(stateLabel, startButton, stopButton, false, "已停止", Color.FromArgb(107, 114, 128));
                AppendLog(displayName + "预览已停止");
            }
            catch (Exception ex)
            {
                AppendLog(displayName + "预览停止异常: " + ex.Message);
            }
        }

        private static void ResetPreviewUiState(PreviewUiState state)
        {
            state.Starting = false;
            state.Running = false;
            state.StopRequested = false;
        }

        private void comboUiScale_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyUiScaleFromCombo();
        }

        private void comboUiScale_Leave(object sender, EventArgs e)
        {
            ApplyUiScaleFromCombo();
        }

        private void comboUiScale_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            ApplyUiScaleFromCombo();
            e.SuppressKeyPress = true;
        }

        private void ApplyUiScaleFromCombo()
        {
            if (_applyingUiScale)
                return;

            var percent = ParseUiScalePercent(comboUiScale.Text);
            if (percent <= 0)
            {
                comboUiScale.Text = FormatUiScalePercent(_uiScaleFactor);
                return;
            }

            percent = Math.Max(85, Math.Min(125, percent));
            var targetScale = percent / 100f;
            if (Math.Abs(targetScale - _uiScaleFactor) < 0.001f)
            {
                comboUiScale.Text = percent + "%";
                return;
            }

            _applyingUiScale = true;
            try
            {
                var scaleFactor = targetScale / _uiScaleFactor;
                SuspendLayout();
                Scale(new SizeF(scaleFactor, scaleFactor));
                _uiScaleFactor = targetScale;
                MinimumSize = new Size(
                    (int)Math.Round(1100 * targetScale),
                    (int)Math.Round(700 * targetScale));
                NormalizeScaledLayout(targetScale);
                comboUiScale.Text = percent + "%";
            }
            finally
            {
                ResumeLayout(true);
                _applyingUiScale = false;
            }
        }

        private void NormalizeScaledLayout(float scale)
        {
            panelTop.Height = ScaleValue(230, scale);
            panelCommandGroups.Padding = new Padding(0, ScaleValue(12, scale), 0, 0);

            NormalizeCommandGroups(scale);

            splitMain.Panel1MinSize = ScaleValue(190, scale);
            splitMain.Panel2MinSize = ScaleValue(270, scale);
            splitMain.SplitterWidth = Math.Max(1, ScaleValue(1, scale));

            var preferredDistance = ScaleValue(210, scale);
            var maxDistance = splitMain.Height - splitMain.Panel2MinSize - splitMain.SplitterWidth;
            if (maxDistance > splitMain.Panel1MinSize)
                splitMain.SplitterDistance = Math.Max(splitMain.Panel1MinSize, Math.Min(preferredDistance, maxDistance));
        }

        private void NormalizeCommandGroups(float scale)
        {
            var groupWidths = new[] { 180, 164, 142, 398, 126, 760 };
            var buttonWidths = new[]
            {
                new[] { 74, 74 },
                new[] { 66, 66 },
                new[] { 54, 54 },
                new[] { 66, 66, 74, 76, 66 },
                new[] { 92 },
                new[] { 86, 86, 74, 74, 74, 74, 74, 74 }
            };

            for (var i = 0; i < panelCommandGroups.Controls.Count && i < groupWidths.Length; i++)
            {
                var group = panelCommandGroups.Controls[i];
                group.Size = new Size(ScaleValue(groupWidths[i], scale), ScaleValue(62, scale));
                group.Margin = new Padding(0, 0, ScaleValue(10, scale), ScaleValue(6, scale));
                group.Padding = new Padding(ScaleValue(10, scale), ScaleValue(26, scale), ScaleValue(10, scale), ScaleValue(6, scale));

                if (group.Controls.Count > 0)
                {
                    var title = group.Controls[0];
                    title.Location = new Point(ScaleValue(10, scale), ScaleValue(5, scale));
                    title.Size = new Size(Math.Max(1, group.Width - ScaleValue(20, scale)), ScaleValue(18, scale));
                }

                var flow = FindButtonFlow(group);
                if (flow != null)
                    NormalizeButtonFlow(flow, buttonWidths[i], scale);
            }
        }

        private static FlowLayoutPanel FindButtonFlow(Control group)
        {
            foreach (Control child in group.Controls)
            {
                var flow = child as FlowLayoutPanel;
                if (flow != null)
                    return flow;
            }
            return null;
        }

        private static void NormalizeButtonFlow(FlowLayoutPanel flow, int[] widths, float scale)
        {
            for (var i = 0; i < flow.Controls.Count && i < widths.Length; i++)
            {
                var button = flow.Controls[i] as Button;
                if (button == null)
                    continue;

                button.Size = new Size(ScaleValue(widths[i], scale), ScaleValue(26, scale));
                button.Margin = new Padding(0, 0, ScaleValue(6, scale), ScaleValue(4, scale));
            }
        }

        private static int ScaleValue(int value, float scale)
        {
            return Math.Max(1, (int)Math.Round(value * scale));
        }

        private static int ParseUiScalePercent(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            var normalized = text.Trim().TrimEnd('%').Trim();
            int percent;
            return int.TryParse(normalized, out percent) ? percent : 0;
        }

        private static string FormatUiScalePercent(float scale)
        {
            return ((int)Math.Round(scale * 100f)) + "%";
        }

        private void btnStartServer_Click(object sender, EventArgs e)
        {
            if (_server != null) return;
            try
            {
                _server = new ProxyServer(AppendLog);
                _server.Start();
                SetServiceStatus(true);
                AppendLog("服务已启动");
            }
            catch (Exception ex)
            {
                SetServiceStatus(false);
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
                _server.Stop();
                _server = null;
                SetServiceStatus(false);
                AppendLog("服务已停止");
            }
            catch (Exception ex)
            {
                AppendLog("停止服务失败: " + ex.Message);
            }
        }

        // --- Terminal operations ---

        private async void btnStartProcess_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.StartProcess(AppConfig.Instance.DefaultSaveDir));
            SetProcessStatus(string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase));
            AppendLog("开始流程: " + result);
        }

        private async void btnEndProcess_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.EndProcess());
            if (string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase))
                SetProcessStatus(false);
            AppendLog("结束流程: " + result);
        }

        private async void btnSwitchTerminal1_Click(object sender, EventArgs e)
        {
            await SwitchTerminalFromUiAsync(1, "左通道");
        }

        private async void btnSwitchTerminal2_Click(object sender, EventArgs e)
        {
            await SwitchTerminalFromUiAsync(2, "右通道");
        }

        private async Task SwitchTerminalFromUiAsync(int index, string displayName)
        {
            if (_server == null || _switchingTerminal)
                return;

            _switchingTerminal = true;
            try
            {
                var result = await _server.SwitchTerminalAsync(index);
                if (result.StartsWith("已切换到", StringComparison.Ordinal) ||
                    result.StartsWith("已在", StringComparison.Ordinal))
                {
                    SetCurrentTerminalStatus(index);
                }
                AppendLog("切换" + displayName + ": " + result);
            }
            catch (Exception ex)
            {
                AppendLog("切换" + displayName + "异常: " + ex.Message);
            }
            finally
            {
                _switchingTerminal = false;
            }
        }

        private async void btnFaceCapture_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var (ok, path) = await Task.Run(() => _server.CaptureFace(AppConfig.Instance.DefaultSaveDir));
            AppendLog(ok ? $"人脸抓拍成功: {path}" : "人脸抓拍失败");
        }

        private async void btnFingerprintCapture_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var (ok, path) = await Task.Run(() => _server.CaptureFingerprint(AppConfig.Instance.DefaultSaveDir));
            AppendLog(ok ? $"指纹抓拍成功: {path}" : "指纹抓拍失败");
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
            await StartPreviewFromUiAsync("camera", "摄像头", panelCamera, lblCameraPreviewState,
                btnStartCameraPreview, btnStopCameraPreview, _cameraPreviewUiState);
        }

        private void btnStopCameraPreview_Click(object sender, EventArgs e)
        {
            StopPreviewFromUi("camera", "摄像头", lblCameraPreviewState,
                btnStartCameraPreview, btnStopCameraPreview, _cameraPreviewUiState);
        }

        private async void btnStartFingerprintPreview_Click(object sender, EventArgs e)
        {
            await StartPreviewFromUiAsync("fingerprint", "指纹", panelFingerprint, lblFingerprintPreviewState,
                btnStartFingerprintPreview, btnStopFingerprintPreview, _fingerprintPreviewUiState);
        }

        private void btnStopFingerprintPreview_Click(object sender, EventArgs e)
        {
            StopPreviewFromUi("fingerprint", "指纹", lblFingerprintPreviewState,
                btnStartFingerprintPreview, btnStopFingerprintPreview, _fingerprintPreviewUiState);
        }

        private async void btnStartIrisPreview_Click(object sender, EventArgs e)
        {
            await StartPreviewFromUiAsync("iris", "虹膜", panelIris, lblIrisPreviewState,
                btnStartIrisPreview, btnStopIrisPreview, _irisPreviewUiState);
        }

        private void btnStopIrisPreview_Click(object sender, EventArgs e)
        {
            StopPreviewFromUi("iris", "虹膜", lblIrisPreviewState,
                btnStartIrisPreview, btnStopIrisPreview, _irisPreviewUiState);
        }

        private void btnStartPlatePreview_Click(object sender, EventArgs e)
        {
            AppendLog("当前版本暂不支持车牌预览");
        }

        private void btnStopPlatePreview_Click(object sender, EventArgs e)
        {
            AppendLog("当前版本暂不支持车牌预览");
        }

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
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            EnqueueUiLog(line);
            Logger.Info(message);
        }

        private const int MaxPendingUiLogLines = 5000;
        private const int MaxUiLogFlushBatch = 300;

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

            var sb = new StringBuilder();
            var count = 0;
            while (count < MaxUiLogFlushBatch && _pendingUiLogs.TryDequeue(out var line))
            {
                Interlocked.Decrement(ref _pendingUiLogCount);
                sb.AppendLine(line);
                count++;
            }

            if (count > 0)
                AppendLogToMemo(sb.ToString(), count);
        }

        private const int MaxLogLines = 3000;  // Prevent UI crash from unbounded log growth
        private const int TrimLogLinesBatch = 300;
        private int _uiLogLineCount;

        private void AppendLogToMemo(string text, int lineCount)
        {
            try
            {
                memoLog.AppendText(text);
                _uiLogLineCount += lineCount;

                // Trim in batches. Reading memoLog.Lines on every log is expensive during high-frequency requests.
                if (_uiLogLineCount > MaxLogLines + TrimLogLinesBatch)
                {
                    var lines = memoLog.Lines;
                    if (lines.Length > MaxLogLines)
                    {
                        var keep = new string[MaxLogLines];
                        Array.Copy(lines, lines.Length - MaxLogLines, keep, 0, MaxLogLines);
                        memoLog.Lines = keep;
                        _uiLogLineCount = keep.Length;
                    }
                    else
                    {
                        _uiLogLineCount = lines.Length;
                    }
                }
                memoLog.SelectionStart = memoLog.TextLength;
                memoLog.ScrollToCaret();
            }
            catch
            {
                // Log area must never crash the program
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
        }
    }
}
