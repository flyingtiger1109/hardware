using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Server;

namespace HZCYKJTHardWare.Proxy
{
    public partial class MainForm : Form
    {
        private ProxyServer _server;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Logger.Info("应用程序启动中...");
            // Auto-start server on launch
            BeginInvoke(new Action(() => btnStartServer_Click(null, null)));
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopServer();
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
            AppendLog("切换终端(1): " + result);
        }

        private async void btnSwitchTerminal2_Click(object sender, EventArgs e)
        {
            if (_server == null) return;
            var result = await Task.Run(() => _server.SwitchTerminal(2));
            AppendLog("切换终端(2): " + result);
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
            var requestId = await Task.Run(() => _server.RequestAuthorize(
                "H111111111", "24", "HKG", "TEST", "M", "19950101"));
            AppendLog("授权已下发, request_id: " + requestId);
        }

        // --- Logging ---

        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AppendLogToMemo(line)));
            }
            else
            {
                AppendLogToMemo(line);
            }
            Logger.Info(message);
        }

        private void AppendLogToMemo(string line)
        {
            memoLog.AppendText(line + Environment.NewLine);
            memoLog.SelectionStart = memoLog.TextLength;
            memoLog.ScrollToCaret();
        }
    }
}
