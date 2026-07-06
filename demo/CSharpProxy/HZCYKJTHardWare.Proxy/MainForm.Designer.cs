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
            this.lblDllListenValue = new System.Windows.Forms.Label();
            this.lblDllListenCaption = new System.Windows.Forms.Label();
            this.panelCallbackListenInfo = new System.Windows.Forms.Panel();
            this.lblCallbackListenValue = new System.Windows.Forms.Label();
            this.lblCallbackListenCaption = new System.Windows.Forms.Label();
            this.panelTerminalInfo = new System.Windows.Forms.Panel();
            this.lblTerminalValue = new System.Windows.Forms.Label();
            this.lblTerminalCaption = new System.Windows.Forms.Label();
            this.panelMonitorInfo = new System.Windows.Forms.Panel();
            this.lblMonitorValue = new System.Windows.Forms.Label();
            this.lblMonitorCaption = new System.Windows.Forms.Label();
            this.panelTop = new System.Windows.Forms.Panel();
            this.cardLayout = new System.Windows.Forms.TableLayoutPanel();
            this.cardService = new System.Windows.Forms.Panel();
            this.tlpService = new System.Windows.Forms.TableLayoutPanel();
            this.lblCardService = new System.Windows.Forms.Label();
            this.cardOperation = new System.Windows.Forms.Panel();
            this.tlpOperation = new System.Windows.Forms.TableLayoutPanel();
            this.lblCardOperation = new System.Windows.Forms.Label();
            this.cardPreviewControl = new System.Windows.Forms.Panel();
            this.tlpPreviewControl = new System.Windows.Forms.TableLayoutPanel();
            this.lblCardPreviewControl = new System.Windows.Forms.Label();
            this.panelPreview = new System.Windows.Forms.Panel();
            this.previewLayout = new System.Windows.Forms.TableLayoutPanel();
            this.panelCamera = new System.Windows.Forms.Panel();
            this.lblCameraPlaceholder = new System.Windows.Forms.Label();
            this.panelFingerprint = new System.Windows.Forms.Panel();
            this.lblFingerprintPlaceholder = new System.Windows.Forms.Label();
            this.panelIris = new System.Windows.Forms.Panel();
            this.lblIrisPlaceholder = new System.Windows.Forms.Label();
            this.panelPlateCJ = new System.Windows.Forms.Panel();
            this.lblPlateCJPlaceholder = new System.Windows.Forms.Label();
            this.panelPlateRJ2 = new System.Windows.Forms.Panel();
            this.lblPlateRJ2Placeholder = new System.Windows.Forms.Label();
            this.panelPlateRJ3 = new System.Windows.Forms.Panel();
            this.lblPlateRJ3Placeholder = new System.Windows.Forms.Label();
            this.splitter2 = new System.Windows.Forms.Splitter();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.panelLog = new System.Windows.Forms.Panel();
            this.memoLog = new HZCYKJTHardWare.Proxy.Infrastructure.LogTextBox();
            this.panelLogToolbar = new System.Windows.Forms.Panel();
            this.chkAutoScroll = new System.Windows.Forms.CheckBox();
            this.chkErrorOnly = new System.Windows.Forms.CheckBox();
            this.btnClearLog = new System.Windows.Forms.Button();
            this.btnExportLog = new System.Windows.Forms.Button();
            this.lblLogTitle = new System.Windows.Forms.Label();
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
            this.btnStartPlatePreviewCJ = new System.Windows.Forms.Button();
            this.btnStopPlatePreviewCJ = new System.Windows.Forms.Button();
            this.btnStartPlatePreviewRJ2 = new System.Windows.Forms.Button();
            this.btnStopPlatePreviewRJ2 = new System.Windows.Forms.Button();
            this.btnStartPlatePreviewRJ3 = new System.Windows.Forms.Button();
            this.btnStopPlatePreviewRJ3 = new System.Windows.Forms.Button();
            this.btnAuthorize = new System.Windows.Forms.Button();
            this.panelHeader.SuspendLayout();
            this.headerLayout.SuspendLayout();
            this.panelDllListenInfo.SuspendLayout();
            this.panelCallbackListenInfo.SuspendLayout();
            this.panelTerminalInfo.SuspendLayout();
            this.panelMonitorInfo.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.cardLayout.SuspendLayout();
            this.cardService.SuspendLayout();
            this.cardOperation.SuspendLayout();
            this.cardPreviewControl.SuspendLayout();
            this.panelPreview.SuspendLayout();
            this.previewLayout.SuspendLayout();
            this.panelCamera.SuspendLayout();
            this.panelFingerprint.SuspendLayout();
            this.panelIris.SuspendLayout();
            this.panelPlateCJ.SuspendLayout();
            this.panelPlateRJ2.SuspendLayout();
            this.panelPlateRJ3.SuspendLayout();
            this.panelLog.SuspendLayout();
            this.panelLogToolbar.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.White;
            this.panelHeader.Controls.Add(this.headerLayout);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Padding = new System.Windows.Forms.Padding(64, 24, 64, 24);
            this.panelHeader.Size = new System.Drawing.Size(2360, 228);
            this.panelHeader.TabIndex = 3;
            // 
            // headerLayout
            // 
            this.headerLayout.ColumnCount = 6;
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 540F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 230F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 300F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 330F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 560F));
            this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.headerLayout.Controls.Add(this.lblPageTitle, 0, 0);
            this.headerLayout.Controls.Add(this.lblServiceStatus, 1, 0);
            this.headerLayout.Controls.Add(this.panelDllListenInfo, 2, 0);
            this.headerLayout.Controls.Add(this.panelCallbackListenInfo, 3, 0);
            this.headerLayout.Controls.Add(this.panelTerminalInfo, 4, 0);
            this.headerLayout.Controls.Add(this.panelMonitorInfo, 5, 0);
            this.headerLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.headerLayout.Location = new System.Drawing.Point(64, 24);
            this.headerLayout.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.headerLayout.Name = "headerLayout";
            this.headerLayout.RowCount = 1;
            this.headerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.headerLayout.Size = new System.Drawing.Size(2232, 180);
            this.headerLayout.TabIndex = 0;
            // 
            // lblPageTitle
            // 
            this.lblPageTitle.AutoEllipsis = true;
            this.lblPageTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPageTitle.Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold);
            this.lblPageTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
            this.lblPageTitle.Location = new System.Drawing.Point(6, 0);
            this.lblPageTitle.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblPageTitle.Name = "lblPageTitle";
            this.lblPageTitle.Size = new System.Drawing.Size(528, 180);
            this.lblPageTitle.TabIndex = 0;
            this.lblPageTitle.Text = "HZCYJKTHardWare\r\n后台服务";
            this.lblPageTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblServiceStatus
            // 
            this.lblServiceStatus.AutoEllipsis = true;
            this.lblServiceStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblServiceStatus.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblServiceStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.lblServiceStatus.Location = new System.Drawing.Point(546, 0);
            this.lblServiceStatus.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblServiceStatus.Name = "lblServiceStatus";
            this.lblServiceStatus.Size = new System.Drawing.Size(218, 180);
            this.lblServiceStatus.TabIndex = 1;
            this.lblServiceStatus.Text = "● 已停止";
            this.lblServiceStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelDllListenInfo
            // 
            this.panelDllListenInfo.Controls.Add(this.lblDllListenValue);
            this.panelDllListenInfo.Controls.Add(this.lblDllListenCaption);
            this.panelDllListenInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDllListenInfo.Location = new System.Drawing.Point(776, 6);
            this.panelDllListenInfo.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelDllListenInfo.Name = "panelDllListenInfo";
            this.panelDllListenInfo.Padding = new System.Windows.Forms.Padding(16, 0, 16, 0);
            this.panelDllListenInfo.Size = new System.Drawing.Size(288, 168);
            this.panelDllListenInfo.TabIndex = 2;
            // 
            // lblDllListenValue
            // 
            this.lblDllListenValue.AutoEllipsis = true;
            this.lblDllListenValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDllListenValue.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblDllListenValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
            this.lblDllListenValue.Location = new System.Drawing.Point(16, 56);
            this.lblDllListenValue.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblDllListenValue.Name = "lblDllListenValue";
            this.lblDllListenValue.Size = new System.Drawing.Size(256, 112);
            this.lblDllListenValue.TabIndex = 0;
            this.lblDllListenValue.Text = "--";
            // 
            // lblDllListenCaption
            // 
            this.lblDllListenCaption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDllListenCaption.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblDllListenCaption.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.lblDllListenCaption.Location = new System.Drawing.Point(16, 0);
            this.lblDllListenCaption.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblDllListenCaption.Name = "lblDllListenCaption";
            this.lblDllListenCaption.Size = new System.Drawing.Size(256, 56);
            this.lblDllListenCaption.TabIndex = 1;
            this.lblDllListenCaption.Text = "DLL 监听";
            this.lblDllListenCaption.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // panelCallbackListenInfo
            // 
            this.panelCallbackListenInfo.Controls.Add(this.lblCallbackListenValue);
            this.panelCallbackListenInfo.Controls.Add(this.lblCallbackListenCaption);
            this.panelCallbackListenInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCallbackListenInfo.Location = new System.Drawing.Point(1076, 6);
            this.panelCallbackListenInfo.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelCallbackListenInfo.Name = "panelCallbackListenInfo";
            this.panelCallbackListenInfo.Padding = new System.Windows.Forms.Padding(16, 0, 16, 0);
            this.panelCallbackListenInfo.Size = new System.Drawing.Size(318, 168);
            this.panelCallbackListenInfo.TabIndex = 3;
            // 
            // lblCallbackListenValue
            // 
            this.lblCallbackListenValue.AutoEllipsis = true;
            this.lblCallbackListenValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCallbackListenValue.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblCallbackListenValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
            this.lblCallbackListenValue.Location = new System.Drawing.Point(16, 56);
            this.lblCallbackListenValue.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCallbackListenValue.Name = "lblCallbackListenValue";
            this.lblCallbackListenValue.Size = new System.Drawing.Size(286, 112);
            this.lblCallbackListenValue.TabIndex = 0;
            this.lblCallbackListenValue.Text = "--";
            // 
            // lblCallbackListenCaption
            // 
            this.lblCallbackListenCaption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCallbackListenCaption.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblCallbackListenCaption.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.lblCallbackListenCaption.Location = new System.Drawing.Point(16, 0);
            this.lblCallbackListenCaption.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCallbackListenCaption.Name = "lblCallbackListenCaption";
            this.lblCallbackListenCaption.Size = new System.Drawing.Size(286, 56);
            this.lblCallbackListenCaption.TabIndex = 1;
            this.lblCallbackListenCaption.Text = "回调监听";
            this.lblCallbackListenCaption.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // panelTerminalInfo
            // 
            this.panelTerminalInfo.Controls.Add(this.lblTerminalValue);
            this.panelTerminalInfo.Controls.Add(this.lblTerminalCaption);
            this.panelTerminalInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTerminalInfo.Location = new System.Drawing.Point(1406, 6);
            this.panelTerminalInfo.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelTerminalInfo.Name = "panelTerminalInfo";
            this.panelTerminalInfo.Padding = new System.Windows.Forms.Padding(16, 0, 0, 0);
            this.panelTerminalInfo.Size = new System.Drawing.Size(548, 168);
            this.panelTerminalInfo.TabIndex = 4;
            // 
            // lblTerminalValue
            // 
            this.lblTerminalValue.AutoEllipsis = true;
            this.lblTerminalValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTerminalValue.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblTerminalValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
            this.lblTerminalValue.Location = new System.Drawing.Point(16, 56);
            this.lblTerminalValue.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblTerminalValue.Name = "lblTerminalValue";
            this.lblTerminalValue.Size = new System.Drawing.Size(532, 112);
            this.lblTerminalValue.TabIndex = 0;
            this.lblTerminalValue.Text = "--";
            // 
            // lblTerminalCaption
            // 
            this.lblTerminalCaption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTerminalCaption.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblTerminalCaption.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.lblTerminalCaption.Location = new System.Drawing.Point(16, 0);
            this.lblTerminalCaption.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblTerminalCaption.Name = "lblTerminalCaption";
            this.lblTerminalCaption.Size = new System.Drawing.Size(532, 56);
            this.lblTerminalCaption.TabIndex = 1;
            this.lblTerminalCaption.Text = "当前终端";
            this.lblTerminalCaption.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // panelMonitorInfo
            // 
            this.panelMonitorInfo.Controls.Add(this.lblMonitorValue);
            this.panelMonitorInfo.Controls.Add(this.lblMonitorCaption);
            this.panelMonitorInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMonitorInfo.Location = new System.Drawing.Point(1966, 6);
            this.panelMonitorInfo.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelMonitorInfo.Name = "panelMonitorInfo";
            this.panelMonitorInfo.Padding = new System.Windows.Forms.Padding(16, 0, 0, 0);
            this.panelMonitorInfo.Size = new System.Drawing.Size(260, 168);
            this.panelMonitorInfo.TabIndex = 5;
            // 
            // lblMonitorValue
            // 
            this.lblMonitorValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMonitorValue.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblMonitorValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
            this.lblMonitorValue.Location = new System.Drawing.Point(16, 56);
            this.lblMonitorValue.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblMonitorValue.Name = "lblMonitorValue";
            this.lblMonitorValue.Size = new System.Drawing.Size(244, 112);
            this.lblMonitorValue.TabIndex = 0;
            this.lblMonitorValue.Text = "CPU: -% | 内存: -MB\r\n运行时间: 0m";
            // 
            // lblMonitorCaption
            // 
            this.lblMonitorCaption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblMonitorCaption.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblMonitorCaption.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.lblMonitorCaption.Location = new System.Drawing.Point(16, 0);
            this.lblMonitorCaption.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblMonitorCaption.Name = "lblMonitorCaption";
            this.lblMonitorCaption.Size = new System.Drawing.Size(244, 56);
            this.lblMonitorCaption.TabIndex = 1;
            this.lblMonitorCaption.Text = "运行状态";
            this.lblMonitorCaption.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // panelTop
            // 
            this.panelTop.BackColor = System.Drawing.Color.White;
            this.panelTop.Controls.Add(this.cardLayout);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 228);
            this.panelTop.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(40, 20, 40, 20);
            this.panelTop.Size = new System.Drawing.Size(2360, 420);
            this.panelTop.TabIndex = 2;
            // 
            // cardLayout
            // 
            this.cardLayout.ColumnCount = 3;
            this.cardLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.333F));
            this.cardLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.333F));
            this.cardLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.334F));
            this.cardLayout.Controls.Add(this.cardService, 0, 0);
            this.cardLayout.Controls.Add(this.cardOperation, 1, 0);
            this.cardLayout.Controls.Add(this.cardPreviewControl, 2, 0);
            this.cardLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardLayout.Location = new System.Drawing.Point(40, 20);
            this.cardLayout.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.cardLayout.Name = "cardLayout";
            this.cardLayout.RowCount = 1;
            this.cardLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.cardLayout.Size = new System.Drawing.Size(2280, 380);
            this.cardLayout.TabIndex = 0;
            // 
            // cardService
            // 
            this.cardService.BackColor = System.Drawing.Color.White;
            this.cardService.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardService.Controls.Add(this.tlpService);
            this.cardService.Controls.Add(this.lblCardService);
            this.cardService.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardService.Location = new System.Drawing.Point(0, 0);
            this.cardService.Margin = new System.Windows.Forms.Padding(0, 0, 24, 0);
            this.cardService.Name = "cardService";
            this.cardService.Padding = new System.Windows.Forms.Padding(8, 8, 8, 8);
            this.cardService.Size = new System.Drawing.Size(735, 380);
            this.cardService.TabIndex = 0;
            // 
            // tlpService
            // 
            this.tlpService.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tlpService.Location = new System.Drawing.Point(0, 0);
            this.tlpService.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tlpService.Name = "tlpService";
            this.tlpService.Size = new System.Drawing.Size(400, 200);
            this.tlpService.TabIndex = 0;
            // 
            // lblCardService
            // 
            this.lblCardService.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardService.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardService.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(64)))), ((int)(((byte)(84)))));
            this.lblCardService.Location = new System.Drawing.Point(8, 8);
            this.lblCardService.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCardService.Name = "lblCardService";
            this.lblCardService.Size = new System.Drawing.Size(717, 44);
            this.lblCardService.TabIndex = 1;
            this.lblCardService.Text = "服务与通道";
            this.lblCardService.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cardOperation
            // 
            this.cardOperation.BackColor = System.Drawing.Color.White;
            this.cardOperation.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardOperation.Controls.Add(this.tlpOperation);
            this.cardOperation.Controls.Add(this.lblCardOperation);
            this.cardOperation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardOperation.Location = new System.Drawing.Point(759, 0);
            this.cardOperation.Margin = new System.Windows.Forms.Padding(0, 0, 24, 0);
            this.cardOperation.Name = "cardOperation";
            this.cardOperation.Padding = new System.Windows.Forms.Padding(8, 8, 8, 8);
            this.cardOperation.Size = new System.Drawing.Size(735, 380);
            this.cardOperation.TabIndex = 1;
            // 
            // tlpOperation
            // 
            this.tlpOperation.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tlpOperation.Location = new System.Drawing.Point(0, 0);
            this.tlpOperation.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tlpOperation.Name = "tlpOperation";
            this.tlpOperation.Size = new System.Drawing.Size(400, 200);
            this.tlpOperation.TabIndex = 0;
            // 
            // lblCardOperation
            // 
            this.lblCardOperation.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardOperation.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardOperation.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(64)))), ((int)(((byte)(84)))));
            this.lblCardOperation.Location = new System.Drawing.Point(8, 8);
            this.lblCardOperation.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCardOperation.Name = "lblCardOperation";
            this.lblCardOperation.Size = new System.Drawing.Size(717, 44);
            this.lblCardOperation.TabIndex = 1;
            this.lblCardOperation.Text = "业务操作";
            this.lblCardOperation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cardPreviewControl
            // 
            this.cardPreviewControl.BackColor = System.Drawing.Color.White;
            this.cardPreviewControl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardPreviewControl.Controls.Add(this.tlpPreviewControl);
            this.cardPreviewControl.Controls.Add(this.lblCardPreviewControl);
            this.cardPreviewControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardPreviewControl.Location = new System.Drawing.Point(1518, 0);
            this.cardPreviewControl.Margin = new System.Windows.Forms.Padding(0);
            this.cardPreviewControl.Name = "cardPreviewControl";
            this.cardPreviewControl.Padding = new System.Windows.Forms.Padding(8, 8, 8, 8);
            this.cardPreviewControl.Size = new System.Drawing.Size(762, 380);
            this.cardPreviewControl.TabIndex = 2;
            // 
            // tlpPreviewControl
            // 
            this.tlpPreviewControl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tlpPreviewControl.Location = new System.Drawing.Point(0, 0);
            this.tlpPreviewControl.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.tlpPreviewControl.Name = "tlpPreviewControl";
            this.tlpPreviewControl.Size = new System.Drawing.Size(400, 200);
            this.tlpPreviewControl.TabIndex = 0;
            // 
            // lblCardPreviewControl
            // 
            this.lblCardPreviewControl.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardPreviewControl.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardPreviewControl.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(64)))), ((int)(((byte)(84)))));
            this.lblCardPreviewControl.Location = new System.Drawing.Point(8, 8);
            this.lblCardPreviewControl.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCardPreviewControl.Name = "lblCardPreviewControl";
            this.lblCardPreviewControl.Size = new System.Drawing.Size(744, 44);
            this.lblCardPreviewControl.TabIndex = 1;
            this.lblCardPreviewControl.Text = "预览控制";
            this.lblCardPreviewControl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelPreview
            // 
            this.panelPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this.panelPreview.Controls.Add(this.previewLayout);
            this.panelPreview.Controls.Add(this.splitter2);
            this.panelPreview.Controls.Add(this.splitter1);
            this.panelPreview.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPreview.Location = new System.Drawing.Point(0, 648);
            this.panelPreview.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelPreview.Name = "panelPreview";
            this.panelPreview.Padding = new System.Windows.Forms.Padding(40, 0, 40, 16);
            this.panelPreview.Size = new System.Drawing.Size(2360, 520);
            this.panelPreview.TabIndex = 1;
            // 
            // previewLayout
            // 
            this.previewLayout.ColumnCount = 7;
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 520F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 520F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 520F));
            this.previewLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.previewLayout.Controls.Add(this.panelCamera, 1, 0);
            this.previewLayout.Controls.Add(this.panelFingerprint, 3, 0);
            this.previewLayout.Controls.Add(this.panelIris, 5, 0);
            this.previewLayout.Controls.Add(this.panelPlateCJ, 1, 1);
            this.previewLayout.Controls.Add(this.panelPlateRJ2, 3, 1);
            this.previewLayout.Controls.Add(this.panelPlateRJ3, 5, 1);
            this.previewLayout.Dock = System.Windows.Forms.DockStyle.Top;
            this.previewLayout.Location = new System.Drawing.Point(56, 0);
            this.previewLayout.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.previewLayout.Name = "previewLayout";
            this.previewLayout.RowCount = 2;
            this.previewLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 250F));
            this.previewLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 250F));
            this.previewLayout.Size = new System.Drawing.Size(2264, 500);
            this.previewLayout.TabIndex = 0;
            // 
            // panelCamera
            // 
            this.panelCamera.BackColor = System.Drawing.Color.Black;
            this.panelCamera.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelCamera.Controls.Add(this.lblCameraPlaceholder);
            this.panelCamera.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCamera.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.panelCamera.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(209)))), ((int)(((byte)(213)))), ((int)(((byte)(219)))));
            this.panelCamera.Location = new System.Drawing.Point(328, 0);
            this.panelCamera.Margin = new System.Windows.Forms.Padding(0);
            this.panelCamera.Name = "panelCamera";
            this.panelCamera.Size = new System.Drawing.Size(520, 250);
            this.panelCamera.TabIndex = 0;
            this.panelCamera.Text = "摄像头预览";
            // 
            // lblCameraPlaceholder
            // 
            this.lblCameraPlaceholder.BackColor = System.Drawing.Color.Black;
            this.lblCameraPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCameraPlaceholder.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblCameraPlaceholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(107)))), ((int)(((byte)(114)))), ((int)(((byte)(128)))));
            this.lblCameraPlaceholder.Location = new System.Drawing.Point(0, 0);
            this.lblCameraPlaceholder.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCameraPlaceholder.Name = "lblCameraPlaceholder";
            this.lblCameraPlaceholder.Size = new System.Drawing.Size(518, 248);
            this.lblCameraPlaceholder.TabIndex = 0;
            this.lblCameraPlaceholder.Text = "摄像头未开启";
            this.lblCameraPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelFingerprint
            // 
            this.panelFingerprint.BackColor = System.Drawing.Color.Black;
            this.panelFingerprint.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelFingerprint.Controls.Add(this.lblFingerprintPlaceholder);
            this.panelFingerprint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelFingerprint.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.panelFingerprint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(209)))), ((int)(((byte)(213)))), ((int)(((byte)(219)))));
            this.panelFingerprint.Location = new System.Drawing.Point(872, 0);
            this.panelFingerprint.Margin = new System.Windows.Forms.Padding(0);
            this.panelFingerprint.Name = "panelFingerprint";
            this.panelFingerprint.Size = new System.Drawing.Size(520, 250);
            this.panelFingerprint.TabIndex = 1;
            this.panelFingerprint.Text = "指纹预览";
            // 
            // lblFingerprintPlaceholder
            // 
            this.lblFingerprintPlaceholder.BackColor = System.Drawing.Color.Black;
            this.lblFingerprintPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFingerprintPlaceholder.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblFingerprintPlaceholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(107)))), ((int)(((byte)(114)))), ((int)(((byte)(128)))));
            this.lblFingerprintPlaceholder.Location = new System.Drawing.Point(0, 0);
            this.lblFingerprintPlaceholder.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblFingerprintPlaceholder.Name = "lblFingerprintPlaceholder";
            this.lblFingerprintPlaceholder.Size = new System.Drawing.Size(518, 248);
            this.lblFingerprintPlaceholder.TabIndex = 0;
            this.lblFingerprintPlaceholder.Text = "指纹未开启";
            this.lblFingerprintPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelIris
            // 
            this.panelIris.BackColor = System.Drawing.Color.Black;
            this.panelIris.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelIris.Controls.Add(this.lblIrisPlaceholder);
            this.panelIris.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelIris.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.panelIris.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(209)))), ((int)(((byte)(213)))), ((int)(((byte)(219)))));
            this.panelIris.Location = new System.Drawing.Point(1416, 0);
            this.panelIris.Margin = new System.Windows.Forms.Padding(0);
            this.panelIris.Name = "panelIris";
            this.panelIris.Size = new System.Drawing.Size(520, 250);
            this.panelIris.TabIndex = 2;
            this.panelIris.Text = "虹膜预览";
            // 
            // lblIrisPlaceholder
            // 
            this.lblIrisPlaceholder.BackColor = System.Drawing.Color.Black;
            this.lblIrisPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblIrisPlaceholder.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblIrisPlaceholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(107)))), ((int)(((byte)(114)))), ((int)(((byte)(128)))));
            this.lblIrisPlaceholder.Location = new System.Drawing.Point(0, 0);
            this.lblIrisPlaceholder.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblIrisPlaceholder.Name = "lblIrisPlaceholder";
            this.lblIrisPlaceholder.Size = new System.Drawing.Size(518, 248);
            this.lblIrisPlaceholder.TabIndex = 0;
            this.lblIrisPlaceholder.Text = "虹膜未开启";
            this.lblIrisPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // panelPlateCJ
            //
            this.panelPlateCJ.BackColor = System.Drawing.Color.Black;
            this.panelPlateCJ.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPlateCJ.Controls.Add(this.lblPlateCJPlaceholder);
            this.panelPlateCJ.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPlateCJ.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.panelPlateCJ.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(209)))), ((int)(((byte)(213)))), ((int)(((byte)(219)))));
            this.panelPlateCJ.Location = new System.Drawing.Point(328, 250);
            this.panelPlateCJ.Margin = new System.Windows.Forms.Padding(0);
            this.panelPlateCJ.Name = "panelPlateCJ";
            this.panelPlateCJ.Size = new System.Drawing.Size(520, 250);
            this.panelPlateCJ.TabIndex = 3;
            this.panelPlateCJ.Text = "出境车牌预览";
            //
            // lblPlateCJPlaceholder
            //
            this.lblPlateCJPlaceholder.BackColor = System.Drawing.Color.Black;
            this.lblPlateCJPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPlateCJPlaceholder.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblPlateCJPlaceholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(107)))), ((int)(((byte)(114)))), ((int)(((byte)(128)))));
            this.lblPlateCJPlaceholder.Location = new System.Drawing.Point(0, 0);
            this.lblPlateCJPlaceholder.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblPlateCJPlaceholder.Name = "lblPlateCJPlaceholder";
            this.lblPlateCJPlaceholder.Size = new System.Drawing.Size(518, 248);
            this.lblPlateCJPlaceholder.TabIndex = 0;
            this.lblPlateCJPlaceholder.Text = "出境车牌预览未开启";
            this.lblPlateCJPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // panelPlateRJ2
            //
            this.panelPlateRJ2.BackColor = System.Drawing.Color.Black;
            this.panelPlateRJ2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPlateRJ2.Controls.Add(this.lblPlateRJ2Placeholder);
            this.panelPlateRJ2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPlateRJ2.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.panelPlateRJ2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(209)))), ((int)(((byte)(213)))), ((int)(((byte)(219)))));
            this.panelPlateRJ2.Location = new System.Drawing.Point(872, 250);
            this.panelPlateRJ2.Margin = new System.Windows.Forms.Padding(0);
            this.panelPlateRJ2.Name = "panelPlateRJ2";
            this.panelPlateRJ2.Size = new System.Drawing.Size(520, 250);
            this.panelPlateRJ2.TabIndex = 4;
            this.panelPlateRJ2.Text = "入境车牌预览 2";
            //
            // lblPlateRJ2Placeholder
            //
            this.lblPlateRJ2Placeholder.BackColor = System.Drawing.Color.Black;
            this.lblPlateRJ2Placeholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPlateRJ2Placeholder.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblPlateRJ2Placeholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(107)))), ((int)(((byte)(114)))), ((int)(((byte)(128)))));
            this.lblPlateRJ2Placeholder.Location = new System.Drawing.Point(0, 0);
            this.lblPlateRJ2Placeholder.Name = "lblPlateRJ2Placeholder";
            this.lblPlateRJ2Placeholder.Size = new System.Drawing.Size(518, 248);
            this.lblPlateRJ2Placeholder.TabIndex = 0;
            this.lblPlateRJ2Placeholder.Text = "入境车牌预览 2 未开启";
            this.lblPlateRJ2Placeholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // panelPlateRJ3
            //
            this.panelPlateRJ3.BackColor = System.Drawing.Color.Black;
            this.panelPlateRJ3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPlateRJ3.Controls.Add(this.lblPlateRJ3Placeholder);
            this.panelPlateRJ3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPlateRJ3.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.panelPlateRJ3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(209)))), ((int)(((byte)(213)))), ((int)(((byte)(219)))));
            this.panelPlateRJ3.Location = new System.Drawing.Point(1416, 250);
            this.panelPlateRJ3.Margin = new System.Windows.Forms.Padding(0);
            this.panelPlateRJ3.Name = "panelPlateRJ3";
            this.panelPlateRJ3.Size = new System.Drawing.Size(520, 250);
            this.panelPlateRJ3.TabIndex = 5;
            this.panelPlateRJ3.Text = "入境车牌预览 3";
            //
            // lblPlateRJ3Placeholder
            //
            this.lblPlateRJ3Placeholder.BackColor = System.Drawing.Color.Black;
            this.lblPlateRJ3Placeholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPlateRJ3Placeholder.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblPlateRJ3Placeholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(107)))), ((int)(((byte)(114)))), ((int)(((byte)(128)))));
            this.lblPlateRJ3Placeholder.Location = new System.Drawing.Point(0, 0);
            this.lblPlateRJ3Placeholder.Name = "lblPlateRJ3Placeholder";
            this.lblPlateRJ3Placeholder.Size = new System.Drawing.Size(518, 248);
            this.lblPlateRJ3Placeholder.TabIndex = 0;
            this.lblPlateRJ3Placeholder.Text = "入境车牌预览 3 未开启";
            this.lblPlateRJ3Placeholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // splitter2
            // 
            this.splitter2.Location = new System.Drawing.Point(48, 0);
            this.splitter2.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.splitter2.Name = "splitter2";
            this.splitter2.Size = new System.Drawing.Size(8, 504);
            this.splitter2.TabIndex = 1;
            this.splitter2.TabStop = false;
            this.splitter2.Visible = false;
            // 
            // splitter1
            // 
            this.splitter1.Location = new System.Drawing.Point(40, 0);
            this.splitter1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(8, 504);
            this.splitter1.TabIndex = 2;
            this.splitter1.TabStop = false;
            this.splitter1.Visible = false;
            // 
            // panelLog
            // 
            this.panelLog.BackColor = System.Drawing.Color.White;
            this.panelLog.Controls.Add(this.memoLog);
            this.panelLog.Controls.Add(this.panelLogToolbar);
            this.panelLog.Controls.Add(this.lblLogTitle);
            this.panelLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLog.Location = new System.Drawing.Point(0, 1168);
            this.panelLog.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelLog.Name = "panelLog";
            this.panelLog.Padding = new System.Windows.Forms.Padding(40, 0, 40, 40);
            this.panelLog.Size = new System.Drawing.Size(2360, 352);
            this.panelLog.TabIndex = 0;
            // 
            // memoLog
            // 
            this.memoLog.AutoScroll = true;
            this.memoLog.DetectUrls = false;
            this.memoLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.memoLog.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.memoLog.Location = new System.Drawing.Point(40, 136);
            this.memoLog.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.memoLog.Name = "memoLog";
            this.memoLog.ReadOnly = true;
            this.memoLog.Size = new System.Drawing.Size(2280, 304);
            this.memoLog.TabIndex = 0;
            this.memoLog.Text = "";
            this.memoLog.WordWrap = false;
            // 
            // panelLogToolbar
            // 
            this.panelLogToolbar.Controls.Add(this.chkAutoScroll);
            this.panelLogToolbar.Controls.Add(this.chkErrorOnly);
            this.panelLogToolbar.Controls.Add(this.btnClearLog);
            this.panelLogToolbar.Controls.Add(this.btnExportLog);
            this.panelLogToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelLogToolbar.Location = new System.Drawing.Point(40, 80);
            this.panelLogToolbar.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.panelLogToolbar.Name = "panelLogToolbar";
            this.panelLogToolbar.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.panelLogToolbar.Size = new System.Drawing.Size(2280, 56);
            this.panelLogToolbar.TabIndex = 1;
            // 
            // chkAutoScroll
            // 
            this.chkAutoScroll.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkAutoScroll.AutoSize = true;
            this.chkAutoScroll.Checked = true;
            this.chkAutoScroll.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAutoScroll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkAutoScroll.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.chkAutoScroll.Location = new System.Drawing.Point(8, 8);
            this.chkAutoScroll.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.chkAutoScroll.Name = "chkAutoScroll";
            this.chkAutoScroll.Size = new System.Drawing.Size(137, 35);
            this.chkAutoScroll.TabIndex = 0;
            this.chkAutoScroll.Text = "自动滚动";
            this.chkAutoScroll.UseVisualStyleBackColor = true;
            // 
            // chkErrorOnly
            // 
            this.chkErrorOnly.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkErrorOnly.AutoSize = true;
            this.chkErrorOnly.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkErrorOnly.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.chkErrorOnly.Location = new System.Drawing.Point(208, 8);
            this.chkErrorOnly.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.chkErrorOnly.Name = "chkErrorOnly";
            this.chkErrorOnly.Size = new System.Drawing.Size(113, 35);
            this.chkErrorOnly.TabIndex = 1;
            this.chkErrorOnly.Text = "仅错误";
            this.chkErrorOnly.UseVisualStyleBackColor = true;
            // 
            // btnClearLog
            // 
            this.btnClearLog.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnClearLog.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClearLog.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnClearLog.Location = new System.Drawing.Point(1880, 4);
            this.btnClearLog.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnClearLog.Name = "btnClearLog";
            this.btnClearLog.Size = new System.Drawing.Size(124, 48);
            this.btnClearLog.TabIndex = 2;
            this.btnClearLog.Text = "清空";
            this.btnClearLog.UseVisualStyleBackColor = true;
            // 
            // btnExportLog
            // 
            this.btnExportLog.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnExportLog.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportLog.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnExportLog.Location = new System.Drawing.Point(1880, 4);
            this.btnExportLog.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnExportLog.Name = "btnExportLog";
            this.btnExportLog.Size = new System.Drawing.Size(124, 48);
            this.btnExportLog.TabIndex = 3;
            this.btnExportLog.Text = "导出";
            this.btnExportLog.UseVisualStyleBackColor = true;
            // 
            // lblLogTitle
            // 
            this.lblLogTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblLogTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblLogTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(64)))), ((int)(((byte)(84)))));
            this.lblLogTitle.Location = new System.Drawing.Point(40, 0);
            this.lblLogTitle.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblLogTitle.Name = "lblLogTitle";
            this.lblLogTitle.Size = new System.Drawing.Size(2280, 80);
            this.lblLogTitle.TabIndex = 2;
            this.lblLogTitle.Text = "实时日志";
            this.lblLogTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnStartServer
            // 
            this.btnStartServer.Location = new System.Drawing.Point(0, 0);
            this.btnStartServer.Name = "btnStartServer";
            this.btnStartServer.Size = new System.Drawing.Size(75, 23);
            this.btnStartServer.TabIndex = 0;
            // 
            // btnStopServer
            // 
            this.btnStopServer.Enabled = false;
            this.btnStopServer.Location = new System.Drawing.Point(0, 0);
            this.btnStopServer.Name = "btnStopServer";
            this.btnStopServer.Size = new System.Drawing.Size(75, 23);
            this.btnStopServer.TabIndex = 0;
            // 
            // btnStartProcess
            // 
            this.btnStartProcess.Location = new System.Drawing.Point(0, 0);
            this.btnStartProcess.Name = "btnStartProcess";
            this.btnStartProcess.Size = new System.Drawing.Size(75, 23);
            this.btnStartProcess.TabIndex = 0;
            // 
            // btnEndProcess
            // 
            this.btnEndProcess.Location = new System.Drawing.Point(0, 0);
            this.btnEndProcess.Name = "btnEndProcess";
            this.btnEndProcess.Size = new System.Drawing.Size(75, 23);
            this.btnEndProcess.TabIndex = 0;
            // 
            // btnSwitchTerminal1
            // 
            this.btnSwitchTerminal1.Location = new System.Drawing.Point(0, 0);
            this.btnSwitchTerminal1.Name = "btnSwitchTerminal1";
            this.btnSwitchTerminal1.Size = new System.Drawing.Size(75, 23);
            this.btnSwitchTerminal1.TabIndex = 0;
            // 
            // btnSwitchTerminal2
            // 
            this.btnSwitchTerminal2.Location = new System.Drawing.Point(0, 0);
            this.btnSwitchTerminal2.Name = "btnSwitchTerminal2";
            this.btnSwitchTerminal2.Size = new System.Drawing.Size(75, 23);
            this.btnSwitchTerminal2.TabIndex = 0;
            // 
            // btnFaceCapture
            // 
            this.btnFaceCapture.Location = new System.Drawing.Point(0, 0);
            this.btnFaceCapture.Name = "btnFaceCapture";
            this.btnFaceCapture.Size = new System.Drawing.Size(75, 23);
            this.btnFaceCapture.TabIndex = 0;
            // 
            // btnFingerprintCapture
            // 
            this.btnFingerprintCapture.Location = new System.Drawing.Point(0, 0);
            this.btnFingerprintCapture.Name = "btnFingerprintCapture";
            this.btnFingerprintCapture.Size = new System.Drawing.Size(75, 23);
            this.btnFingerprintCapture.TabIndex = 0;
            // 
            // btnOCR
            // 
            this.btnOCR.Location = new System.Drawing.Point(0, 0);
            this.btnOCR.Name = "btnOCR";
            this.btnOCR.Size = new System.Drawing.Size(75, 23);
            this.btnOCR.TabIndex = 0;
            // 
            // btnNfcCard
            // 
            this.btnNfcCard.Location = new System.Drawing.Point(0, 0);
            this.btnNfcCard.Name = "btnNfcCard";
            this.btnNfcCard.Size = new System.Drawing.Size(75, 23);
            this.btnNfcCard.TabIndex = 0;
            // 
            // btnIrisCapture
            // 
            this.btnIrisCapture.Location = new System.Drawing.Point(0, 0);
            this.btnIrisCapture.Name = "btnIrisCapture";
            this.btnIrisCapture.Size = new System.Drawing.Size(75, 23);
            this.btnIrisCapture.TabIndex = 0;
            // 
            // btnStartCameraPreview
            // 
            this.btnStartCameraPreview.Location = new System.Drawing.Point(0, 0);
            this.btnStartCameraPreview.Name = "btnStartCameraPreview";
            this.btnStartCameraPreview.Size = new System.Drawing.Size(75, 23);
            this.btnStartCameraPreview.TabIndex = 0;
            // 
            // btnStopCameraPreview
            // 
            this.btnStopCameraPreview.Location = new System.Drawing.Point(0, 0);
            this.btnStopCameraPreview.Name = "btnStopCameraPreview";
            this.btnStopCameraPreview.Size = new System.Drawing.Size(75, 23);
            this.btnStopCameraPreview.TabIndex = 0;
            // 
            // btnStartFingerprintPreview
            // 
            this.btnStartFingerprintPreview.Location = new System.Drawing.Point(0, 0);
            this.btnStartFingerprintPreview.Name = "btnStartFingerprintPreview";
            this.btnStartFingerprintPreview.Size = new System.Drawing.Size(75, 23);
            this.btnStartFingerprintPreview.TabIndex = 0;
            // 
            // btnStopFingerprintPreview
            // 
            this.btnStopFingerprintPreview.Location = new System.Drawing.Point(0, 0);
            this.btnStopFingerprintPreview.Name = "btnStopFingerprintPreview";
            this.btnStopFingerprintPreview.Size = new System.Drawing.Size(75, 23);
            this.btnStopFingerprintPreview.TabIndex = 0;
            // 
            // btnStartIrisPreview
            // 
            this.btnStartIrisPreview.Location = new System.Drawing.Point(0, 0);
            this.btnStartIrisPreview.Name = "btnStartIrisPreview";
            this.btnStartIrisPreview.Size = new System.Drawing.Size(75, 23);
            this.btnStartIrisPreview.TabIndex = 0;
            // 
            // btnStopIrisPreview
            // 
            this.btnStopIrisPreview.Location = new System.Drawing.Point(0, 0);
            this.btnStopIrisPreview.Name = "btnStopIrisPreview";
            this.btnStopIrisPreview.Size = new System.Drawing.Size(75, 23);
            this.btnStopIrisPreview.TabIndex = 0;
            //
            // btnStartPlatePreviewCJ
            //
            this.btnStartPlatePreviewCJ.Location = new System.Drawing.Point(0, 0);
            this.btnStartPlatePreviewCJ.Name = "btnStartPlatePreviewCJ";
            this.btnStartPlatePreviewCJ.Size = new System.Drawing.Size(75, 23);
            this.btnStartPlatePreviewCJ.TabIndex = 0;
            //
            // btnStopPlatePreviewCJ
            //
            this.btnStopPlatePreviewCJ.Location = new System.Drawing.Point(0, 0);
            this.btnStopPlatePreviewCJ.Name = "btnStopPlatePreviewCJ";
            this.btnStopPlatePreviewCJ.Size = new System.Drawing.Size(75, 23);
            this.btnStopPlatePreviewCJ.TabIndex = 0;
            //
            // btnStartPlatePreviewRJ2
            //
            this.btnStartPlatePreviewRJ2.Location = new System.Drawing.Point(0, 0);
            this.btnStartPlatePreviewRJ2.Name = "btnStartPlatePreviewRJ2";
            this.btnStartPlatePreviewRJ2.Size = new System.Drawing.Size(75, 23);
            this.btnStartPlatePreviewRJ2.TabIndex = 0;
            //
            // btnStopPlatePreviewRJ2
            //
            this.btnStopPlatePreviewRJ2.Location = new System.Drawing.Point(0, 0);
            this.btnStopPlatePreviewRJ2.Name = "btnStopPlatePreviewRJ2";
            this.btnStopPlatePreviewRJ2.Size = new System.Drawing.Size(75, 23);
            this.btnStopPlatePreviewRJ2.TabIndex = 0;
            //
            // btnStartPlatePreviewRJ3
            //
            this.btnStartPlatePreviewRJ3.Location = new System.Drawing.Point(0, 0);
            this.btnStartPlatePreviewRJ3.Name = "btnStartPlatePreviewRJ3";
            this.btnStartPlatePreviewRJ3.Size = new System.Drawing.Size(75, 23);
            this.btnStartPlatePreviewRJ3.TabIndex = 0;
            //
            // btnStopPlatePreviewRJ3
            //
            this.btnStopPlatePreviewRJ3.Location = new System.Drawing.Point(0, 0);
            this.btnStopPlatePreviewRJ3.Name = "btnStopPlatePreviewRJ3";
            this.btnStopPlatePreviewRJ3.Size = new System.Drawing.Size(75, 23);
            this.btnStopPlatePreviewRJ3.TabIndex = 0;
            // 
            // btnAuthorize
            // 
            this.btnAuthorize.Location = new System.Drawing.Point(0, 0);
            this.btnAuthorize.Name = "btnAuthorize";
            this.btnAuthorize.Size = new System.Drawing.Size(75, 23);
            this.btnAuthorize.TabIndex = 0;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(192F, 192F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(2360, 1520);
            this.Controls.Add(this.panelLog);
            this.Controls.Add(this.panelPreview);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.panelHeader);
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.MinimumSize = new System.Drawing.Size(1894, 1369);
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
            this.panelMonitorInfo.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.cardLayout.ResumeLayout(false);
            this.cardService.ResumeLayout(false);
            this.cardOperation.ResumeLayout(false);
            this.cardPreviewControl.ResumeLayout(false);
            this.panelPreview.ResumeLayout(false);
            this.previewLayout.ResumeLayout(false);
            this.panelCamera.ResumeLayout(false);
            this.panelFingerprint.ResumeLayout(false);
            this.panelIris.ResumeLayout(false);
            this.panelPlateCJ.ResumeLayout(false);
            this.panelPlateRJ2.ResumeLayout(false);
            this.panelPlateRJ3.ResumeLayout(false);
            this.panelLog.ResumeLayout(false);
            this.panelLogToolbar.ResumeLayout(false);
            this.panelLogToolbar.PerformLayout();
            this.ResumeLayout(false);

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
        private System.Windows.Forms.Panel panelMonitorInfo;
        private System.Windows.Forms.Label lblMonitorCaption;
        private System.Windows.Forms.Label lblMonitorValue;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.TableLayoutPanel cardLayout;
        private System.Windows.Forms.Panel cardService;
        private System.Windows.Forms.Label lblCardService;
        private System.Windows.Forms.TableLayoutPanel tlpService;
        private System.Windows.Forms.Panel cardOperation;
        private System.Windows.Forms.Label lblCardOperation;
        private System.Windows.Forms.TableLayoutPanel tlpOperation;
        private System.Windows.Forms.Panel cardPreviewControl;
        private System.Windows.Forms.Label lblCardPreviewControl;
        private System.Windows.Forms.TableLayoutPanel tlpPreviewControl;
        private System.Windows.Forms.Panel panelPreview;
        private System.Windows.Forms.TableLayoutPanel previewLayout;
        private System.Windows.Forms.Panel panelLog;
        private System.Windows.Forms.Label lblLogTitle;
        private Infrastructure.LogTextBox memoLog;

        private System.Windows.Forms.Panel panelLogToolbar;
        private System.Windows.Forms.CheckBox chkAutoScroll;
        private System.Windows.Forms.CheckBox chkErrorOnly;
        private System.Windows.Forms.Button btnClearLog;
        private System.Windows.Forms.Button btnExportLog;

        private System.Windows.Forms.Label lblCameraPlaceholder;
        private System.Windows.Forms.Label lblFingerprintPlaceholder;
        private System.Windows.Forms.Label lblIrisPlaceholder;
        private System.Windows.Forms.Label lblPlateCJPlaceholder;
        private System.Windows.Forms.Label lblPlateRJ2Placeholder;
        private System.Windows.Forms.Label lblPlateRJ3Placeholder;

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
        private System.Windows.Forms.Button btnStartPlatePreviewCJ;
        private System.Windows.Forms.Button btnStopPlatePreviewCJ;
        private System.Windows.Forms.Button btnStartPlatePreviewRJ2;
        private System.Windows.Forms.Button btnStopPlatePreviewRJ2;
        private System.Windows.Forms.Button btnStartPlatePreviewRJ3;
        private System.Windows.Forms.Button btnStopPlatePreviewRJ3;
        private System.Windows.Forms.Button btnAuthorize;

        private System.Windows.Forms.Panel panelCamera;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.Panel panelFingerprint;
        private System.Windows.Forms.Splitter splitter2;
        private System.Windows.Forms.Panel panelIris;
        private System.Windows.Forms.Panel panelPlateCJ;
        private System.Windows.Forms.Panel panelPlateRJ2;
        private System.Windows.Forms.Panel panelPlateRJ3;
    }
}
