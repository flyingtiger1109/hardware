using System.Drawing;
using System.Windows.Forms;

namespace HZCYKJTHardWare.CSharpDemo
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelTop;
        private Button btnInit;
        private Button btnRelease;
        private Button btnSwitch1;
        private Button btnSwitch2;
        private Button btnStartProcess;
        private Button btnEndProcess;
        private Label lblSaveDir;
        private TextBox txtSaveDir;
        private Label lblSaveDirHk;
        private TextBox txtSaveDirHk;
        private Label lblDeviceMode;
        private Label lblPreviewDevice;
        private ComboBox cmbPreviewDevice;
        private Button btnStartSelectedPreview;
        private Button btnStopSelectedPreview;
        private Button btnFaceCapture;
        private Button btnFingerprintCapture;
        private Button btnOcr;
        private Button btnNfc;
        private Button btnIrisCapture;
        private Button btnAuthorize;
        private Label lblAuthSample;
        private Label lblAuthZJHM;
        private Label lblAuthZJLB;
        private Label lblAuthGJDQDM;
        private Label lblAuthXM;
        private Label lblAuthXB;
        private Label lblAuthCSRQ;
        private Label lblAuthKADM;
        private TextBox txtAuthZJHM;
        private TextBox txtAuthZJLB;
        private TextBox txtAuthGJDQDM;
        private TextBox txtAuthXM;
        private TextBox txtAuthXB;
        private TextBox txtAuthCSRQ;
        private TextBox txtAuthKADM;
        private TableLayoutPanel previewLayout;
        private CaptionPanel panelCamera;
        private CaptionPanel panelFingerprint;
        private CaptionPanel panelIris;
        private CaptionPanel panelPlateCJ;
        private CaptionPanel panelPlateRJ2;
        private CaptionPanel panelPlateRJ3;
        private TextBox txtLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.panelTop = new Panel();
            this.btnInit = new Button();
            this.btnRelease = new Button();
            this.btnSwitch1 = new Button();
            this.btnSwitch2 = new Button();
            this.btnStartProcess = new Button();
            this.btnEndProcess = new Button();
            this.lblSaveDir = new Label();
            this.txtSaveDir = new TextBox();
            this.lblSaveDirHk = new Label();
            this.txtSaveDirHk = new TextBox();
            this.lblDeviceMode = new Label();
            this.lblPreviewDevice = new Label();
            this.cmbPreviewDevice = new ComboBox();
            this.btnStartSelectedPreview = new Button();
            this.btnStopSelectedPreview = new Button();
            this.btnFaceCapture = new Button();
            this.btnFingerprintCapture = new Button();
            this.btnOcr = new Button();
            this.btnNfc = new Button();
            this.btnIrisCapture = new Button();
            this.btnAuthorize = new Button();
            this.lblAuthSample = new Label();
            this.lblAuthZJHM = new Label();
            this.lblAuthZJLB = new Label();
            this.lblAuthGJDQDM = new Label();
            this.lblAuthXM = new Label();
            this.lblAuthXB = new Label();
            this.lblAuthCSRQ = new Label();
            this.lblAuthKADM = new Label();
            this.txtAuthZJHM = new TextBox();
            this.txtAuthZJLB = new TextBox();
            this.txtAuthGJDQDM = new TextBox();
            this.txtAuthXM = new TextBox();
            this.txtAuthXB = new TextBox();
            this.txtAuthCSRQ = new TextBox();
            this.txtAuthKADM = new TextBox();
            this.previewLayout = new TableLayoutPanel();
            this.panelCamera = new CaptionPanel();
            this.panelFingerprint = new CaptionPanel();
            this.panelIris = new CaptionPanel();
            this.panelPlateCJ = new CaptionPanel();
            this.panelPlateRJ2 = new CaptionPanel();
            this.panelPlateRJ3 = new CaptionPanel();
            this.txtLog = new TextBox();
            this.panelTop.SuspendLayout();
            this.previewLayout.SuspendLayout();
            this.SuspendLayout();
            //
            // panelTop
            //
            this.panelTop.Controls.Add(this.btnInit);
            this.panelTop.Controls.Add(this.btnRelease);
            this.panelTop.Controls.Add(this.btnSwitch1);
            this.panelTop.Controls.Add(this.btnSwitch2);
            this.panelTop.Controls.Add(this.btnStartProcess);
            this.panelTop.Controls.Add(this.btnEndProcess);
            this.panelTop.Controls.Add(this.lblSaveDir);
            this.panelTop.Controls.Add(this.txtSaveDir);
            this.panelTop.Controls.Add(this.lblSaveDirHk);
            this.panelTop.Controls.Add(this.txtSaveDirHk);
            this.panelTop.Controls.Add(this.lblDeviceMode);
            this.panelTop.Controls.Add(this.lblPreviewDevice);
            this.panelTop.Controls.Add(this.cmbPreviewDevice);
            this.panelTop.Controls.Add(this.btnStartSelectedPreview);
            this.panelTop.Controls.Add(this.btnStopSelectedPreview);
            this.panelTop.Controls.Add(this.btnFaceCapture);
            this.panelTop.Controls.Add(this.btnFingerprintCapture);
            this.panelTop.Controls.Add(this.btnOcr);
            this.panelTop.Controls.Add(this.btnNfc);
            this.panelTop.Controls.Add(this.btnIrisCapture);
            this.panelTop.Controls.Add(this.btnAuthorize);
            this.panelTop.Controls.Add(this.lblAuthSample);
            this.panelTop.Controls.Add(this.lblAuthZJHM);
            this.panelTop.Controls.Add(this.lblAuthZJLB);
            this.panelTop.Controls.Add(this.lblAuthGJDQDM);
            this.panelTop.Controls.Add(this.lblAuthXM);
            this.panelTop.Controls.Add(this.lblAuthXB);
            this.panelTop.Controls.Add(this.lblAuthCSRQ);
            this.panelTop.Controls.Add(this.lblAuthKADM);
            this.panelTop.Controls.Add(this.txtAuthZJHM);
            this.panelTop.Controls.Add(this.txtAuthZJLB);
            this.panelTop.Controls.Add(this.txtAuthGJDQDM);
            this.panelTop.Controls.Add(this.txtAuthXM);
            this.panelTop.Controls.Add(this.txtAuthXB);
            this.panelTop.Controls.Add(this.txtAuthCSRQ);
            this.panelTop.Controls.Add(this.txtAuthKADM);
            this.panelTop.Dock = DockStyle.Top;
            this.panelTop.Location = new Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new Size(964, 208);
            this.panelTop.TabIndex = 0;
            //
            // btnInit
            //
            this.btnInit.Location = new Point(8, 8);
            this.btnInit.Name = "btnInit";
            this.btnInit.Size = new Size(100, 25);
            this.btnInit.TabIndex = 0;
            this.btnInit.Text = "初始化";
            this.btnInit.UseVisualStyleBackColor = true;
            this.btnInit.Click += this.btnInit_Click;
            //
            // btnRelease
            //
            this.btnRelease.Location = new Point(114, 8);
            this.btnRelease.Name = "btnRelease";
            this.btnRelease.Size = new Size(100, 25);
            this.btnRelease.TabIndex = 1;
            this.btnRelease.Text = "释放";
            this.btnRelease.UseVisualStyleBackColor = true;
            this.btnRelease.Click += this.btnRelease_Click;
            //
            // btnSwitch1
            //
            this.btnSwitch1.Location = new Point(220, 8);
            this.btnSwitch1.Name = "btnSwitch1";
            this.btnSwitch1.Size = new Size(100, 25);
            this.btnSwitch1.TabIndex = 2;
            this.btnSwitch1.Text = "终端1";
            this.btnSwitch1.UseVisualStyleBackColor = true;
            this.btnSwitch1.Click += this.btnSwitch1_Click;
            //
            // btnSwitch2
            //
            this.btnSwitch2.Location = new Point(326, 8);
            this.btnSwitch2.Name = "btnSwitch2";
            this.btnSwitch2.Size = new Size(100, 25);
            this.btnSwitch2.TabIndex = 3;
            this.btnSwitch2.Text = "终端2";
            this.btnSwitch2.UseVisualStyleBackColor = true;
            this.btnSwitch2.Click += this.btnSwitch2_Click;
            //
            // btnStartProcess
            //
            this.btnStartProcess.Location = new Point(432, 8);
            this.btnStartProcess.Name = "btnStartProcess";
            this.btnStartProcess.Size = new Size(100, 25);
            this.btnStartProcess.TabIndex = 4;
            this.btnStartProcess.Text = "开始流程";
            this.btnStartProcess.UseVisualStyleBackColor = true;
            this.btnStartProcess.Click += this.btnStartProcess_Click;
            //
            // btnEndProcess
            //
            this.btnEndProcess.Location = new Point(538, 8);
            this.btnEndProcess.Name = "btnEndProcess";
            this.btnEndProcess.Size = new Size(100, 25);
            this.btnEndProcess.TabIndex = 5;
            this.btnEndProcess.Text = "结束流程";
            this.btnEndProcess.UseVisualStyleBackColor = true;
            this.btnEndProcess.Click += this.btnEndProcess_Click;
            //
            // lblDeviceMode
            //
            this.lblDeviceMode.AutoSize = true;
            this.lblDeviceMode.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            this.lblDeviceMode.Location = new Point(662, 13);
            this.lblDeviceMode.Name = "lblDeviceMode";
            this.lblDeviceMode.Size = new Size(110, 14);
            this.lblDeviceMode.Text = "设备模式：模式 1";
            //
            // lblSaveDir
            //
            this.lblSaveDir.AutoSize = true;
            this.lblSaveDir.Location = new Point(8, 45);
            this.lblSaveDir.Text = "主图目录";
            //
            // txtSaveDir
            //
            this.txtSaveDir.Location = new Point(70, 42);
            this.txtSaveDir.Name = "txtSaveDir";
            this.txtSaveDir.Size = new Size(300, 22);
            this.txtSaveDir.TabIndex = 6;
            this.txtSaveDir.Text = @".\captures";
            //
            // lblSaveDirHk
            //
            this.lblSaveDirHk.AutoSize = true;
            this.lblSaveDirHk.Location = new Point(382, 45);
            this.lblSaveDirHk.Text = "无畸变文件";
            //
            // txtSaveDirHk
            //
            this.txtSaveDirHk.Location = new Point(466, 42);
            this.txtSaveDirHk.Name = "txtSaveDirHk";
            this.txtSaveDirHk.Size = new Size(300, 22);
            this.txtSaveDirHk.TabIndex = 7;
            this.txtSaveDirHk.Text = @".\captures_hk\fingerprint_undistorted.bmp";
            //
            // lblPreviewDevice
            //
            this.lblPreviewDevice.AutoSize = true;
            this.lblPreviewDevice.Location = new Point(8, 82);
            this.lblPreviewDevice.Name = "lblPreviewDevice";
            this.lblPreviewDevice.Size = new Size(56, 14);
            this.lblPreviewDevice.Text = "预览设备";
            //
            // cmbPreviewDevice
            //
            this.cmbPreviewDevice.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbPreviewDevice.FormattingEnabled = true;
            this.cmbPreviewDevice.Location = new Point(70, 78);
            this.cmbPreviewDevice.Name = "cmbPreviewDevice";
            this.cmbPreviewDevice.Size = new Size(194, 22);
            this.cmbPreviewDevice.TabIndex = 8;
            //
            // btnStartSelectedPreview
            //
            this.btnStartSelectedPreview.Location = new Point(274, 76);
            this.btnStartSelectedPreview.Name = "btnStartSelectedPreview";
            this.btnStartSelectedPreview.Size = new Size(130, 25);
            this.btnStartSelectedPreview.TabIndex = 9;
            this.btnStartSelectedPreview.Text = "开始预览";
            this.btnStartSelectedPreview.UseVisualStyleBackColor = true;
            this.btnStartSelectedPreview.Click += this.btnStartSelectedPreview_Click;
            //
            // btnStopSelectedPreview
            //
            this.btnStopSelectedPreview.Location = new Point(410, 76);
            this.btnStopSelectedPreview.Name = "btnStopSelectedPreview";
            this.btnStopSelectedPreview.Size = new Size(130, 25);
            this.btnStopSelectedPreview.TabIndex = 10;
            this.btnStopSelectedPreview.Text = "停止预览";
            this.btnStopSelectedPreview.UseVisualStyleBackColor = true;
            this.btnStopSelectedPreview.Click += this.btnStopSelectedPreview_Click;
            //
            // btnFaceCapture
            //
            this.btnFaceCapture.Location = new Point(8, 112);
            this.btnFaceCapture.Name = "btnFaceCapture";
            this.btnFaceCapture.Size = new Size(100, 25);
            this.btnFaceCapture.TabIndex = 11;
            this.btnFaceCapture.Text = "人脸抓拍";
            this.btnFaceCapture.UseVisualStyleBackColor = true;
            this.btnFaceCapture.Click += this.btnFaceCapture_Click;
            //
            // btnFingerprintCapture
            //
            this.btnFingerprintCapture.Location = new Point(114, 112);
            this.btnFingerprintCapture.Name = "btnFingerprintCapture";
            this.btnFingerprintCapture.Size = new Size(100, 25);
            this.btnFingerprintCapture.TabIndex = 12;
            this.btnFingerprintCapture.Text = "指纹抓拍";
            this.btnFingerprintCapture.UseVisualStyleBackColor = true;
            this.btnFingerprintCapture.Click += this.btnFingerprintCapture_Click;
            //
            // btnOcr
            //
            this.btnOcr.Location = new Point(220, 112);
            this.btnOcr.Name = "btnOcr";
            this.btnOcr.Size = new Size(100, 25);
            this.btnOcr.TabIndex = 13;
            this.btnOcr.Text = "OCR";
            this.btnOcr.UseVisualStyleBackColor = true;
            this.btnOcr.Click += this.btnOcr_Click;
            //
            // btnNfc
            //
            this.btnNfc.Location = new Point(326, 112);
            this.btnNfc.Name = "btnNfc";
            this.btnNfc.Size = new Size(100, 25);
            this.btnNfc.TabIndex = 14;
            this.btnNfc.Text = "NFC/IC";
            this.btnNfc.UseVisualStyleBackColor = true;
            this.btnNfc.Click += this.btnNfc_Click;
            //
            // btnIrisCapture
            //
            this.btnIrisCapture.Location = new Point(432, 112);
            this.btnIrisCapture.Name = "btnIrisCapture";
            this.btnIrisCapture.Size = new Size(100, 25);
            this.btnIrisCapture.TabIndex = 15;
            this.btnIrisCapture.Text = "虹膜抓拍";
            this.btnIrisCapture.UseVisualStyleBackColor = true;
            this.btnIrisCapture.Click += this.btnIrisCapture_Click;
            //
            // btnAuthorize
            //
            this.btnAuthorize.Location = new Point(538, 112);
            this.btnAuthorize.Name = "btnAuthorize";
            this.btnAuthorize.Size = new Size(120, 25);
            this.btnAuthorize.TabIndex = 16;
            this.btnAuthorize.Text = "授权模拟";
            this.btnAuthorize.UseVisualStyleBackColor = true;
            this.btnAuthorize.Click += this.btnAuthorize_Click;
            //
            // labels and auth inputs
            //
            this.lblAuthSample.AutoSize = true;
            this.lblAuthSample.Location = new Point(8, 145);
            this.lblAuthSample.Text = "授权模拟参数";
            this.lblAuthZJHM.AutoSize = true;
            this.lblAuthZJHM.Location = new Point(8, 164);
            this.lblAuthZJHM.Text = "证件号码";
            this.lblAuthZJLB.AutoSize = true;
            this.lblAuthZJLB.Location = new Point(170, 164);
            this.lblAuthZJLB.Text = "证件类别";
            this.lblAuthGJDQDM.AutoSize = true;
            this.lblAuthGJDQDM.Location = new Point(256, 164);
            this.lblAuthGJDQDM.Text = "国家地区代码";
            this.lblAuthXM.AutoSize = true;
            this.lblAuthXM.Location = new Point(370, 164);
            this.lblAuthXM.Text = "姓名";
            this.lblAuthXB.AutoSize = true;
            this.lblAuthXB.Location = new Point(492, 164);
            this.lblAuthXB.Text = "性别";
            this.lblAuthCSRQ.AutoSize = true;
            this.lblAuthCSRQ.Location = new Point(558, 164);
            this.lblAuthCSRQ.Text = "出生日期";
            this.lblAuthKADM.AutoSize = true;
            this.lblAuthKADM.Location = new Point(680, 164);
            this.lblAuthKADM.Text = "口岸代码";
            this.txtAuthZJHM.Location = new Point(8, 180);
            this.txtAuthZJHM.Size = new Size(154, 22);
            this.txtAuthZJHM.TabIndex = 17;
            this.txtAuthZJHM.Text = "H111111111";
            this.txtAuthZJLB.Location = new Point(170, 180);
            this.txtAuthZJLB.Size = new Size(78, 22);
            this.txtAuthZJLB.TabIndex = 18;
            this.txtAuthZJLB.Text = "24";
            this.txtAuthGJDQDM.Location = new Point(256, 180);
            this.txtAuthGJDQDM.Size = new Size(106, 22);
            this.txtAuthGJDQDM.TabIndex = 19;
            this.txtAuthGJDQDM.Text = "HKG";
            this.txtAuthXM.Location = new Point(370, 180);
            this.txtAuthXM.Size = new Size(114, 22);
            this.txtAuthXM.TabIndex = 20;
            this.txtAuthXM.Text = "TEST";
            this.txtAuthXB.Location = new Point(492, 180);
            this.txtAuthXB.Size = new Size(58, 22);
            this.txtAuthXB.TabIndex = 21;
            this.txtAuthXB.Text = "M";
            this.txtAuthCSRQ.Location = new Point(558, 180);
            this.txtAuthCSRQ.Size = new Size(114, 22);
            this.txtAuthCSRQ.TabIndex = 22;
            this.txtAuthCSRQ.Text = "19950101";
            this.txtAuthKADM.Location = new Point(680, 180);
            this.txtAuthKADM.Size = new Size(82, 22);
            this.txtAuthKADM.TabIndex = 23;
            this.txtAuthKADM.Text = "414";
            //
            // previewLayout
            //
            this.previewLayout.ColumnCount = 3;
            this.previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            this.previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            this.previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33334F));
            this.previewLayout.Controls.Add(this.panelCamera, 0, 0);
            this.previewLayout.Controls.Add(this.panelFingerprint, 1, 0);
            this.previewLayout.Controls.Add(this.panelIris, 2, 0);
            this.previewLayout.Controls.Add(this.panelPlateCJ, 0, 1);
            this.previewLayout.Controls.Add(this.panelPlateRJ2, 1, 1);
            this.previewLayout.Controls.Add(this.panelPlateRJ3, 2, 1);
            this.previewLayout.Dock = DockStyle.Fill;
            this.previewLayout.Location = new Point(0, 208);
            this.previewLayout.Name = "previewLayout";
            this.previewLayout.RowCount = 2;
            this.previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.previewLayout.Size = new Size(1200, 372);
            this.previewLayout.TabIndex = 1;
            //
            // preview panels
            //
            ConfigurePreviewPanel(this.panelCamera, "视频预览");
            ConfigurePreviewPanel(this.panelFingerprint, "指纹预览");
            ConfigurePreviewPanel(this.panelIris, "虹膜预览");
            ConfigurePreviewPanel(this.panelPlateCJ, "出境车牌预览");
            ConfigurePreviewPanel(this.panelPlateRJ2, "入境车牌预览 2");
            ConfigurePreviewPanel(this.panelPlateRJ3, "入境车牌预览 3");
            //
            // txtLog
            //
            this.txtLog.Dock = DockStyle.Bottom;
            this.txtLog.Location = new Point(0, 580);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ScrollBars = ScrollBars.Both;
            this.txtLog.Size = new Size(1200, 140);
            this.txtLog.TabIndex = 2;
            this.txtLog.WordWrap = false;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.ClientSize = new Size(1200, 720);
            this.Controls.Add(this.previewLayout);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.panelTop);
            this.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            this.MinimumSize = new Size(980, 680);
            this.Name = "MainForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "HZCYKJTHardWare DLL Test";
            this.FormClosing += this.MainForm_FormClosing;
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.previewLayout.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private static void ConfigurePreviewPanel(CaptionPanel panel, string caption)
        {
            panel.BackColor = Color.Black;
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.CaptionText = caption;
            panel.Dock = DockStyle.Fill;
            panel.ForeColor = Color.White;
            panel.Margin = new Padding(0);
        }
    }
}
