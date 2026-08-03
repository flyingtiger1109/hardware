using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Terminal;

namespace HZCYKJTHardWare.Proxy.UI
{
    internal static class HardwareHealthDpiLayout
    {
        private const float DesignDpi = 192F;

        public static int ScaleFromDesignDpi(Control control, int designPixels, int minimum)
        {
            if (designPixels <= 0)
                return Math.Max(0, minimum);

            var dpi = DesignDpi;
            try
            {
                using (var graphics = control.CreateGraphics())
                    dpi = graphics.DpiX;
            }
            catch
            {
                dpi = DesignDpi;
            }

            var scaled = (int)Math.Round(designPixels * dpi / DesignDpi);
            return Math.Max(minimum, scaled);
        }
    }

    internal enum HardwareVisualState
    {
        Unknown,
        Online,
        Starting,
        Offline,
        Abnormal
    }

    internal sealed class HardwareHealthPresentation
    {
        private HardwareHealthPresentation(
            HardwareVisualState state,
            string statusText,
            string messageText)
        {
            State = state;
            StatusText = statusText;
            MessageText = messageText;
        }

        public HardwareVisualState State { get; }
        public string StatusText { get; }
        public string MessageText { get; }

        public static HardwareHealthPresentation From(DeviceHealth device)
        {
            var status = (device?.Status ?? "unknown").Trim().ToLowerInvariant();
            var message = (device?.Message ?? "").Trim().ToLowerInvariant();

            switch (status)
            {
                case "online":
                    return new HardwareHealthPresentation(
                        HardwareVisualState.Online, "正常", "设备连接稳定");

                case "starting":
                case "initializing":
                case "recovering":
                    return new HardwareHealthPresentation(
                        HardwareVisualState.Starting,
                        "启动中",
                        message == "recovery_local" ? "正在恢复连接…" : "设备正在启动…");

                case "offline":
                case "disconnected":
                    return new HardwareHealthPresentation(
                        HardwareVisualState.Offline,
                        "离线",
                        message == "silence_timeout" ? "设备暂未响应" : "设备连接已断开");

                case "abnormal":
                case "error":
                case "fault":
                    return new HardwareHealthPresentation(
                        HardwareVisualState.Abnormal,
                        "异常",
                        message == "recovery_local_failed"
                            ? "自动恢复失败，请检查设备"
                            : "设备状态异常，请检查设备");

                default:
                    var unknownMessage = "等待终端上报状态";
                    if (message == "not_reported")
                        unknownMessage = "终端未上报该设备";
                    else if (message == "service_stopped")
                        unknownMessage = "服务启动后自动检测";

                    return new HardwareHealthPresentation(
                        HardwareVisualState.Unknown, "待检测", unknownMessage);
            }
        }
    }

    internal sealed class HardwareHealthPanel : Panel
    {
        public const int DefaultHeight = 132;
        private const int HeaderRowHeight = 44;
        private const int HeaderGridGap = 8;
        private const int RightStatusColumnWidth = 760;
        private const int RefreshButtonWidth = 188;
        private const int RefreshButtonHeight = 40;
        private const int StatusButtonGap = 12;
        private const int MinRightStatusColumnWidth = 380;
        private const int MinRefreshButtonWidth = 156;
        private const int MinRefreshButtonMinimumWidth = 132;
        private const int MinStatusButtonGap = 8;

        private sealed class DeviceDescriptor
        {
            public DeviceDescriptor(string id, string code, string name)
            {
                Id = id;
                Code = code;
                Name = name;
            }

            public string Id { get; }
            public string Code { get; }
            public string Name { get; }
        }

        private static readonly DeviceDescriptor[] DeviceDescriptors =
        {
            new DeviceDescriptor("ocr", "OCR", "OCR 文字识别"),
            new DeviceDescriptor("nfc", "IC", "IC 卡"),
            new DeviceDescriptor("fingerprint", "指纹", "指纹设备"),
            new DeviceDescriptor("iris", "虹膜", "虹膜设备"),
            new DeviceDescriptor("face", "人脸", "人脸设备")
        };

        private readonly Dictionary<string, DeviceHealthCard> _cards =
            new Dictionary<string, DeviceHealthCard>(StringComparer.OrdinalIgnoreCase);
        private readonly Label _summaryLabel;
        private readonly Button _refreshButton;
        private readonly ToolTip _toolTip;
        private readonly Timer _animationTimer;
        private bool _alternateAnimationFrame;

