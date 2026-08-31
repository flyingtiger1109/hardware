using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using HZCYKJTHardWare.CSharpDemo.Native;

namespace HZCYKJTHardWare.CSharpDemo
{
    internal class CaptionPanel : Panel
    {
        public string CaptionText { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (string.IsNullOrEmpty(CaptionText))
            {
                return;
            }

            using (var brush = new SolidBrush(ForeColor))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                e.Graphics.DrawString(CaptionText, Font, brush, ClientRectangle, format);
            }
        }
    }

    public partial class MainForm : Form
    {
        private sealed class DeviceModeSettings
        {
            public int Mode { get; set; }
            public string Name { get; set; }
            public string Warning { get; set; }
        }

        private sealed class PreviewOption
        {
            public PreviewOption(string name, CaptionPanel panel,
                Func<IntPtr, int> start, Func<int> stop)
            {
                Name = name;
                Panel = panel;
                Start = start;
                Stop = stop;
            }

            public string Name { get; private set; }
            public CaptionPanel Panel { get; private set; }
            public Func<IntPtr, int> Start { get; private set; }
            public Func<int> Stop { get; private set; }

            public override string ToString()
            {
                return Name;
            }
        }

        private bool _initialized;
        private readonly HzcyHardwareNative.EventCallbackDelegate _eventCallback;
        private readonly DeviceModeSettings _deviceModeSettings;
        private readonly List<PreviewOption> _previewOptions = new List<PreviewOption>();
        private FlowLayoutPanel _mode2Toolbar;

        public MainForm()
        {
            InitializeComponent();
            ApplyWindowIcon();
            _eventCallback = OnNativeEvent;
            _deviceModeSettings = LoadDeviceModeSettings();
            ApplyDeviceMode();
            if (!string.IsNullOrEmpty(_deviceModeSettings.Warning))
            {
                Log("[配置警告] " + _deviceModeSettings.Warning);
            }
            Log(string.Format("设备模式：模式 {0} / {1}",
                _deviceModeSettings.Mode, _deviceModeSettings.Name));
            Log("就绪，请点击[初始化]开始。");
        }

        private void ApplyDeviceMode()
        {
            var isRjOnly = _deviceModeSettings.Mode == 2;
            lblDeviceMode.Text = string.Format("设备模式：模式 {0} / {1}",
                _deviceModeSettings.Mode, _deviceModeSettings.Name);

            _previewOptions.Clear();
            if (!isRjOnly)
            {
                _previewOptions.Add(new PreviewOption("摄像头", panelCamera,
                    HzcyHardwareNative.HZCYKJTHardWare_StartCameraPreview,
                    HzcyHardwareNative.HZCYKJTHardWare_StopCameraPreview));
                _previewOptions.Add(new PreviewOption("指纹", panelFingerprint,
                    HzcyHardwareNative.HZCYKJTHardWare_StartFingerprintPreview,
                    HzcyHardwareNative.HZCYKJTHardWare_StopFingerprintPreview));
                _previewOptions.Add(new PreviewOption("虹膜", panelIris,
                    HzcyHardwareNative.HZCYKJTHardWare_StartIrisPreview,
                    HzcyHardwareNative.HZCYKJTHardWare_StopIrisPreview));
                _previewOptions.Add(new PreviewOption("出境车牌 CJ", panelPlateCJ,
                    HzcyHardwareNative.HZCYKJTHardWare_StartPlatePreviewCJ,
                    HzcyHardwareNative.HZCYKJTHardWare_StopPlatePreviewCJ));
            }

            _previewOptions.Add(new PreviewOption("入境车牌 RJ2", panelPlateRJ2,
                HzcyHardwareNative.HZCYKJTHardWare_StartPlatePreviewRJ2,
                HzcyHardwareNative.HZCYKJTHardWare_StopPlatePreviewRJ2));
            _previewOptions.Add(new PreviewOption("入境车牌 RJ3", panelPlateRJ3,
                HzcyHardwareNative.HZCYKJTHardWare_StartPlatePreviewRJ3,
                HzcyHardwareNative.HZCYKJTHardWare_StopPlatePreviewRJ3));

            cmbPreviewDevice.Items.Clear();
            foreach (var option in _previewOptions)
            {
                cmbPreviewDevice.Items.Add(option);
            }
            if (cmbPreviewDevice.Items.Count > 0)
            {
                cmbPreviewDevice.SelectedIndex = 0;
            }

            ConfigurePreviewLayout(isRjOnly);
            if (isRjOnly)
            {
                HideMode2UnsupportedControls();
            }
        }

        private void ConfigurePreviewLayout(bool isRjOnly)
        {
            previewLayout.SuspendLayout();
            previewLayout.Controls.Clear();
            previewLayout.ColumnStyles.Clear();
            previewLayout.RowStyles.Clear();

            if (isRjOnly)
            {
                previewLayout.ColumnCount = 2;
                previewLayout.RowCount = 1;
                previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                previewLayout.Controls.Add(panelPlateRJ2, 0, 0);
                previewLayout.Controls.Add(panelPlateRJ3, 1, 0);
            }
            else
            {
                previewLayout.ColumnCount = 3;
                previewLayout.RowCount = 2;
                for (var i = 0; i < 3; i++)
                {
                    previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
                }
                previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
                previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
                previewLayout.Controls.Add(panelCamera, 0, 0);
                previewLayout.Controls.Add(panelFingerprint, 1, 0);
                previewLayout.Controls.Add(panelIris, 2, 0);
                previewLayout.Controls.Add(panelPlateCJ, 0, 1);
                previewLayout.Controls.Add(panelPlateRJ2, 1, 1);
                previewLayout.Controls.Add(panelPlateRJ3, 2, 1);
            }

            previewLayout.ResumeLayout(true);
        }

        private void HideMode2UnsupportedControls()
        {
            var unsupportedControls = new Control[]
            {
                btnSwitch1, btnSwitch2, btnStartProcess, btnEndProcess,
                lblSaveDir, txtSaveDir, lblSaveDirHk, txtSaveDirHk,
                btnFaceCapture, btnFingerprintCapture, btnOcr, btnNfc,
                btnIrisCapture, btnAuthorize, lblAuthSample, lblAuthZJHM,
                lblAuthZJLB, lblAuthGJDQDM, lblAuthXM, lblAuthXB,
                lblAuthCSRQ, lblAuthKADM, txtAuthZJHM, txtAuthZJLB,
                txtAuthGJDQDM, txtAuthXM, txtAuthXB, txtAuthCSRQ, txtAuthKADM,
                btnSavePlateCJ
            };

            foreach (var control in unsupportedControls)
            {
                control.Visible = false;
            }

            ConfigureMode2Toolbar();
        }

        private void ConfigureMode2Toolbar()
        {
            panelTop.SuspendLayout();

            _mode2Toolbar = new FlowLayoutPanel
            {
                AutoScroll = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                Name = "mode2Toolbar",
                Padding = new Padding(8),
                WrapContents = false
            };

            SetToolbarControlMargin(btnInit, 0);
            SetToolbarControlMargin(btnRelease, 6);
            SetToolbarControlMargin(lblPreviewDevice, 18);
            SetToolbarControlMargin(cmbPreviewDevice, 6);
            SetToolbarControlMargin(btnStartSelectedPreview, 6);
            SetToolbarControlMargin(btnStopSelectedPreview, 6);
            SetToolbarControlMargin(lblDeviceMode, 18);

            _mode2Toolbar.Controls.Add(btnInit);
            _mode2Toolbar.Controls.Add(btnRelease);
            _mode2Toolbar.Controls.Add(lblPreviewDevice);
            _mode2Toolbar.Controls.Add(cmbPreviewDevice);
            _mode2Toolbar.Controls.Add(btnStartSelectedPreview);
            _mode2Toolbar.Controls.Add(btnStopSelectedPreview);
            _mode2Toolbar.Controls.Add(lblDeviceMode);

            panelTop.Controls.Add(_mode2Toolbar);
            panelTop.AutoSize = true;
            panelTop.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTop.ResumeLayout(true);
        }

        private static void SetToolbarControlMargin(Control control, int left)
        {
            var verticalMargin = control is Label ? 6 : 0;
            control.Margin = new Padding(left, verticalMargin, 0, 0);
        }

        private void ApplyWindowIcon()
        {
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_initialized)
            {
                try
                {
                    HzcyHardwareNative.HZCYKJTHardWare_ReleaseSdk();
                }
                catch
                {
                    // Form closing should not be blocked by native cleanup errors.
                }
                _initialized = false;
            }
        }

        private void btnInit_Click(object sender, EventArgs e)
        {
            if (_initialized)
            {
                Log("SDK 已初始化。");
                return;
            }

            ExecuteDllCall("初始化SDK", () =>
            {
                var ret = HzcyHardwareNative.HZCYKJTHardWare_InitSdk();
                LogRet("初始化SDK", ret);

                if (ret == 1)
                {
                    _initialized = true;
                    LogRet("注册事件回调", HzcyHardwareNative.HZCYKJTHardWare_RegisterEventCallback(_eventCallback));
                }
            });
        }

        private void btnRelease_Click(object sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            ExecuteDllCall("释放SDK", () =>
            {
                LogRet("释放SDK", HzcyHardwareNative.HZCYKJTHardWare_ReleaseSdk());
                _initialized = false;
            });
        }

        private void btnSwitch1_Click(object sender, EventArgs e)
        {
            ExecuteDllCall("切换终端1", () => LogRet("切换终端1", HzcyHardwareNative.HZCYKJTHardWare_SwitchTerminal(1)));
        }

        private void btnSwitch2_Click(object sender, EventArgs e)
        {
            ExecuteDllCall("切换终端2", () => LogRet("切换终端2", HzcyHardwareNative.HZCYKJTHardWare_SwitchTerminal(2)));
        }

        private void btnStartProcess_Click(object sender, EventArgs e)
        {
            ExecuteWithSaveDir("开始流程", ptr => HzcyHardwareNative.HZCYKJTHardWare_StartProcess(ptr));
        }

        private void btnEndProcess_Click(object sender, EventArgs e)
        {
            ExecuteDllCall("结束流程", () => LogRet("结束流程", HzcyHardwareNative.HZCYKJTHardWare_EndProcess()));
        }

        private void btnStartSelectedPreview_Click(object sender, EventArgs e)
        {
            var option = cmbPreviewDevice.SelectedItem as PreviewOption;
            if (option == null)
            {
                Log("请先选择预览设备。");
                return;
            }

            RunPreviewDllCallAsync("开始" + option.Name + "预览",
                option.Panel.Handle, option.Start);
        }

        private void btnStopSelectedPreview_Click(object sender, EventArgs e)
        {
            var option = cmbPreviewDevice.SelectedItem as PreviewOption;
            if (option == null)
            {
                Log("请先选择预览设备。");
                return;
            }

            ExecuteDllCall("停止" + option.Name + "预览",
                () => LogRet("停止" + option.Name + "预览", option.Stop()));
        }

        private void btnSavePlateCJ_Click(object sender, EventArgs e)
        {
            SaveLatestPlateFrame("CJ", HzcyHardwareNative.PlateCameraCj);
        }

        private void btnSavePlateRJ2_Click(object sender, EventArgs e)
        {
            SaveLatestPlateFrame("RJ2", HzcyHardwareNative.PlateCameraRj2);
        }

        private void btnSavePlateRJ3_Click(object sender, EventArgs e)
        {
            SaveLatestPlateFrame("RJ3", HzcyHardwareNative.PlateCameraRj3);
        }

        private void SaveLatestPlateFrame(string cameraName, int cameraType)
        {
            var savePath = (txtPlateSavePath.Text ?? string.Empty).Trim();
            if (savePath.Length == 0)
            {
                Log("保存" + cameraName + "最新车牌帧失败：请填写完整图片保存路径。返回码(-3)");
                return;
            }

            ExecuteDllCall("保存" + cameraName + "最新车牌帧", () =>
            {
                using (var path = new Utf8NativeString(savePath))
                {
                    var ret = HzcyHardwareNative.HZCYKJTHardWare_SaveLatestPlateFrame(
                        path.Pointer, cameraType);
                    LogRet("保存" + cameraName + "最新车牌帧", ret);
                    if (ret == 1)
                    {
                        Log("  保存路径：" + savePath);
                    }
                    else
                    {
                        Log("  失败提示：请确认对应预览已启动、最新帧未过期，并检查目标目录权限。");
                    }
                }
            });
        }

        private void btnFaceCapture_Click(object sender, EventArgs e)
        {
            ExecuteWithSaveDir("人脸抓拍", ptr => HzcyHardwareNative.HZCYKJTHardWare_CaptureCameraImage(ptr));
        }

        private void btnFingerprintCapture_Click(object sender, EventArgs e)
        {
            ExecuteWithFingerprintSaveDirs("指纹抓拍", (saveDir, saveDirHk) =>
                HzcyHardwareNative.HZCYKJTHardWare_CaptureFingerprintImage(saveDir, saveDirHk));
        }

        private void btnOcr_Click(object sender, EventArgs e)
        {
            ExecuteWithSaveDir("OCR识别", ptr => HzcyHardwareNative.HZCYKJTHardWare_RequestOCR(ptr));
        }

        private void btnNfc_Click(object sender, EventArgs e)
        {
            ExecuteWithSaveDir("NFC/IC读卡", ptr => HzcyHardwareNative.HZCYKJTHardWare_RequestNfcCard(ptr));
        }

        private void btnIrisCapture_Click(object sender, EventArgs e)
        {
            ExecuteWithSaveDir("虹膜抓拍", ptr => HzcyHardwareNative.HZCYKJTHardWare_CaptureIrisImage(ptr));
        }

        private void btnAuthorize_Click(object sender, EventArgs e)
        {
            ExecuteDllCall("授权模拟", () =>
            {
                using (var zjhm = new Utf8NativeString(txtAuthZJHM.Text))
                using (var zjlb = new Utf8NativeString(txtAuthZJLB.Text))
                using (var gjdqdm = new Utf8NativeString(txtAuthGJDQDM.Text))
                using (var xm = new Utf8NativeString(txtAuthXM.Text))
                using (var xb = new Utf8NativeString(txtAuthXB.Text))
                using (var csrq = new Utf8NativeString(txtAuthCSRQ.Text))
                using (var kadm = new Utf8NativeString(txtAuthKADM.Text))
                {
                    Log("已提交授权模拟参数：" +
                        "ZJHM=" + txtAuthZJHM.Text +
                        ", XM=" + txtAuthXM.Text +
                        ", ZJLB=" + txtAuthZJLB.Text +
                        ", GJDQDM=" + txtAuthGJDQDM.Text +
                        ", XB=" + txtAuthXB.Text +
                        ", CSRQ=" + txtAuthCSRQ.Text +
                        ", KADM=" + txtAuthKADM.Text);

                    var ret = HzcyHardwareNative.HZCYKJTHardWare_RequestAuthorize(
                        zjhm.Pointer,
                        zjlb.Pointer,
                        gjdqdm.Pointer,
                        xm.Pointer,
                        xb.Pointer,
                        csrq.Pointer,
                        kadm.Pointer);
                    LogRet("授权模拟", ret);
                }
            });
        }

        private void ExecuteWithSaveDir(string name, Func<IntPtr, int> action)
        {
            ExecuteDllCall(name, () =>
            {
                using (var saveDir = new Utf8NativeString(txtSaveDir.Text))
                {
                    LogRet(name, action(saveDir.Pointer));
                }
            });
        }

        private void ExecuteWithFingerprintSaveDirs(string name, Func<IntPtr, IntPtr, int> action)
        {
            var saveDirText = txtSaveDir.Text;
            var saveDirHkText = NormalizeFingerprintUndistortedPath(txtSaveDirHk.Text);
            if (!string.Equals(txtSaveDirHk.Text, saveDirHkText,
                StringComparison.Ordinal))
            {
                txtSaveDirHk.Text = saveDirHkText;
            }

            ExecuteDllCall(name, () =>
            {
                using (var saveDir = new Utf8NativeString(saveDirText))
                using (var saveDirHk = new Utf8NativeString(saveDirHkText))
                {
                    LogRet(name, action(saveDir.Pointer, saveDirHk.Pointer));
                }
            });
        }

        private static string NormalizeFingerprintUndistortedPath(string path)
        {
            var normalized = (path ?? string.Empty).Trim();
            if (normalized.Length == 0 || Path.HasExtension(normalized))
                return normalized;

            return Path.Combine(normalized, "fingerprint_undistorted.bmp");
        }

        private void RunPreviewDllCallAsync(string name, IntPtr targetHwnd, Func<IntPtr, int> action)
        {
            Log(name + " 已提交到后台线程：HWND=" + targetHwnd.ToInt64());

            Task.Run(() =>
            {
                try
                {
                    var ret = action(targetHwnd);
                    SafeBeginInvoke(() => LogRet(name, ret));
                }
                catch (Exception ex)
                {
                    SafeBeginInvoke(() => LogNativeException(name, ex));
                }
            });
        }

        private void OnNativeEvent(IntPtr eventJson)
        {
            var json = Utf8NativeString.FromPointer(eventJson);
            SafeBeginInvoke(() => OnEventJson(json));
        }

        private void OnEventJson(string json)
        {
            var eventType = ExtractJsonInt(json, "event_type");
            var status = ExtractJsonInt(json, "status");
            var resourceType = ExtractJsonString(json, "resource_type");
            var requestId = ExtractJsonString(json, "request_id");
            var errorCode = ExtractJsonString(json, "error_code");
            if (string.IsNullOrEmpty(errorCode))
            {
                errorCode = ExtractJsonString(json, "code");
            }
            var message = ExtractJsonString(json, "message");
            var mrz = ExtractJsonString(json, "mrz");
            var icNumber = ExtractJsonString(json, "ic_number");
            var savePath = ExtractJsonString(json, "save_path");
            var cardType = resourceType == "ocr_document"
                ? ExtractJsonInt(json, "card_type", -1)
                : -1;

            Log(string.Format("[事件] {0} 状态={1} 资源={2}",
                FormatEventType(eventType), FormatResultCode(status),
                FormatResourceType(resourceType)));
            if (!string.IsNullOrEmpty(requestId))
            {
                Log("  请求 ID：" + requestId);
            }
            if (!string.IsNullOrEmpty(errorCode))
            {
                Log("  错误码：" + errorCode);
            }
            if (!string.IsNullOrEmpty(mrz))
            {
                Log(cardType == 30 && mrz.StartsWith("$", StringComparison.Ordinal)
                    ? "  ID 卡兼容串：" + mrz
                    : "  MRZ：" + mrz);
            }
            if (!string.IsNullOrEmpty(icNumber))
            {
                Log("  IC 卡号：" + icNumber);
            }
            if (!string.IsNullOrEmpty(savePath))
            {
                Log("  保存路径：" + savePath);
            }
            if (!string.IsNullOrEmpty(message))
            {
                Log("  消息：" + message);
            }
            if (resourceType == "ocr_document")
            {
                if (cardType == 30)
                {
                    var authenScore = ExtractJsonInt(json, "authen_score", -1);
                    var opticalCheckResult = ExtractJsonInt(json, "optical_check_result", -1);
                    Log("  证件类型：香港身份证(30)");
                    Log("  姓名：" + ExtractJsonString(json, "name"));
                    Log("  性别：" + ExtractJsonString(json, "sex"));
                    Log("  证件号码：" + ExtractJsonString(json, "cardId"));
                    Log("  出生日期：" + ExtractJsonString(json, "birthday"));
                    Log("  签发日期：" + ExtractJsonString(json, "dateOfissue"));
                    Log("  鉴伪分数：" + authenScore);
                    Log("  光学鉴伪结果：" + FormatOpticalCheckResult(opticalCheckResult));
                }
            }
            if (resourceType == "authorization")
            {
                Log("  授权结果=" + ExtractJsonInt(json, "auth_result"));
                Log("  证件号码=" + ExtractJsonString(json, "ZJHM"));
                Log("  证件类别=" + ExtractJsonString(json, "ZJLB"));
                Log("  国家地区代码=" + ExtractJsonString(json, "GJDQDM"));
                Log("  姓名=" + ExtractJsonString(json, "XM"));
                Log("  性别=" + ExtractJsonString(json, "XB"));
                Log("  出生日期=" + ExtractJsonString(json, "CSRQ"));
                Log("  口岸代码=" + ExtractJsonString(json, "KADM"));
            }
        }

        private void ExecuteDllCall(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogNativeException(name, ex);
            }
        }

        private void LogNativeException(string name, Exception ex)
        {
            Log(name + " 异常：" + ex.GetType().Name + ": " + ex.Message);
        }

        private static string FormatResourceType(string resourceType)
        {
            switch (resourceType)
            {
                case "face_image": return "人脸图像(face_image)";
                case "fingerprint_image": return "指纹图像(fingerprint_image)";
                case "ocr_document": return "OCR证件(ocr_document)";
                case "iris_image": return "虹膜图像(iris_image)";
                case "nfc_card": return "NFC/IC卡(nfc_card)";
                case "plate_image": return "车牌图像(plate_image)";
                case "authorization": return "授权结果(authorization)";
                default:
                    return string.IsNullOrEmpty(resourceType) ? "未知" : resourceType;
            }
        }

        private static string FormatEventType(int eventType)
        {
            switch (eventType)
            {
                case 1001: return "终端切换开始(1001)";
                case 1002: return "终端切换成功(1002)";
                case 1003: return "终端切换失败(1003)";
                case 1101: return "流程开始(1101)";
                case 1102: return "流程结束(1102)";
                case 1201: return "摄像头预览已启动(1201)";
                case 1202: return "摄像头预览已停止(1202)";
                case 1203: return "摄像头预览失败(1203)";
                case 1301: return "指纹预览已启动(1301)";
                case 1302: return "指纹预览已停止(1302)";
                case 1303: return "指纹预览失败(1303)";
                case 1401: return "人脸抓拍成功(1401)";
                case 1402: return "人脸抓拍失败(1402)";
                case 1501: return "指纹抓拍成功(1501)";
                case 1502: return "指纹抓拍失败(1502)";
                case 1601: return "OCR成功(1601)";
                case 1602: return "OCR失败(1602)";
                case 1701: return "请求超时(1701)";
                case 1801: return "虹膜预览已启动(1801)";
                case 1802: return "虹膜预览已停止(1802)";
                case 1803: return "虹膜预览失败(1803)";
                case 1804: return "虹膜抓拍成功(1804)";
                case 1805: return "虹膜抓拍失败(1805)";
                case 1806: return "NFC/IC读卡成功(1806)";
                case 1807: return "NFC/IC读卡失败(1807)";
                case 1901: return "车牌预览已启动(1901)";
                case 1902: return "车牌预览已停止(1902)";
                case 1903: return "车牌预览失败(1903)";
                case 1999: return "通用错误(1999)";
                case 2001: return "授权成功(2001)";
                case 2002: return "授权失败(2002)";
                default: return "未知事件(" + eventType + ")";
            }
        }

        private void LogRet(string name, int ret)
        {
            Log(string.Format("{0}：{1}", name, FormatResultCode(ret)));
        }

        private static string FormatResultCode(int code)
        {
            switch (code)
            {
                case 1: return "成功(1)";
                case 0: return "失败(0)";
                case -2: return "SDK未初始化(-2)";
                case -3: return "参数无效(-3)";
                case -6: return "Proxy通信失败(-6)";
                case -7: return "请求超时(-7)";
                case -10: return "对应预览未启动(-10)";
                case -14: return "保存文件失败(-14)";
                case -15: return "设备忙(-15)";
                case -18: return "当前设备模式不支持(-18)";
                case -31: return "最新帧尚未就绪(-31)";
                case -32: return "车牌镜头类型无效(-32)";
                case -33: return "JPEG数据无效(-33)";
                case -34: return "JPEG数据过大(-34)";
                case -35: return "最新帧已过期(-35)";
                default: return "返回码(" + code + ")";
            }
        }

        private static string FormatOpticalCheckResult(int result)
        {
            switch (result)
            {
                case 0: return "通过(0)";
                case 1: return "不通过(1)";
                default: return "未知/未检测(-1)";
            }
        }

        private void Log(string text)
        {
            if (InvokeRequired)
            {
                SafeBeginInvoke(() => Log(text));
                return;
            }

            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + Environment.NewLine);
        }

        private void SafeBeginInvoke(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static DeviceModeSettings LoadDeviceModeSettings()
        {
            const int defaultMode = 1;
            var settings = new DeviceModeSettings
            {
                Mode = defaultMode,
                Name = "出境模式"
            };

            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "HZCYKJTHardWare.json");
            try
            {
                if (!File.Exists(configPath))
                {
                    settings.Warning = "未找到 HZCYKJTHardWare.json，Demo 回退到 Mode 1。";
                    return settings;
                }

                // 统一配置文件允许使用 // 注释（JSONC），而 JavaScriptSerializer
                // 只接受严格 JSON，因此先安全移除注释再解析；字符串中的 // 不会被移除。
                var json = StripJsonComments(
                    File.ReadAllText(configPath, Encoding.UTF8));
                var root = new JavaScriptSerializer().DeserializeObject(json)
                    as IDictionary<string, object>;
                if (root == null)
                {
                    settings.Warning = "HZCYKJTHardWare.json 内容损坏，Demo 回退到 Mode 1。";
                    return settings;
                }

                object modeValue;
                int mode;
                if (!root.TryGetValue("device_mode", out modeValue) ||
                    !int.TryParse(Convert.ToString(modeValue, CultureInfo.InvariantCulture),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out mode))
                {
                    settings.Warning = "缺少或无法解析 device_mode，Demo 回退到 Mode 1。";
                    return settings;
                }
                if (mode != 1 && mode != 2)
                {
                    settings.Warning = "device_mode 不是 1/2，Demo 回退到 Mode 1。";
                    return settings;
                }

                settings.Mode = mode;
                settings.Name = mode == 1 ? "出境模式" : "入境模式";
                object namesValue;
                var names = root.TryGetValue("device_mode_names", out namesValue)
                    ? namesValue as IDictionary<string, object> : null;
                object nameValue;
                if (names != null && names.TryGetValue(mode.ToString(), out nameValue))
                {
                    var configuredName = Convert.ToString(nameValue,
                        CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(configuredName))
                    {
                        settings.Name = configuredName.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                settings.Mode = defaultMode;
                settings.Name = "出境模式";
                settings.Warning = "读取设备模式失败，Demo 回退到 Mode 1：" + ex.Message;
            }

            return settings;
        }

        /// <summary>
        /// 移除 JSONC 中的 // 和 /* */ 注释，同时保留字符串字面量中的内容。
        /// C# Demo 不额外引入 JSON 库，仅用现有 JavaScriptSerializer 读取统一配置。
        /// </summary>
        private static string StripJsonComments(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            var builder = new StringBuilder(json.Length);
            var inString = false;
            var escaped = false;
            var inLineComment = false;
            var inBlockComment = false;

            for (var i = 0; i < json.Length; i++)
            {
                var current = json[i];

                if (inLineComment)
                {
                    if (current == '\r' || current == '\n')
                    {
                        inLineComment = false;
                        builder.Append(current);
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && i + 1 < json.Length &&
                        json[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    else if (current == '\r' || current == '\n')
                    {
                        // 保留换行，便于 JSON 异常仍能定位原始行号。
                        builder.Append(current);
                    }
                    continue;
                }

                if (inString)
                {
                    builder.Append(current);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    builder.Append(current);
                    continue;
                }

                if (current == '/' && i + 1 < json.Length)
                {
                    var next = json[i + 1];
                    if (next == '/')
                    {
                        inLineComment = true;
                        i++;
                        continue;
                    }
                    if (next == '*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var searchKey = "\"" + key + "\"";
            var index = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (index < 0)
            {
                return string.Empty;
            }

            index += searchKey.Length;
            while (index < json.Length && IsJsonWhitespaceOrColon(json[index]))
            {
                index++;
            }

            if (index >= json.Length || json[index] != '"')
            {
                return string.Empty;
            }

            index++;
            var sb = new StringBuilder();
            var escaped = false;
            for (; index < json.Length; index++)
            {
                var ch = json[index];
                if (escaped)
                {
                    switch (ch)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append(ch); break;
                    }
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static int ExtractJsonInt(string json, string key)
        {
            return ExtractJsonInt(json, key, 0);
        }

        private static int ExtractJsonInt(string json, string key, int defaultValue)
        {
            var textValue = ExtractJsonString(json, key);
            int parsed;
            if (int.TryParse(textValue, out parsed))
            {
                return parsed;
            }

            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            var searchKey = "\"" + key + "\"";
            var index = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (index < 0)
            {
                return defaultValue;
            }

            index += searchKey.Length;
            while (index < json.Length && IsJsonWhitespaceOrColon(json[index]))
            {
                index++;
            }

            var start = index;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '-'))
            {
                index++;
            }

            if (index <= start)
            {
                return defaultValue;
            }

            return int.TryParse(json.Substring(start, index - start), out parsed)
                ? parsed : defaultValue;
        }

        private static bool IsJsonWhitespaceOrColon(char ch)
        {
            return ch == ' ' || ch == ':' || ch == '\t' || ch == '\r' || ch == '\n';
        }
    }
}
