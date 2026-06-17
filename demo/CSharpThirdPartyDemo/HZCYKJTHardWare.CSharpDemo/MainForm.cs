using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        private bool _initialized;
        private readonly HzcyHardwareNative.EventCallbackDelegate _eventCallback;

        public MainForm()
        {
            InitializeComponent();
            ApplyWindowIcon();
            _eventCallback = OnNativeEvent;
            Log("就绪，请点击[初始化]开始。");
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

        private void btnCameraPreview_Click(object sender, EventArgs e)
        {
            RunPreviewDllCallAsync("开始视频预览", panelCamera.Handle, hwnd => HzcyHardwareNative.HZCYKJTHardWare_StartCameraPreview(hwnd));
        }

        private void btnStopCameraPreview_Click(object sender, EventArgs e)
        {
            ExecuteDllCall("停止视频预览", () => LogRet("停止视频预览", HzcyHardwareNative.HZCYKJTHardWare_StopCameraPreview()));
        }

        private void btnFingerprintPreview_Click(object sender, EventArgs e)
        {
            RunPreviewDllCallAsync("开始指纹预览", panelFingerprint.Handle, hwnd => HzcyHardwareNative.HZCYKJTHardWare_StartFingerprintPreview(hwnd));
        }

        private void btnStopFingerprintPreview_Click(object sender, EventArgs e)
        {
            ExecuteDllCall("停止指纹预览", () => LogRet("停止指纹预览", HzcyHardwareNative.HZCYKJTHardWare_StopFingerprintPreview()));
        }

        private void btnFaceCapture_Click(object sender, EventArgs e)
        {
            ExecuteWithSaveDir("人脸抓拍", ptr => HzcyHardwareNative.HZCYKJTHardWare_CaptureCameraImage(ptr));
        }

        private void btnFingerprintCapture_Click(object sender, EventArgs e)
        {
            ExecuteWithSaveDir("指纹抓拍", ptr => HzcyHardwareNative.HZCYKJTHardWare_CaptureFingerprintImage(ptr));
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

        private void RunPreviewDllCallAsync(string name, IntPtr targetHwnd, Func<IntPtr, int> action)
        {
            Log(name + " 已提交到后台线程：hwnd=" + targetHwnd.ToInt64());

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
            var message = ExtractJsonString(json, "message");
            var mrz = ExtractJsonString(json, "mrz");
            var icNumber = ExtractJsonString(json, "ic_number");
            var savePath = ExtractJsonString(json, "save_path");

            Log(string.Format("[事件] 类型={0} 状态={1} 资源={2}", eventType, FormatResultCode(status), FormatResourceType(resourceType)));
            if (!string.IsNullOrEmpty(mrz))
            {
                Log("  MRZ: " + mrz);
            }
            if (!string.IsNullOrEmpty(icNumber))
            {
                Log("  IC卡号: " + icNumber);
            }
            if (!string.IsNullOrEmpty(savePath))
            {
                Log("  保存路径: " + savePath);
            }
            if (!string.IsNullOrEmpty(message))
            {
                Log("  消息: " + message);
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

        private void LogRet(string name, int ret)
        {
            Log(string.Format("{0}：{1}", name, FormatResultCode(ret)));
        }

        private static string FormatResultCode(int code)
        {
            if (code == 1)
            {
                return "成功(1)";
            }
            if (code == 0)
            {
                return "失败(0)";
            }
            return "返回码(" + code + ")";
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
            var textValue = ExtractJsonString(json, key);
            int parsed;
            if (int.TryParse(textValue, out parsed))
            {
                return parsed;
            }

            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return 0;
            }

            var searchKey = "\"" + key + "\"";
            var index = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (index < 0)
            {
                return 0;
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
                return 0;
            }

            return int.TryParse(json.Substring(start, index - start), out parsed) ? parsed : 0;
        }

        private static bool IsJsonWhitespaceOrColon(char ch)
        {
            return ch == ' ' || ch == ':' || ch == '\t' || ch == '\r' || ch == '\n';
        }
    }
}