        public event EventHandler RefreshRequested;

        public HardwareHealthPanel()
        {
            BackColor = Color.White;
            Height = DefaultHeight;
            Padding = Padding.Empty;

            _toolTip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 350,
                ReshowDelay = 100,
                ShowAlways = true
            };

            var rightStatusColumnWidth = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, RightStatusColumnWidth, MinRightStatusColumnWidth);
            var refreshButtonWidth = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, RefreshButtonWidth, MinRefreshButtonWidth);
            var refreshButtonMinimumWidth = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, 168, MinRefreshButtonMinimumWidth);
            var statusButtonGap = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, StatusButtonGap, MinStatusButtonGap);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = HeaderRowHeight + HeaderGridGap,
                BackColor = Color.White,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightStatusColumnWidth));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderRowHeight));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderGridGap));
            var titleLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 64, 84),
                Text = "硬件健康检测",
                TextAlign = ContentAlignment.MiddleLeft
            };
            _summaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Margin = Padding.Empty
            };
            _refreshButton = new Button
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(refreshButtonMinimumWidth, RefreshButtonHeight),
                Text = "刷新状态",
                Font = new Font("Microsoft YaHei", 8.5F),
                ForeColor = Color.FromArgb(13, 110, 253),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 0, 2),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            _refreshButton.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
            _refreshButton.FlatAppearance.BorderSize = 1;
            _refreshButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
            _refreshButton.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
            _toolTip.SetToolTip(_refreshButton, "刷新状态");

            var statusRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, statusButtonGap));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, refreshButtonWidth));
            statusRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statusRow.Controls.Add(_summaryLabel, 0, 0);
            statusRow.Controls.Add(_refreshButton, 2, 0);

            header.Controls.Add(titleLabel, 0, 0);
            header.Controls.Add(statusRow, 1, 0);

            var grid = new TableLayoutPanel
            {
                ColumnCount = DeviceDescriptors.Length,
                RowCount = 1,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            for (var i = 0; i < DeviceDescriptors.Length; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            for (var i = 0; i < DeviceDescriptors.Length; i++)
            {
                var descriptor = DeviceDescriptors[i];
                var card = new DeviceHealthCard(
                    descriptor.Id, descriptor.Code, descriptor.Name, _toolTip)
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(i == 0 ? 0 : 5, 1,
                        i == DeviceDescriptors.Length - 1 ? 0 : 5, 2)
                };
                _cards.Add(descriptor.Id, card);
                grid.Controls.Add(card, i, 0);
            }

            Controls.Add(grid);
            Controls.Add(header);

            _animationTimer = new Timer { Interval = 650 };
            _animationTimer.Tick += OnAnimationTick;
            ShowServiceStopped();
        }

        public void SetRefreshEnabled(bool enabled)
        {
            _refreshButton.Enabled = enabled;
            _refreshButton.ForeColor = enabled
                ? Color.FromArgb(13, 110, 253)
                : Color.FromArgb(148, 163, 184);
        }

        public void ShowRefreshPending()
        {
            _summaryLabel.Text = "正在刷新状态…";
            _summaryLabel.ForeColor = Color.FromArgb(22, 119, 255);
            _toolTip.SetToolTip(_summaryLabel, "正在读取当前终端硬件状态");
        }

        public void ShowServiceStopped()
        {
            foreach (var descriptor in DeviceDescriptors)
            {
                _cards[descriptor.Id].UpdateHealth(
                    new DeviceHealth
                    {
                        Id = descriptor.Id,
                        Status = "unknown",
                        Message = "service_stopped"
                    }, "", DateTime.MinValue);
            }

            _summaryLabel.Text = "等待服务启动";
            _summaryLabel.ForeColor = Color.FromArgb(100, 116, 139);
            _toolTip.SetToolTip(_summaryLabel, "服务启动后将自动读取终端硬件状态");
            UpdateAnimationState();
        }

        public void UpdateHealth(HealthStatus health)
        {
            if (health == null)
            {
                ShowServiceStopped();
                return;
            }

            var reported = (health.Devices ?? new List<DeviceHealth>())
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id))
                .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            foreach (var descriptor in DeviceDescriptors)
            {
                DeviceHealth device;
                if (!reported.TryGetValue(descriptor.Id, out device))
                {
                    device = new DeviceHealth
                    {
                        Id = descriptor.Id,
                        Status = "unknown",
                        Message = "not_reported"
                    };
                }

                _cards[descriptor.Id].UpdateHealth(
                    device, health.RequestId, health.Timestamp);
            }

            UpdateSummary(health);
            UpdateAnimationState();
        }

        private void UpdateSummary(HealthStatus health)
        {
            if (!string.IsNullOrEmpty(health.ErrorMessage))
            {
                _summaryLabel.Text = "检测失败 · " + health.ErrorMessage;
                _summaryLabel.ForeColor = Color.FromArgb(220, 38, 38);
                _toolTip.SetToolTip(_summaryLabel, BuildSummaryToolTip(health));
                return;
            }

            var states = _cards.Values.Select(c => c.VisualState).ToList();
            var parts = new List<string>();
            AddCount(parts, states, HardwareVisualState.Online, "正常");
            AddCount(parts, states, HardwareVisualState.Starting, "启动中");
            AddCount(parts, states, HardwareVisualState.Offline, "离线");
            AddCount(parts, states, HardwareVisualState.Abnormal, "异常");
            AddCount(parts, states, HardwareVisualState.Unknown, "待检测");

            _summaryLabel.Text = string.Join(" · ", parts);
            if (states.Contains(HardwareVisualState.Abnormal))
                _summaryLabel.ForeColor = Color.FromArgb(220, 38, 38);
            else if (states.Contains(HardwareVisualState.Starting))
                _summaryLabel.ForeColor = Color.FromArgb(22, 119, 255);
            else if (states.Contains(HardwareVisualState.Offline) ||
                     states.Contains(HardwareVisualState.Unknown))
                _summaryLabel.ForeColor = Color.FromArgb(100, 116, 139);
            else
                _summaryLabel.ForeColor = Color.FromArgb(22, 163, 74);

            _toolTip.SetToolTip(_summaryLabel, BuildSummaryToolTip(health));
        }

        private static void AddCount(
            ICollection<string> parts,
            IEnumerable<HardwareVisualState> states,
            HardwareVisualState state,
            string name)
        {
            var count = states.Count(s => s == state);
            if (count > 0)
                parts.Add(count + " " + name);
        }

        private static string BuildSummaryToolTip(HealthStatus health)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(health.RequestId))
                lines.Add("请求编号：" + health.RequestId);
            if (health.Timestamp != DateTime.MinValue)
                lines.Add("检测时间：" + health.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            if (!string.IsNullOrEmpty(health.ErrorMessage))
                lines.Add("检测信息：" + health.ErrorMessage);
            return lines.Count == 0 ? "等待终端状态" : string.Join(Environment.NewLine, lines);
        }

        private void UpdateAnimationState()
        {
            if (_cards.Values.Any(c => c.VisualState == HardwareVisualState.Starting))
            {
                if (!_animationTimer.Enabled)
                    _animationTimer.Start();
            }
            else
            {
                _animationTimer.Stop();
                _alternateAnimationFrame = false;
                foreach (var card in _cards.Values)
                    card.SetAnimationFrame(false);
            }
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            _alternateAnimationFrame = !_alternateAnimationFrame;
            foreach (var card in _cards.Values)
                card.SetAnimationFrame(_alternateAnimationFrame);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Stop();
                _animationTimer?.Dispose();
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class DeviceHealthCard : Panel
    {
        private const int CodeColumnWidth = 76;
        private const int StatusColumnWidth = 118;
        private const int CardHorizontalPadding = 8;
        private const int CardVerticalPadding = 3;
        private const int CodeRightMargin = 10;
        private const int MinCodeColumnWidth = 44;
        private const int MinStatusColumnWidth = 72;
        private const int MinCardHorizontalPadding = 6;
        private const int MinCardVerticalPadding = 3;
        private const int MinCodeRightMargin = 4;

        private readonly string _deviceId;
        private readonly Label _codeLabel;
        private readonly Label _nameLabel;
        private readonly Label _messageLabel;
        private readonly Label _statusLabel;
        private readonly ToolTip _toolTip;
        private Color _borderColor;

        public DeviceHealthCard(
            string deviceId,
            string deviceCode,
            string deviceName,
            ToolTip toolTip)
        {
            _deviceId = deviceId;
            _toolTip = toolTip;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            var codeColumnWidth = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, CodeColumnWidth, MinCodeColumnWidth);
            var statusColumnWidth = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, StatusColumnWidth, MinStatusColumnWidth);
            var horizontalPadding = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, CardHorizontalPadding, MinCardHorizontalPadding);
            var verticalPadding = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, CardVerticalPadding, MinCardVerticalPadding);
            var codeRightMargin = HardwareHealthDpiLayout.ScaleFromDesignDpi(
                this, CodeRightMargin, MinCodeRightMargin);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 2,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(horizontalPadding, verticalPadding,
                    horizontalPadding, verticalPadding),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, codeColumnWidth));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, statusColumnWidth));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            _codeLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Bold),
                Text = deviceCode,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, codeRightMargin, 0),
                BackColor = Color.Transparent
            };
            _nameLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                Text = deviceName,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
            _messageLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 8F),
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };

            layout.Controls.Add(_codeLabel, 0, 0);
            layout.SetRowSpan(_codeLabel, 2);
            layout.Controls.Add(_nameLabel, 1, 0);
            layout.Controls.Add(_messageLabel, 1, 1);
            layout.Controls.Add(_statusLabel, 2, 0);
            layout.SetRowSpan(_statusLabel, 2);
            Controls.Add(layout);
        }

        public HardwareVisualState VisualState { get; private set; }

        public void UpdateHealth(DeviceHealth device, string requestId, DateTime timestamp)
        {
            var presentation = HardwareHealthPresentation.From(device);
            VisualState = presentation.State;
            _messageLabel.Text = presentation.MessageText;
            ApplyTheme(presentation.State);
            SetAnimationFrame(false);

            var detail = new List<string>
            {
                "设备：" + _deviceId,
                "状态：" + (device?.Status ?? "unknown"),
                "界面提示：" + presentation.MessageText
            };
            if (!string.IsNullOrEmpty(device?.Message))
                detail.Add("诊断代码：" + device.Message);
            if (!string.IsNullOrEmpty(requestId))
                detail.Add("请求编号：" + requestId);
            if (timestamp != DateTime.MinValue)
                detail.Add("检测时间：" + timestamp.ToString("yyyy-MM-dd HH:mm:ss"));

            SetToolTipRecursively(this, string.Join(Environment.NewLine, detail));
            Invalidate();
        }

        public void SetAnimationFrame(bool alternate)
        {
            switch (VisualState)
            {
                case HardwareVisualState.Online:
                    _statusLabel.Text = "✓ 正常";
                    break;
                case HardwareVisualState.Starting:
                    _statusLabel.Text = alternate ? "◌ 启动中" : "↻ 启动中";
                    break;
                case HardwareVisualState.Offline:
                    _statusLabel.Text = "— 离线";
                    break;
                case HardwareVisualState.Abnormal:
                    _statusLabel.Text = "! 异常";
                    break;
                default:
                    _statusLabel.Text = "○ 待检测";
                    break;
            }
        }

        private void ApplyTheme(HardwareVisualState state)
        {
            Color accent;
            switch (state)
            {
                case HardwareVisualState.Online:
                    accent = Color.FromArgb(22, 163, 74);
                    BackColor = Color.FromArgb(240, 253, 244);
                    _borderColor = Color.FromArgb(187, 247, 208);
                    break;
                case HardwareVisualState.Starting:
                    accent = Color.FromArgb(22, 119, 255);
                    BackColor = Color.FromArgb(239, 246, 255);
                    _borderColor = Color.FromArgb(191, 219, 254);
                    break;
                case HardwareVisualState.Abnormal:
                    accent = Color.FromArgb(220, 38, 38);
                    BackColor = Color.FromArgb(254, 242, 242);
                    _borderColor = Color.FromArgb(254, 202, 202);
                    break;
                default:
                    accent = Color.FromArgb(100, 116, 139);
                    BackColor = Color.FromArgb(248, 250, 252);
                    _borderColor = Color.FromArgb(203, 213, 225);
                    break;
            }

            _codeLabel.ForeColor = accent;
            _statusLabel.ForeColor = accent;
        }

        private void SetToolTipRecursively(Control control, string text)
        {
            _toolTip.SetToolTip(control, text);
            foreach (Control child in control.Controls)
                SetToolTipRecursively(child, text);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(_borderColor))
            {
                e.Graphics.DrawRectangle(
                    pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            }
        }
    }
}
