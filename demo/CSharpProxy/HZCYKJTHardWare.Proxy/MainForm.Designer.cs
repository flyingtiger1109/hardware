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
            this.panelHeader = new System.Windows.Forms.Panel();
            this.headerLayout = new System.Windows.Forms.TableLayoutPanel();
            this.lblPageTitle = new System.Windows.Forms.Label();
            this.lblServiceStatus = new System.Windows.Forms.Label();
            this.panelDllListenInfo = new System.Windows.Forms.Panel();
            this.lblDllListenCaption = new System.Windows.Forms.Label();
            this.lblDllListenValue = new System.Windows.Forms.Label();
            this.panelCallbackListenInfo = new System.Windows.Forms.Panel();
            this.lblCallbackListenCaption = new System.Windows.Forms.Label();
            this.lblCallbackListenValue = new System.Windows.Forms.Label();
            this.panelTerminalInfo = new System.Windows.Forms.Panel();
            this.lblTerminalCaption = new System.Windows.Forms.Label();
            this.lblTerminalValue = new System.Windows.Forms.Label();
            this.panelTop = new System.Windows.Forms.Panel();
            this.cardLayout = new System.Windows.Forms.TableLayoutPanel();
            this.cardService = new System.Windows.Forms.Panel();
            this.lblCardService = new System.Windows.Forms.Label();
            this.cardOperation = new System.Windows.Forms.Panel();
            this.lblCardOperation = new System.Windows.Forms.Label();
            this.cardPreviewControl = new System.Windows.Forms.Panel();
            this.lblCardPreviewControl = new System.Windows.Forms.Label();
            this.panelPreview = new System.Windows.Forms.Panel();
            this.previewLayout = new System.Windows.Forms.TableLayoutPanel();
            this.panelLog = new System.Windows.Forms.Panel();
            this.lblLogTitle = new System.Windows.Forms.Label();
            this.memoLog = new Infrastructure.LogTextBox();

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

            this.panelHeader.SuspendLayout();
            this.headerLayout.SuspendLayout();
            this.panelDllListenInfo.SuspendLayout();
            this.panelCallbackListenInfo.SuspendLayout();
            this.panelTerminalInfo.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.cardLayout.SuspendLayout();
            this.cardService.SuspendLayout();
            this.cardOperation.SuspendLayout();
            this.cardPreviewControl.SuspendLayout();
            this.panelPreview.SuspendLayout();
            this.previewLayout.SuspendLayout();
            this.panelLog.SuspendLayout();
            this.SuspendLayout();

            // === Header ===
            this.panelHeader.BackColor = System.Drawing.Color.White;
            this.panelHeader.Controls.Add(this.headerLayout);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Padding = new System.Windows.Forms.Padding(32, 12, 32, 12);
            this.panelHeader.Size = new System.Drawing.Size(1180, 104);

            this.headerLayout.ColumnCount = 5;
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 270F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 165F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.headerLayout.Controls.Add(this.lblPageTitle, 0, 0);
            this.headerLayout.Controls.Add(this.lblServiceStatus, 1, 0);
            this.headerLayout.Controls.Add(this.panelDllListenInfo, 2, 0);
            this.headerLayout.Controls.Add(this.panelCallbackListenInfo, 3, 0);
            this.headerLayout.Controls.Add(this.panelTerminalInfo, 4, 0);
            this.headerLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.headerLayout.Name = "headerLayout";
            this.headerLayout.RowCount = 1;
            this.headerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            this.lblPageTitle.AutoEllipsis = true;
            this.lblPageTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPageTitle.Font = new System.Drawing.Font("Microsoft YaHei", 14F, System.Drawing.FontStyle.Bold);
            this.lblPageTitle.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            this.lblPageTitle.Name = "lblPageTitle";
            this.lblPageTitle.Text = "HZCYJKTHardWare\r\n后台服务";
            this.lblPageTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            this.lblServiceStatus.AutoEllipsis = true;
            this.lblServiceStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblServiceStatus.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblServiceStatus.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblServiceStatus.Name = "lblServiceStatus";
            this.lblServiceStatus.Text = "● 已停止";
            this.lblServiceStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this.panelDllListenInfo.Controls.Add(this.lblDllListenValue);
            this.panelDllListenInfo.Controls.Add(this.lblDllListenCaption);
            this.panelDllListenInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDllListenInfo.Name = "panelDllListenInfo";
            this.panelDllListenInfo.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);

            this.lblDllListenCaption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDllListenCaption.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblDllListenCaption.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblDllListenCaption.Height = 28;
            this.lblDllListenCaption.Name = "lblDllListenCaption";
            this.lblDllListenCaption.Text = "DLL 监听";
            this.lblDllListenCaption.TextAlign = System.Drawing.ContentAlignment.BottomLeft;

            this.lblDllListenValue.AutoEllipsis = true;
            this.lblDllListenValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDllListenValue.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblDllListenValue.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            this.lblDllListenValue.Name = "lblDllListenValue";
            this.lblDllListenValue.Text = "--";
            this.lblDllListenValue.TextAlign = System.Drawing.ContentAlignment.TopLeft;

            this.panelCallbackListenInfo.Controls.Add(this.lblCallbackListenValue);
            this.panelCallbackListenInfo.Controls.Add(this.lblCallbackListenCaption);
            this.panelCallbackListenInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCallbackListenInfo.Name = "panelCallbackListenInfo";
            this.panelCallbackListenInfo.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);

            this.lblCallbackListenCaption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCallbackListenCaption.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblCallbackListenCaption.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblCallbackListenCaption.Height = 28;
            this.lblCallbackListenCaption.Name = "lblCallbackListenCaption";
            this.lblCallbackListenCaption.Text = "回调监听";
            this.lblCallbackListenCaption.TextAlign = System.Drawing.ContentAlignment.BottomLeft;

            this.lblCallbackListenValue.AutoEllipsis = true;
            this.lblCallbackListenValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCallbackListenValue.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblCallbackListenValue.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            this.lblCallbackListenValue.Name = "lblCallbackListenValue";
            this.lblCallbackListenValue.Text = "--";
            this.lblCallbackListenValue.TextAlign = System.Drawing.ContentAlignment.TopLeft;

            this.panelTerminalInfo.Controls.Add(this.lblTerminalValue);
            this.panelTerminalInfo.Controls.Add(this.lblTerminalCaption);
            this.panelTerminalInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTerminalInfo.Name = "panelTerminalInfo";
            this.panelTerminalInfo.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);

            this.lblTerminalCaption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTerminalCaption.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblTerminalCaption.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblTerminalCaption.Height = 28;
            this.lblTerminalCaption.Name = "lblTerminalCaption";
            this.lblTerminalCaption.Text = "当前终端";
            this.lblTerminalCaption.TextAlign = System.Drawing.ContentAlignment.BottomLeft;

            this.lblTerminalValue.AutoEllipsis = true;
            this.lblTerminalValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTerminalValue.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblTerminalValue.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            this.lblTerminalValue.Name = "lblTerminalValue";
            this.lblTerminalValue.Text = "--";
            this.lblTerminalValue.TextAlign = System.Drawing.ContentAlignment.TopLeft;

            // === panelTop ===
            this.panelTop.BackColor = System.Drawing.Color.White;
            this.panelTop.Controls.Add(this.cardLayout);
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
            this.panelTop.Location = new System.Drawing.Point(0, 104);
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(20, 10, 20, 10);
            this.panelTop.Size = new System.Drawing.Size(1180, 156);

            // === cardLayout ===
            this.cardLayout.ColumnCount = 3;
            this.cardLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.333F));
            this.cardLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.333F));
            this.cardLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.334F));
            this.cardLayout.Controls.Add(this.cardService, 0, 0);
            this.cardLayout.Controls.Add(this.cardOperation, 1, 0);
            this.cardLayout.Controls.Add(this.cardPreviewControl, 2, 0);
            this.cardLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardLayout.Location = new System.Drawing.Point(24, 10);
            this.cardLayout.Name = "cardLayout";
            this.cardLayout.RowCount = 1;
            this.cardLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            // === service card ===
            this.cardService.BackColor = System.Drawing.Color.White;
            this.cardService.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardService.Controls.Add(this.lblCardService);
            this.cardService.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardService.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            this.cardService.Name = "cardService";
            this.cardService.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);

            this.lblCardService.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardService.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardService.ForeColor = System.Drawing.Color.FromArgb(52, 64, 84);
            this.lblCardService.Height = 22;
            this.lblCardService.Name = "lblCardService";
            this.lblCardService.Text = "服务与通道";
            this.lblCardService.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // === operation card ===
            this.cardOperation.BackColor = System.Drawing.Color.White;
            this.cardOperation.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardOperation.Controls.Add(this.lblCardOperation);
            this.cardOperation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardOperation.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            this.cardOperation.Name = "cardOperation";
            this.cardOperation.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);

            this.lblCardOperation.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardOperation.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardOperation.ForeColor = System.Drawing.Color.FromArgb(52, 64, 84);
            this.lblCardOperation.Height = 22;
            this.lblCardOperation.Name = "lblCardOperation";
            this.lblCardOperation.Text = "业务操作";
            this.lblCardOperation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // === preview control card ===
            this.cardPreviewControl.BackColor = System.Drawing.Color.White;
            this.cardPreviewControl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardPreviewControl.Controls.Add(this.lblCardPreviewControl);
            this.cardPreviewControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardPreviewControl.Margin = new System.Windows.Forms.Padding(0);
            this.cardPreviewControl.Name = "cardPreviewControl";
            this.cardPreviewControl.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);

            this.lblCardPreviewControl.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardPreviewControl.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardPreviewControl.ForeColor = System.Drawing.Color.FromArgb(52, 64, 84);
            this.lblCardPreviewControl.Height = 22;
            this.lblCardPreviewControl.Name = "lblCardPreviewControl";
            this.lblCardPreviewControl.Text = "预览控制";
            this.lblCardPreviewControl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Row 1: Server control
            SetupButton(btnStartServer, 12, 34, 130, 30, "启动服务", btnStartServer_Click);
            SetupButton(btnStopServer, 150, 34, 130, 30, "停止服务", btnStopServer_Click);
            btnStopServer.Enabled = false;

            // Row 2: Process + Terminal
            SetupButton(btnStartProcess, 12, 68, 130, 30, "开始流程", btnStartProcess_Click);
            SetupButton(btnEndProcess, 150, 68, 130, 30, "结束流程", btnEndProcess_Click);
            SetupButton(btnSwitchTerminal1, 12, 102, 130, 30, "左通道", btnSwitchTerminal1_Click);
            SetupButton(btnSwitchTerminal2, 150, 102, 130, 30, "右通道", btnSwitchTerminal2_Click);

            // Row 3: Capture
            SetupButton(btnFaceCapture, 12, 34, 130, 30, "人脸抓拍", btnFaceCapture_Click);
            SetupButton(btnFingerprintCapture, 150, 34, 130, 30, "指纹抓拍", btnFingerprintCapture_Click);
            SetupButton(btnOCR, 12, 68, 130, 30, "OCR 阅读", btnOCR_Click);
            SetupButton(btnNfcCard, 150, 68, 130, 30, "IC 卡识别", btnNfcCard_Click);
            SetupButton(btnIrisCapture, 12, 102, 130, 30, "虹膜抓拍", btnIrisCapture_Click);

            // Row 4: Camera preview
            SetupButton(btnStartCameraPreview, 12, 30, 130, 30, "开始摄像头预览", btnStartCameraPreview_Click);
            SetupButton(btnStopCameraPreview, 150, 30, 130, 30, "停止摄像头预览", btnStopCameraPreview_Click);

            // Row 5: Fingerprint preview
            SetupButton(btnStartFingerprintPreview, 12, 64, 130, 30, "开始指纹预览", btnStartFingerprintPreview_Click);
            SetupButton(btnStopFingerprintPreview, 150, 64, 130, 30, "停止指纹预览", btnStopFingerprintPreview_Click);

            // Row 6: Iris preview
            SetupButton(btnStartIrisPreview, 12, 98, 130, 30, "开始虹膜预览", btnStartIrisPreview_Click);
            SetupButton(btnStopIrisPreview, 150, 98, 130, 30, "停止虹膜预览", btnStopIrisPreview_Click);

            // Row 7: Plate preview
            SetupButton(btnStartPlatePreview, 12, 132, 130, 30, "开始车牌预览", btnStartPlatePreview_Click);
            SetupButton(btnStopPlatePreview, 150, 132, 130, 30, "停止车牌预览", btnStopPlatePreview_Click);

            // Row 8: Authorize
            SetupButton(btnAuthorize, 150, 102, 130, 30, "授权测试", btnAuthorize_Click);

            this.cardService.Controls.Add(this.btnStartServer);
            this.cardService.Controls.Add(this.btnStopServer);
            this.cardService.Controls.Add(this.btnStartProcess);
            this.cardService.Controls.Add(this.btnEndProcess);
            this.cardService.Controls.Add(this.btnSwitchTerminal1);
            this.cardService.Controls.Add(this.btnSwitchTerminal2);

            this.cardOperation.Controls.Add(this.btnFaceCapture);
            this.cardOperation.Controls.Add(this.btnFingerprintCapture);
            this.cardOperation.Controls.Add(this.btnOCR);
            this.cardOperation.Controls.Add(this.btnNfcCard);
            this.cardOperation.Controls.Add(this.btnIrisCapture);
            this.cardOperation.Controls.Add(this.btnAuthorize);

            this.cardPreviewControl.Controls.Add(this.btnStartCameraPreview);
            this.cardPreviewControl.Controls.Add(this.btnStopCameraPreview);
            this.cardPreviewControl.Controls.Add(this.btnStartFingerprintPreview);
            this.cardPreviewControl.Controls.Add(this.btnStopFingerprintPreview);
            this.cardPreviewControl.Controls.Add(this.btnStartIrisPreview);
            this.cardPreviewControl.Controls.Add(this.btnStopIrisPreview);
            this.cardPreviewControl.Controls.Add(this.btnStartPlatePreview);
            this.cardPreviewControl.Controls.Add(this.btnStopPlatePreview);

            // === panelPreview ===
            this.panelPreview.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.panelPreview.Controls.Add(this.previewLayout);
            this.panelPreview.Controls.Add(this.splitter2);
            this.panelPreview.Controls.Add(this.splitter1);
            this.panelPreview.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPreview.Location = new System.Drawing.Point(0, 308);
            this.panelPreview.Name = "panelPreview";
            this.panelPreview.Padding = new System.Windows.Forms.Padding(20, 0, 20, 8);
            this.panelPreview.Size = new System.Drawing.Size(1180, 220);

            this.previewLayout.ColumnCount = 7;
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 260F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 12F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 260F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 12F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 260F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewLayout.Controls.Add(this.panelCamera, 1, 0);
            this.previewLayout.Controls.Add(this.panelFingerprint, 3, 0);
            this.previewLayout.Controls.Add(this.panelIris, 5, 0);
            this.previewLayout.Dock = System.Windows.Forms.DockStyle.Top;
            this.previewLayout.Height = 210;
            this.previewLayout.Name = "previewLayout";
            this.previewLayout.RowCount = 1;
            this.previewLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 210F));

            // panelCamera
            this.panelCamera.BackColor = System.Drawing.Color.Black;
            this.panelCamera.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelCamera.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCamera.Margin = new System.Windows.Forms.Padding(0);
            this.panelCamera.Name = "panelCamera";
            this.panelCamera.Size = new System.Drawing.Size(260, 210);
            this.panelCamera.ForeColor = System.Drawing.Color.FromArgb(209, 213, 219);
            this.panelCamera.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.panelCamera.Text = "摄像头预览";

            // splitter1
            this.splitter1.Dock = System.Windows.Forms.DockStyle.Left;
            this.splitter1.Location = new System.Drawing.Point(0, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(4, 210);
            this.splitter1.Visible = false;

            // panelFingerprint
            this.panelFingerprint.BackColor = System.Drawing.Color.Black;
            this.panelFingerprint.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelFingerprint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelFingerprint.Margin = new System.Windows.Forms.Padding(0);
            this.panelFingerprint.Name = "panelFingerprint";
            this.panelFingerprint.Size = new System.Drawing.Size(260, 210);
            this.panelFingerprint.ForeColor = System.Drawing.Color.FromArgb(209, 213, 219);
            this.panelFingerprint.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.panelFingerprint.Text = "指纹预览";

            // splitter2
            this.splitter2.Dock = System.Windows.Forms.DockStyle.Left;
            this.splitter2.Location = new System.Drawing.Point(0, 0);
            this.splitter2.Name = "splitter2";
            this.splitter2.Size = new System.Drawing.Size(4, 210);
            this.splitter2.Visible = false;

            // panelIris
            this.panelIris.BackColor = System.Drawing.Color.Black;
            this.panelIris.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelIris.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelIris.Location = new System.Drawing.Point(0, 0);
            this.panelIris.Margin = new System.Windows.Forms.Padding(0);
            this.panelIris.Name = "panelIris";
            this.panelIris.Size = new System.Drawing.Size(260, 210);
            this.panelIris.ForeColor = System.Drawing.Color.FromArgb(209, 213, 219);
            this.panelIris.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.panelIris.Text = "虹膜预览";

            // === log section ===
            this.panelLog.BackColor = System.Drawing.Color.White;
            this.panelLog.Controls.Add(this.memoLog);
            this.panelLog.Controls.Add(this.lblLogTitle);
            this.panelLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLog.Location = new System.Drawing.Point(0, 528);
            this.panelLog.Name = "panelLog";
            this.panelLog.Padding = new System.Windows.Forms.Padding(20, 0, 20, 20);

            this.lblLogTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblLogTitle.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblLogTitle.ForeColor = System.Drawing.Color.FromArgb(52, 64, 84);
            this.lblLogTitle.Height = 40;
            this.lblLogTitle.Name = "lblLogTitle";
            this.lblLogTitle.Text = "实时日志";
            this.lblLogTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblLogTitle.BorderStyle = System.Windows.Forms.BorderStyle.None;

            // === memoLog ===
            this.memoLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoLog.Location = new System.Drawing.Point(24, 536);
            this.memoLog.Multiline = true;
            this.memoLog.Name = "memoLog";
            this.memoLog.ReadOnly = true;
            this.memoLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.memoLog.Font = new System.Drawing.Font("Consolas", 9F);

            // === MainForm ===
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1180, 760);
            this.Controls.Add(this.panelLog);
            this.Controls.Add(this.panelPreview);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.panelHeader);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.MinimumSize = new System.Drawing.Size(960, 720);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "HZCYJKTHardWare - 后端服务";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);

            this.panelHeader.ResumeLayout(false);
            this.headerLayout.ResumeLayout(false);
            this.panelDllListenInfo.ResumeLayout(false);
            this.panelCallbackListenInfo.ResumeLayout(false);
            this.panelTerminalInfo.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.cardLayout.ResumeLayout(false);
            this.cardService.ResumeLayout(false);
            this.cardOperation.ResumeLayout(false);
            this.cardPreviewControl.ResumeLayout(false);
            this.panelPreview.ResumeLayout(false);
            this.previewLayout.ResumeLayout(false);
            this.panelLog.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void SetupButton(System.Windows.Forms.Button btn, int x, int y, int w, int h, string text, System.EventHandler handler)
        {
            btn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btn.BackColor = System.Drawing.Color.White;
            btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(209, 213, 219);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(239, 246, 255);
            btn.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(219, 229, 254);
            btn.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            btn.ForeColor = System.Drawing.Color.FromArgb(13, 110, 253);
            btn.Location = new System.Drawing.Point(x, y);
            btn.Margin = System.Windows.Forms.Padding.Empty;
            btn.Size = new System.Drawing.Size(w, h);
            btn.Text = text;
            btn.UseVisualStyleBackColor = false;
            btn.Click += handler;
        }

        #endregion

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.TableLayoutPanel headerLayout;
        private System.Windows.Forms.Label lblPageTitle;
        private System.Windows.Forms.Label lblServiceStatus;
        private System.Windows.Forms.Panel panelDllListenInfo;
        private System.Windows.Forms.Label lblDllListenCaption;
        private System.Windows.Forms.Label lblDllListenValue;
        private System.Windows.Forms.Panel panelCallbackListenInfo;
        private System.Windows.Forms.Label lblCallbackListenCaption;
        private System.Windows.Forms.Label lblCallbackListenValue;
        private System.Windows.Forms.Panel panelTerminalInfo;
        private System.Windows.Forms.Label lblTerminalCaption;
        private System.Windows.Forms.Label lblTerminalValue;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.TableLayoutPanel cardLayout;
        private System.Windows.Forms.Panel cardService;
        private System.Windows.Forms.Label lblCardService;
        private System.Windows.Forms.Panel cardOperation;
        private System.Windows.Forms.Label lblCardOperation;
        private System.Windows.Forms.Panel cardPreviewControl;
        private System.Windows.Forms.Label lblCardPreviewControl;
        private System.Windows.Forms.Panel panelPreview;
        private System.Windows.Forms.TableLayoutPanel previewLayout;
        private System.Windows.Forms.Panel panelLog;
        private System.Windows.Forms.Label lblLogTitle;
        private Infrastructure.LogTextBox memoLog;

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
