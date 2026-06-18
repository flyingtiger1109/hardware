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
                DisposeUiLogResources();
                DisposeTrayResources();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.panelStatusBar = new RoundedPanel();
            this.lblWindowTitle = new System.Windows.Forms.Label();
            this.lblServiceIndicator = new System.Windows.Forms.Label();
            this.lblServiceState = new System.Windows.Forms.Label();
            this.lblDllEndpointCaption = new System.Windows.Forms.Label();
            this.lblDllEndpointValue = new System.Windows.Forms.Label();
            this.lblCallbackEndpointCaption = new System.Windows.Forms.Label();
            this.lblCallbackEndpointValue = new System.Windows.Forms.Label();
            this.lblTerminalCaption = new System.Windows.Forms.Label();
            this.lblTerminalValue = new System.Windows.Forms.Label();
            this.lblUiScaleCaption = new System.Windows.Forms.Label();
            this.comboUiScale = new System.Windows.Forms.ComboBox();
            this.panelCommandGroups = new System.Windows.Forms.FlowLayoutPanel();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.panelPreview = new System.Windows.Forms.Panel();
            this.panelPreviewHeader = new System.Windows.Forms.Panel();
            this.lblPreviewTitle = new System.Windows.Forms.Label();
            this.tablePreview = new System.Windows.Forms.TableLayoutPanel();
            this.panelCamera = new System.Windows.Forms.Panel();
            this.panelFingerprint = new System.Windows.Forms.Panel();
            this.panelIris = new System.Windows.Forms.Panel();
            this.lblCameraPreviewState = new System.Windows.Forms.Label();
            this.lblFingerprintPreviewState = new System.Windows.Forms.Label();
            this.lblIrisPreviewState = new System.Windows.Forms.Label();
            this.panelLog = new RoundedPanel();
            this.panelLogHeader = new System.Windows.Forms.Panel();
            this.lblLogTitle = new System.Windows.Forms.Label();
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

            var groupService = CreateGroupPanel("服务控制", 180);
            var groupProcess = CreateGroupPanel("流程控制", 164);
            var groupTerminal = CreateGroupPanel("终端切换", 142);
            var groupCapture = CreateGroupPanel("采集操作", 398);
            var groupPreview = CreateGroupPanel("预览控制", 760);
            var groupTools = CreateGroupPanel("测试工具", 126);
            var flowService = CreateButtonFlow();
            var flowProcess = CreateButtonFlow();
            var flowTerminal = CreateButtonFlow();
            var flowCapture = CreateButtonFlow();
            var flowPreview = CreateButtonFlow();
            var flowTools = CreateButtonFlow();
            var cardCamera = CreatePreviewCard("摄像头预览", this.lblCameraPreviewState, this.panelCamera);
            var cardFingerprint = CreatePreviewCard("指纹预览", this.lblFingerprintPreviewState, this.panelFingerprint);
            var cardIris = CreatePreviewCard("虹膜预览", this.lblIrisPreviewState, this.panelIris);

            this.panelTop.SuspendLayout();
            this.panelStatusBar.SuspendLayout();
            this.panelCommandGroups.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.panelPreview.SuspendLayout();
            this.panelPreviewHeader.SuspendLayout();
            this.tablePreview.SuspendLayout();
            this.panelLog.SuspendLayout();
            this.panelLogHeader.SuspendLayout();
            this.SuspendLayout();

            // === panelTop ===
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(243, 246, 250);
            this.panelTop.Controls.Add(this.panelCommandGroups);
            this.panelTop.Controls.Add(this.panelStatusBar);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(16, 14, 16, 12);
            this.panelTop.Size = new System.Drawing.Size(1100, 230);

            // === panelStatusBar ===
            this.panelStatusBar.BackColor = System.Drawing.Color.White;
            this.panelStatusBar.Controls.Add(this.comboUiScale);
            this.panelStatusBar.Controls.Add(this.lblUiScaleCaption);
            this.panelStatusBar.Controls.Add(this.lblTerminalValue);
            this.panelStatusBar.Controls.Add(this.lblTerminalCaption);
            this.panelStatusBar.Controls.Add(this.lblCallbackEndpointValue);
            this.panelStatusBar.Controls.Add(this.lblCallbackEndpointCaption);
            this.panelStatusBar.Controls.Add(this.lblDllEndpointValue);
            this.panelStatusBar.Controls.Add(this.lblDllEndpointCaption);
            this.panelStatusBar.Controls.Add(this.lblServiceState);
            this.panelStatusBar.Controls.Add(this.lblServiceIndicator);
            this.panelStatusBar.Controls.Add(this.lblWindowTitle);
            this.panelStatusBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelStatusBar.Location = new System.Drawing.Point(16, 14);
            this.panelStatusBar.Name = "panelStatusBar";
            this.panelStatusBar.Padding = new System.Windows.Forms.Padding(18, 0, 18, 0);
            this.panelStatusBar.Size = new System.Drawing.Size(1068, 52);

            this.lblWindowTitle.AutoSize = false;
            this.lblWindowTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 11.5F, System.Drawing.FontStyle.Bold);
            this.lblWindowTitle.ForeColor = System.Drawing.Color.FromArgb(17, 24, 39);
            this.lblWindowTitle.Location = new System.Drawing.Point(18, 0);
            this.lblWindowTitle.Name = "lblWindowTitle";
            this.lblWindowTitle.Size = new System.Drawing.Size(210, 52);
            this.lblWindowTitle.Text = "HZCYJKTHardWare 后端";
            this.lblWindowTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this.lblServiceIndicator.AutoSize = false;
            this.lblServiceIndicator.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblServiceIndicator.ForeColor = System.Drawing.Color.FromArgb(156, 163, 175);
            this.lblServiceIndicator.Location = new System.Drawing.Point(236, 15);
            this.lblServiceIndicator.Name = "lblServiceIndicator";
            this.lblServiceIndicator.Size = new System.Drawing.Size(18, 22);
            this.lblServiceIndicator.Text = "●";
            this.lblServiceIndicator.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            this.lblServiceState.AutoSize = false;
            this.lblServiceState.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblServiceState.ForeColor = System.Drawing.Color.FromArgb(75, 85, 99);
            this.lblServiceState.Location = new System.Drawing.Point(258, 0);
            this.lblServiceState.Name = "lblServiceState";
            this.lblServiceState.Size = new System.Drawing.Size(72, 52);
            this.lblServiceState.Text = "已停止";
            this.lblServiceState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            SetupStatusCaption(this.lblDllEndpointCaption, 350, "DLL 监听");
            SetupStatusValue(this.lblDllEndpointValue, 350, "未加载");
            SetupStatusCaption(this.lblCallbackEndpointCaption, 495, "回调监听");
            SetupStatusValue(this.lblCallbackEndpointValue, 495, "未加载");
            SetupStatusCaption(this.lblTerminalCaption, 640, "当前终端");
            SetupStatusValue(this.lblTerminalValue, 640, "未加载");
            this.lblTerminalValue.Size = new System.Drawing.Size(325, 22);

            this.lblUiScaleCaption.AutoSize = false;
            this.lblUiScaleCaption.Font = new System.Drawing.Font("Microsoft YaHei UI", 8F);
            this.lblUiScaleCaption.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            this.lblUiScaleCaption.Location = new System.Drawing.Point(982, 7);
            this.lblUiScaleCaption.Name = "lblUiScaleCaption";
            this.lblUiScaleCaption.Size = new System.Drawing.Size(42, 18);
            this.lblUiScaleCaption.Text = "缩放";
            this.lblUiScaleCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this.comboUiScale.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.5F);
            this.comboUiScale.FormattingEnabled = true;
            this.comboUiScale.Items.AddRange(new object[] { "90%", "100%", "110%", "125%" });
            this.comboUiScale.Location = new System.Drawing.Point(982, 25);
            this.comboUiScale.Name = "comboUiScale";
            this.comboUiScale.Size = new System.Drawing.Size(76, 25);
            this.comboUiScale.Text = "100%";
            this.comboUiScale.SelectedIndexChanged += new System.EventHandler(this.comboUiScale_SelectedIndexChanged);
            this.comboUiScale.Leave += new System.EventHandler(this.comboUiScale_Leave);
            this.comboUiScale.KeyDown += new System.Windows.Forms.KeyEventHandler(this.comboUiScale_KeyDown);

            // === panelCommandGroups ===
            this.panelCommandGroups.AutoScroll = false;
            this.panelCommandGroups.BackColor = System.Drawing.Color.FromArgb(243, 246, 250);
            this.panelCommandGroups.Controls.Add(groupService);
            this.panelCommandGroups.Controls.Add(groupProcess);
            this.panelCommandGroups.Controls.Add(groupTerminal);
            this.panelCommandGroups.Controls.Add(groupCapture);
            this.panelCommandGroups.Controls.Add(groupTools);
            this.panelCommandGroups.Controls.Add(groupPreview);
            this.panelCommandGroups.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCommandGroups.Location = new System.Drawing.Point(16, 66);
            this.panelCommandGroups.Name = "panelCommandGroups";
            this.panelCommandGroups.Padding = new System.Windows.Forms.Padding(0, 12, 0, 0);
            this.panelCommandGroups.Size = new System.Drawing.Size(1068, 152);
            this.panelCommandGroups.WrapContents = true;

            groupService.Controls.Add(flowService);
            groupProcess.Controls.Add(flowProcess);
            groupTerminal.Controls.Add(flowTerminal);
            groupCapture.Controls.Add(flowCapture);
            groupPreview.Controls.Add(flowPreview);
            groupTools.Controls.Add(flowTools);

            AddButton(flowService, this.btnStartServer, "启动服务", this.btnStartServer_Click, 74, ButtonTone.Primary);
            AddButton(flowService, this.btnStopServer, "停止服务", this.btnStopServer_Click, 74, ButtonTone.Neutral);

            AddButton(flowProcess, this.btnStartProcess, "开始流程", this.btnStartProcess_Click, 66, ButtonTone.Primary);
            AddButton(flowProcess, this.btnEndProcess, "结束流程", this.btnEndProcess_Click, 66, ButtonTone.Neutral);
            AddButton(flowTerminal, this.btnSwitchTerminal1, "左通道", this.btnSwitchTerminal1_Click, 54, ButtonTone.Segment);
            AddButton(flowTerminal, this.btnSwitchTerminal2, "右通道", this.btnSwitchTerminal2_Click, 54, ButtonTone.Segment);

            AddButton(flowCapture, this.btnFaceCapture, "人脸抓拍", this.btnFaceCapture_Click, 66, ButtonTone.Neutral);
            AddButton(flowCapture, this.btnFingerprintCapture, "指纹抓拍", this.btnFingerprintCapture_Click, 66, ButtonTone.Neutral);
            AddButton(flowCapture, this.btnOCR, "OCR 阅读", this.btnOCR_Click, 74, ButtonTone.Neutral);
            AddButton(flowCapture, this.btnNfcCard, "IC 卡识别", this.btnNfcCard_Click, 76, ButtonTone.Neutral);
            AddButton(flowCapture, this.btnIrisCapture, "虹膜抓拍", this.btnIrisCapture_Click, 66, ButtonTone.Neutral);

            AddButton(flowPreview, this.btnStartCameraPreview, "开始摄像头", this.btnStartCameraPreview_Click, 86, ButtonTone.Primary);
            AddButton(flowPreview, this.btnStopCameraPreview, "停止摄像头", this.btnStopCameraPreview_Click, 86, ButtonTone.Neutral);
            AddButton(flowPreview, this.btnStartFingerprintPreview, "开始指纹", this.btnStartFingerprintPreview_Click, 74, ButtonTone.Primary);
            AddButton(flowPreview, this.btnStopFingerprintPreview, "停止指纹", this.btnStopFingerprintPreview_Click, 74, ButtonTone.Neutral);
            AddButton(flowPreview, this.btnStartIrisPreview, "开始虹膜", this.btnStartIrisPreview_Click, 74, ButtonTone.Primary);
            AddButton(flowPreview, this.btnStopIrisPreview, "停止虹膜", this.btnStopIrisPreview_Click, 74, ButtonTone.Neutral);
            AddButton(flowPreview, this.btnStartPlatePreview, "开始车牌", this.btnStartPlatePreview_Click, 74, ButtonTone.Neutral);
            AddButton(flowPreview, this.btnStopPlatePreview, "停止车牌", this.btnStopPlatePreview_Click, 74, ButtonTone.Neutral);

            AddButton(flowTools, this.btnAuthorize, "授权测试", this.btnAuthorize_Click, 92, ButtonTone.Neutral);

            // === splitMain ===
            this.splitMain.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitMain.IsSplitterFixed = true;
            this.splitMain.Location = new System.Drawing.Point(0, 230);
            this.splitMain.Name = "splitMain";
            this.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitMain.Panel1.Controls.Add(this.panelPreview);
            this.splitMain.Panel1MinSize = 190;
            this.splitMain.Panel2.Controls.Add(this.panelLog);
            this.splitMain.Panel2MinSize = 270;
            this.splitMain.Size = new System.Drawing.Size(1100, 510);
            this.splitMain.SplitterDistance = 210;
            this.splitMain.SplitterWidth = 1;

            // === panelPreview ===
            this.panelPreview.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.panelPreview.Controls.Add(this.tablePreview);
            this.panelPreview.Controls.Add(this.panelPreviewHeader);
            this.panelPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreview.Location = new System.Drawing.Point(0, 0);
            this.panelPreview.Name = "panelPreview";
            this.panelPreview.Padding = new System.Windows.Forms.Padding(16, 10, 16, 10);
            this.panelPreview.Size = new System.Drawing.Size(1100, 210);

            this.panelPreviewHeader.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.panelPreviewHeader.Controls.Add(this.lblPreviewTitle);
            this.panelPreviewHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPreviewHeader.Location = new System.Drawing.Point(16, 10);
            this.panelPreviewHeader.Name = "panelPreviewHeader";
            this.panelPreviewHeader.Size = new System.Drawing.Size(1068, 30);

            this.lblPreviewTitle.AutoSize = false;
            this.lblPreviewTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPreviewTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblPreviewTitle.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            this.lblPreviewTitle.Name = "lblPreviewTitle";
            this.lblPreviewTitle.Text = "实时预览";
            this.lblPreviewTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this.tablePreview.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.tablePreview.ColumnCount = 3;
            this.tablePreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tablePreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tablePreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tablePreview.Controls.Add(cardCamera, 0, 0);
            this.tablePreview.Controls.Add(cardFingerprint, 1, 0);
            this.tablePreview.Controls.Add(cardIris, 2, 0);
            this.tablePreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tablePreview.Location = new System.Drawing.Point(16, 40);
            this.tablePreview.Name = "tablePreview";
            this.tablePreview.RowCount = 1;
            this.tablePreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tablePreview.Size = new System.Drawing.Size(1068, 160);

            cardCamera.Margin = new System.Windows.Forms.Padding(0, 0, 10, 0);
            cardFingerprint.Margin = new System.Windows.Forms.Padding(0, 0, 10, 0);
            cardIris.Margin = new System.Windows.Forms.Padding(0);

            // === panelLog ===
            this.panelLog.BackColor = System.Drawing.Color.White;
            this.panelLog.Controls.Add(this.memoLog);
            this.panelLog.Controls.Add(this.panelLogHeader);
            this.panelLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLog.Location = new System.Drawing.Point(0, 0);
            this.panelLog.Name = "panelLog";
            this.panelLog.Padding = new System.Windows.Forms.Padding(16, 8, 16, 14);
            this.panelLog.Size = new System.Drawing.Size(1100, 299);

            this.panelLogHeader.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.panelLogHeader.Controls.Add(this.lblLogTitle);
            this.panelLogHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelLogHeader.Location = new System.Drawing.Point(16, 8);
            this.panelLogHeader.Name = "panelLogHeader";
            this.panelLogHeader.Size = new System.Drawing.Size(1068, 28);

            this.lblLogTitle.AutoSize = false;
            this.lblLogTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLogTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblLogTitle.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            this.lblLogTitle.Name = "lblLogTitle";
            this.lblLogTitle.Text = "运行日志";
            this.lblLogTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this.memoLog.BackColor = System.Drawing.Color.FromArgb(17, 24, 39);
            this.memoLog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.memoLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoLog.ForeColor = System.Drawing.Color.FromArgb(229, 231, 235);
            this.memoLog.Location = new System.Drawing.Point(16, 38);
            this.memoLog.Margin = new System.Windows.Forms.Padding(0);
            this.memoLog.Multiline = true;
            this.memoLog.Name = "memoLog";
            this.memoLog.ReadOnly = true;
            this.memoLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.memoLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.memoLog.Size = new System.Drawing.Size(1068, 249);
            this.memoLog.WordWrap = true;

            // === MainForm ===
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.ClientSize = new System.Drawing.Size(1100, 740);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.panelTop);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.MinimumSize = new System.Drawing.Size(1100, 700);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "HZCYJKTHardWare - 后端服务";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);

            this.panelTop.ResumeLayout(false);
            this.panelStatusBar.ResumeLayout(false);
            this.panelCommandGroups.ResumeLayout(false);
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.panelPreview.ResumeLayout(false);
            this.panelPreviewHeader.ResumeLayout(false);
            this.tablePreview.ResumeLayout(false);
            this.panelLog.ResumeLayout(false);
            this.panelLog.PerformLayout();
            this.panelLogHeader.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private enum ButtonTone
        {
            Primary,
            Neutral,
            Segment
        }

        private System.Windows.Forms.Panel CreateGroupPanel(string title, int width)
        {
            var panel = new RoundedPanel();
            var label = new System.Windows.Forms.Label();

            panel.BackColor = System.Drawing.Color.White;
            panel.Margin = new System.Windows.Forms.Padding(0, 0, 10, 6);
            panel.Name = "group" + title;
            panel.Padding = new System.Windows.Forms.Padding(10, 26, 10, 6);
            panel.Size = new System.Drawing.Size(width, 62);

            label.AutoSize = false;
            label.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.5F, System.Drawing.FontStyle.Bold);
            label.ForeColor = System.Drawing.Color.FromArgb(75, 85, 99);
            label.Location = new System.Drawing.Point(10, 5);
            label.Size = new System.Drawing.Size(width - 20, 18);
            label.Text = title;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            panel.Controls.Add(label);
            return panel;
        }

        private System.Windows.Forms.FlowLayoutPanel CreateButtonFlow()
        {
            return new System.Windows.Forms.FlowLayoutPanel
            {
                BackColor = System.Drawing.Color.White,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Margin = new System.Windows.Forms.Padding(0),
                Padding = new System.Windows.Forms.Padding(0),
                WrapContents = true
            };
        }

        private void AddButton(
            System.Windows.Forms.FlowLayoutPanel parent,
            System.Windows.Forms.Button button,
            string text,
            System.EventHandler handler,
            int width,
            ButtonTone tone)
        {
            SetupButton(button, text, handler, width, tone);
            parent.Controls.Add(button);
        }

        private void SetupButton(
            System.Windows.Forms.Button button,
            string text,
            System.EventHandler handler,
            int width,
            ButtonTone tone)
        {
            button.Cursor = System.Windows.Forms.Cursors.Hand;
            button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            button.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.5F);
            button.Margin = new System.Windows.Forms.Padding(0, 0, 6, 4);
            button.Name = button.Name;
            button.Size = new System.Drawing.Size(width, 26);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
            button.Click += handler;

            if (tone == ButtonTone.Primary)
            {
                button.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
                button.ForeColor = System.Drawing.Color.White;
                button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            }
            else if (tone == ButtonTone.Segment)
            {
                button.BackColor = System.Drawing.Color.White;
                button.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
                button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(191, 219, 254);
            }
            else
            {
                button.BackColor = System.Drawing.Color.White;
                button.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
                button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(191, 219, 254);
            }

            button.FlatAppearance.BorderSize = 1;
        }

        private System.Windows.Forms.Panel CreatePreviewCard(
            string title,
            System.Windows.Forms.Label stateLabel,
            System.Windows.Forms.Panel hostPanel)
        {
            var card = new RoundedPanel();
            var header = new System.Windows.Forms.Panel();
            var titleLabel = new System.Windows.Forms.Label();

            card.BackColor = System.Drawing.Color.White;
            card.Dock = System.Windows.Forms.DockStyle.Fill;
            card.Padding = new System.Windows.Forms.Padding(1, 1, 1, 8);

            header.BackColor = System.Drawing.Color.White;
            header.Dock = System.Windows.Forms.DockStyle.Top;
            header.Height = 34;
            header.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);

            titleLabel.AutoSize = false;
            titleLabel.Dock = System.Windows.Forms.DockStyle.Left;
            titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            titleLabel.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            titleLabel.Size = new System.Drawing.Size(145, 34);
            titleLabel.Text = title;
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            stateLabel.AutoSize = false;
            stateLabel.Dock = System.Windows.Forms.DockStyle.Right;
            stateLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 8F);
            stateLabel.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            stateLabel.Size = new System.Drawing.Size(82, 34);
            stateLabel.Text = "待预览";
            stateLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            hostPanel.BackColor = System.Drawing.Color.Black;
            hostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            hostPanel.ForeColor = System.Drawing.Color.White;
            hostPanel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            hostPanel.Margin = new System.Windows.Forms.Padding(0);
            hostPanel.Text = title;

            header.Controls.Add(stateLabel);
            header.Controls.Add(titleLabel);
            card.Controls.Add(hostPanel);
            card.Controls.Add(header);
            return card;
        }

        private void SetupStatusCaption(System.Windows.Forms.Label label, int x, string text)
        {
            label.AutoSize = false;
            label.Font = new System.Drawing.Font("Microsoft YaHei UI", 8F);
            label.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            label.Location = new System.Drawing.Point(x, 7);
            label.Size = new System.Drawing.Size(126, 17);
            label.Text = text;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        }

        private void SetupStatusValue(System.Windows.Forms.Label label, int x, string text)
        {
            label.AutoSize = false;
            label.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            label.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            label.Location = new System.Drawing.Point(x, 24);
            label.Size = new System.Drawing.Size(140, 22);
            label.Text = text;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundRectPath(
            System.Drawing.Rectangle bounds,
            int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var diameter = System.Math.Max(1, radius * 2);
            var arc = new System.Drawing.Rectangle(bounds.Location, new System.Drawing.Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class RoundedPanel : System.Windows.Forms.Panel
        {
            public int BorderRadius { get; set; } = 8;
            public System.Drawing.Color BorderColor { get; set; } = System.Drawing.Color.FromArgb(226, 232, 240);

            public RoundedPanel()
            {
                DoubleBuffered = true;
            }

            protected override void OnResize(System.EventArgs eventargs)
            {
                base.OnResize(eventargs);
                Invalidate();
            }

            protected override void OnPaintBackground(System.Windows.Forms.PaintEventArgs e)
            {
                if (Width <= 1 || Height <= 1)
                    return;

                using (var parentBrush = new System.Drawing.SolidBrush(
                    Parent?.BackColor ?? System.Drawing.Color.FromArgb(248, 250, 252)))
                {
                    e.Graphics.FillRectangle(parentBrush, ClientRectangle);
                }

                using (var path = CreateRoundRectPath(new System.Drawing.Rectangle(0, 0, Width - 1, Height - 1), BorderRadius))
                using (var brush = new System.Drawing.SolidBrush(BackColor))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
            }

            protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Width <= 1 || Height <= 1)
                    return;

                using (var path = CreateRoundRectPath(new System.Drawing.Rectangle(0, 0, Width - 1, Height - 1), BorderRadius))
                using (var pen = new System.Drawing.Pen(BorderColor, 1))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panelStatusBar;
        private System.Windows.Forms.Label lblWindowTitle;
        private System.Windows.Forms.Label lblServiceIndicator;
        private System.Windows.Forms.Label lblServiceState;
        private System.Windows.Forms.Label lblDllEndpointCaption;
        private System.Windows.Forms.Label lblDllEndpointValue;
        private System.Windows.Forms.Label lblCallbackEndpointCaption;
        private System.Windows.Forms.Label lblCallbackEndpointValue;
        private System.Windows.Forms.Label lblTerminalCaption;
        private System.Windows.Forms.Label lblTerminalValue;
        private System.Windows.Forms.Label lblUiScaleCaption;
        private System.Windows.Forms.ComboBox comboUiScale;
        private System.Windows.Forms.FlowLayoutPanel panelCommandGroups;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.Panel panelPreview;
        private System.Windows.Forms.Panel panelPreviewHeader;
        private System.Windows.Forms.Label lblPreviewTitle;
        private System.Windows.Forms.TableLayoutPanel tablePreview;
        private System.Windows.Forms.TextBox memoLog;
        private System.Windows.Forms.Panel panelLog;
        private System.Windows.Forms.Panel panelLogHeader;
        private System.Windows.Forms.Label lblLogTitle;

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
        private System.Windows.Forms.Panel panelFingerprint;
        private System.Windows.Forms.Panel panelIris;
        private System.Windows.Forms.Label lblCameraPreviewState;
        private System.Windows.Forms.Label lblFingerprintPreviewState;
        private System.Windows.Forms.Label lblIrisPreviewState;
    }
}
