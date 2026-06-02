namespace HZCYKJTHardWare.Proxy
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (disposing)
            {
                DisposeTrayResources();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.panelPreview = new System.Windows.Forms.Panel();
            this.memoLog = new System.Windows.Forms.TextBox();

            // Buttons
            this.btnStartServer = new System.Windows.Forms.Button();
            this.btnStopServer = new System.Windows.Forms.Button();
            this.btnStartProcess = new System.Windows.Forms.Button();
            this.btnEndProcess = new System.Windows.Forms.Button();
            this.btnSwitchTerminal1 = new System.Windows.Forms.Button();
            this.btnSwitchTerminal2 = new System.Windows.Forms.Button();
            this.btnFaceCapture = new System.Windows.Forms.Button();
            this.btnFingerprintCapture = new System.Windows.Forms.Button();
            this.btnOCR = new System.Windows.Forms.Button();
            this.btnNfcCard = new System.Windows.Forms.Button();
            this.btnIrisCapture = new System.Windows.Forms.Button();
            this.btnStartCameraPreview = new System.Windows.Forms.Button();
            this.btnStopCameraPreview = new System.Windows.Forms.Button();
            this.btnStartFingerprintPreview = new System.Windows.Forms.Button();
            this.btnStopFingerprintPreview = new System.Windows.Forms.Button();
            this.btnStartIrisPreview = new System.Windows.Forms.Button();
            this.btnStopIrisPreview = new System.Windows.Forms.Button();
            this.btnStartPlatePreview = new System.Windows.Forms.Button();
            this.btnStopPlatePreview = new System.Windows.Forms.Button();
            this.btnAuthorize = new System.Windows.Forms.Button();

            // Preview panels
            this.panelCamera = new System.Windows.Forms.Panel();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.panelFingerprint = new System.Windows.Forms.Panel();
            this.splitter2 = new System.Windows.Forms.Splitter();
            this.panelIris = new System.Windows.Forms.Panel();

            this.panelTop.SuspendLayout();
            this.panelPreview.SuspendLayout();
            this.SuspendLayout();

            // === panelTop ===
            this.panelTop.Controls.Add(this.btnStartServer);
            this.panelTop.Controls.Add(this.btnStopServer);
            this.panelTop.Controls.Add(this.btnStartProcess);
            this.panelTop.Controls.Add(this.btnEndProcess);
            this.panelTop.Controls.Add(this.btnSwitchTerminal1);
            this.panelTop.Controls.Add(this.btnSwitchTerminal2);
            this.panelTop.Controls.Add(this.btnFaceCapture);
            this.panelTop.Controls.Add(this.btnFingerprintCapture);
            this.panelTop.Controls.Add(this.btnOCR);
            this.panelTop.Controls.Add(this.btnNfcCard);
            this.panelTop.Controls.Add(this.btnIrisCapture);
            this.panelTop.Controls.Add(this.btnStartCameraPreview);
            this.panelTop.Controls.Add(this.btnStopCameraPreview);
            this.panelTop.Controls.Add(this.btnStartFingerprintPreview);
            this.panelTop.Controls.Add(this.btnStopFingerprintPreview);
            this.panelTop.Controls.Add(this.btnStartIrisPreview);
            this.panelTop.Controls.Add(this.btnStopIrisPreview);
            this.panelTop.Controls.Add(this.btnStartPlatePreview);
            this.panelTop.Controls.Add(this.btnStopPlatePreview);
            this.panelTop.Controls.Add(this.btnAuthorize);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(8);
            this.panelTop.Size = new System.Drawing.Size(980, 260);

            // Row 1: Server control
            SetupButton(btnStartServer, 8, 8, 100, 28, "启动服务", btnStartServer_Click);
            SetupButton(btnStopServer, 116, 8, 100, 28, "停止服务", btnStopServer_Click);
            btnStopServer.Enabled = false;

            // Row 2: Process + Terminal
            SetupButton(btnStartProcess, 8, 44, 100, 28, "开始流程", btnStartProcess_Click);
            SetupButton(btnEndProcess, 116, 44, 100, 28, "结束流程", btnEndProcess_Click);
            SetupButton(btnSwitchTerminal1, 224, 44, 80, 28, "终端 1", btnSwitchTerminal1_Click);
            SetupButton(btnSwitchTerminal2, 310, 44, 80, 28, "终端 2", btnSwitchTerminal2_Click);

            // Row 3: Capture
            SetupButton(btnFaceCapture, 8, 80, 100, 28, "人脸抓拍", btnFaceCapture_Click);
            SetupButton(btnFingerprintCapture, 116, 80, 100, 28, "指纹抓拍", btnFingerprintCapture_Click);
            SetupButton(btnOCR, 224, 80, 100, 28, "OCR 阅读", btnOCR_Click);
            SetupButton(btnNfcCard, 332, 80, 100, 28, "IC 卡识别", btnNfcCard_Click);
            SetupButton(btnIrisCapture, 440, 80, 100, 28, "虹膜抓拍", btnIrisCapture_Click);

            // Row 4: Camera preview
            SetupButton(btnStartCameraPreview, 8, 116, 160, 28, "开始摄像头预览", btnStartCameraPreview_Click);
            SetupButton(btnStopCameraPreview, 176, 116, 160, 28, "停止摄像头预览", btnStopCameraPreview_Click);

            // Row 5: Fingerprint preview
            SetupButton(btnStartFingerprintPreview, 8, 152, 160, 28, "开始指纹预览", btnStartFingerprintPreview_Click);
            SetupButton(btnStopFingerprintPreview, 176, 152, 160, 28, "停止指纹预览", btnStopFingerprintPreview_Click);

            // Row 6: Iris preview
            SetupButton(btnStartIrisPreview, 8, 188, 160, 28, "开始虹膜预览", btnStartIrisPreview_Click);
            SetupButton(btnStopIrisPreview, 176, 188, 160, 28, "停止虹膜预览", btnStopIrisPreview_Click);

            // Row 7: Plate preview
            SetupButton(btnStartPlatePreview, 8, 224, 160, 28, "开始车牌预览", btnStartPlatePreview_Click);
            SetupButton(btnStopPlatePreview, 176, 224, 160, 28, "停止车牌预览", btnStopPlatePreview_Click);

            // Row 8: Authorize
            SetupButton(btnAuthorize, 352, 224, 160, 28, "授权测试", btnAuthorize_Click);

            // === panelPreview ===
            this.panelPreview.Controls.Add(this.panelIris);
            this.panelPreview.Controls.Add(this.splitter2);
            this.panelPreview.Controls.Add(this.panelFingerprint);
            this.panelPreview.Controls.Add(this.splitter1);
            this.panelPreview.Controls.Add(this.panelCamera);
            this.panelPreview.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPreview.Location = new System.Drawing.Point(0, 260);
            this.panelPreview.Name = "panelPreview";
            this.panelPreview.Size = new System.Drawing.Size(980, 300);

            // panelCamera
            this.panelCamera.BackColor = System.Drawing.Color.Black;
            this.panelCamera.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelCamera.Location = new System.Drawing.Point(0, 0);
            this.panelCamera.Name = "panelCamera";
            this.panelCamera.Size = new System.Drawing.Size(300, 300);
            this.panelCamera.ForeColor = System.Drawing.Color.White;
            this.panelCamera.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.panelCamera.Text = "摄像头预览";

            // splitter1
            this.splitter1.Dock = System.Windows.Forms.DockStyle.Left;
            this.splitter1.Location = new System.Drawing.Point(300, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(4, 300);

            // panelFingerprint
            this.panelFingerprint.BackColor = System.Drawing.Color.Black;
            this.panelFingerprint.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelFingerprint.Location = new System.Drawing.Point(304, 0);
            this.panelFingerprint.Name = "panelFingerprint";
            this.panelFingerprint.Size = new System.Drawing.Size(300, 300);
            this.panelFingerprint.ForeColor = System.Drawing.Color.White;
            this.panelFingerprint.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.panelFingerprint.Text = "指纹预览";

            // splitter2
            this.splitter2.Dock = System.Windows.Forms.DockStyle.Left;
            this.splitter2.Location = new System.Drawing.Point(604, 0);
            this.splitter2.Name = "splitter2";
            this.splitter2.Size = new System.Drawing.Size(4, 300);

            // panelIris
            this.panelIris.BackColor = System.Drawing.Color.Black;
            this.panelIris.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelIris.Location = new System.Drawing.Point(608, 0);
            this.panelIris.Name = "panelIris";
            this.panelIris.Size = new System.Drawing.Size(372, 300);
            this.panelIris.ForeColor = System.Drawing.Color.White;
            this.panelIris.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.panelIris.Text = "虹膜预览";

            // === memoLog ===
            this.memoLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoLog.Location = new System.Drawing.Point(0, 560);
            this.memoLog.Multiline = true;
            this.memoLog.Name = "memoLog";
            this.memoLog.ReadOnly = true;
            this.memoLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.memoLog.Font = new System.Drawing.Font("Consolas", 9F);

            // === MainForm ===
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(980, 680);
            this.Controls.Add(this.memoLog);
            this.Controls.Add(this.panelPreview);
            this.Controls.Add(this.panelTop);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "HZCYJKTHardWare - 后端服务";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);

            this.panelTop.ResumeLayout(false);
            this.panelPreview.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void SetupButton(System.Windows.Forms.Button btn, int x, int y, int w, int h, string text, System.EventHandler handler)
        {
            btn.Location = new System.Drawing.Point(x, y);
            btn.Size = new System.Drawing.Size(w, h);
            btn.Text = text;
            btn.UseVisualStyleBackColor = true;
            btn.Click += handler;
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panelPreview;
        private System.Windows.Forms.TextBox memoLog;

        private System.Windows.Forms.Button btnStartServer;
        private System.Windows.Forms.Button btnStopServer;
        private System.Windows.Forms.Button btnStartProcess;
        private System.Windows.Forms.Button btnEndProcess;
        private System.Windows.Forms.Button btnSwitchTerminal1;
        private System.Windows.Forms.Button btnSwitchTerminal2;
        private System.Windows.Forms.Button btnFaceCapture;
        private System.Windows.Forms.Button btnFingerprintCapture;
        private System.Windows.Forms.Button btnOCR;
        private System.Windows.Forms.Button btnNfcCard;
        private System.Windows.Forms.Button btnIrisCapture;
        private System.Windows.Forms.Button btnStartCameraPreview;
        private System.Windows.Forms.Button btnStopCameraPreview;
        private System.Windows.Forms.Button btnStartFingerprintPreview;
        private System.Windows.Forms.Button btnStopFingerprintPreview;
        private System.Windows.Forms.Button btnStartIrisPreview;
        private System.Windows.Forms.Button btnStopIrisPreview;
        private System.Windows.Forms.Button btnStartPlatePreview;
        private System.Windows.Forms.Button btnStopPlatePreview;
        private System.Windows.Forms.Button btnAuthorize;

        private System.Windows.Forms.Panel panelCamera;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.Panel panelFingerprint;
        private System.Windows.Forms.Splitter splitter2;
        private System.Windows.Forms.Panel panelIris;
    }
}
