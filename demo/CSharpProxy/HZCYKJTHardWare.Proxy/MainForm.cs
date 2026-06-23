using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

        private string _historyCurrentFile;
        private bool _isLoadingHistory;
        private int _historyLoadedLineCount;

        public MainForm()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeUiLogTimer();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Logger.Info("应用程序启动中...");
            memoLog.ScrolledToTop += OnLogScrolledToTop;
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

        private void btnStartServer_Click(object sender, EventArgs e)
        {
            if (_server != null) return;
            try
            {
                _server = new ProxyServer(AppendLog);
                _server.Start();
                btnStartServer.Enabled = false;
                btnStopServer.Enabled = true;
                AppendLog("服务已启动");
            }
            catch (Exception ex)
            {
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
                btnStartServer.Enabled = true;
                btnStopServer.Enabled = false;
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
            AppendLog("开始流程: " + result);
        }

        private async void btnEndProcess_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.EndProcess());
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
            AppendLog(ok ? "摄像头预览已启动" : "摄像头预览启动失败");
        }

        private void btnStopCameraPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            _server.StopLocalPreview("camera");
            AppendLog("摄像头预览已停止");
        }

        private async void btnStartFingerprintPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            btnStartFingerprintPreview.Enabled = false;
            var ok = await _server.StartLocalPreviewAsync("fingerprint", panelFingerprint);
            btnStartFingerprintPreview.Enabled = true;
            AppendLog(ok ? "指纹预览已启动" : "指纹预览启动失败");
        }

        private void btnStopFingerprintPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            _server.StopLocalPreview("fingerprint");
            AppendLog("指纹预览已停止");
        }

        private async void btnStartIrisPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            btnStartIrisPreview.Enabled = false;
            var ok = await _server.StartLocalPreviewAsync("iris", panelIris);
            btnStartIrisPreview.Enabled = true;
            AppendLog(ok ? "虹膜预览已启动" : "虹膜预览启动失败");
        }

        private void btnStopIrisPreview_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            _server.StopLocalPreview("iris");
            AppendLog("虹膜预览已停止");
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

                if (memoLog.AutoScroll)
                {
                    memoLog.SelectionStart = memoLog.TextLength;
                    memoLog.ScrollToCaret();
                }
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

        // --- History loading from log file ---

        private const int HistoryLoadBatch = 500;
        private const int WM_SETREDRAW = 0x000B;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, bool wParam, int lParam);

        private void OnLogScrolledToTop(object sender, EventArgs e)
        {
            if (_isLoadingHistory) return;
            _isLoadingHistory = true;

            Task.Run(() => LoadHistoryFromLog()).ContinueWith(t =>
            {
                _isLoadingHistory = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void LoadHistoryFromLog()
        {
            try
            {
                var logDir = Logger.LogDirectory;
                if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir)) return;

                if (string.IsNullOrEmpty(_historyCurrentFile))
                {
                    _historyCurrentFile = GetCurrentLogFile(logDir);
                    if (string.IsNullOrEmpty(_historyCurrentFile)) return;
                }

                var lines = ReadLinesFromFile(_historyCurrentFile, HistoryLoadBatch, _historyLoadedLineCount);
                if (lines.Count == 0) return;

                _historyLoadedLineCount += lines.Count;

                BeginInvoke(new Action(() => PrependHistoryLines(lines)));
            }
            catch { }
        }

        private List<string> ReadLinesFromFile(string filePath, int maxLines, int alreadyLoaded)
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

                // Take lines before the already-loaded tail
                int available = allLines.Length - alreadyLoaded;
                if (available <= 0) return result;

                int take = Math.Min(maxLines, available);
                int start = available - take;
                for (int i = start; i < available; i++)
                    result.Add(allLines[i]);

                return result;
            }
            catch { return result; }
        }

        private void PrependHistoryLines(List<string> lines)
        {
            if (lines.Count == 0) return;
            try
            {
                memoLog._suppressScrollDetection = true;
                SendMessage(memoLog.Handle, WM_SETREDRAW, false, 0);

                int oldSelStart = memoLog.SelectionStart;
                int oldTextLen = memoLog.TextLength;

                var sb = new StringBuilder();
                foreach (var line in lines)
                    sb.AppendLine(line);
                string historyText = sb.ToString();

                memoLog.Text = historyText + memoLog.Text;
                _uiLogLineCount += lines.Count;

                int offset = historyText.Length;
                memoLog.SelectionStart = oldSelStart + offset;
                memoLog.ScrollToCaret();

                SendMessage(memoLog.Handle, WM_SETREDRAW, true, 0);
                memoLog.Invalidate();
                memoLog._suppressScrollDetection = false;
            }
            catch
            {
                try
                {
                    SendMessage(memoLog.Handle, WM_SETREDRAW, true, 0);
                    memoLog._suppressScrollDetection = false;
                }
                catch { }
            }
        }

        private string GetCurrentLogFile(string logDir)
        {
            try
            {
                var pattern = $"*_{DateTime.Now:yyyyMMdd}.log";
                var files = Directory.GetFiles(logDir, pattern);
                return files.Length > 0 ? files[0] : null;
            }
            catch { return null; }
        }
    }
}
