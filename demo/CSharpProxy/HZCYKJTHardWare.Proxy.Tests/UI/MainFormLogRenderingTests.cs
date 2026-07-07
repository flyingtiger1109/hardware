using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy;
using HZCYKJTHardWare.Proxy.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.UI
{
    [TestClass]
    public class MainFormLogRenderingTests
    {
        [TestMethod]
        public void HardwareHealthPanel_IsEmbeddedWithoutOverlappingHeaderOrContent()
        {
            RunInSta(() =>
            {
                using (var form = new MainForm())
                {
                    StopTimer(form, "_uiLogTimer");
                    StopTimer(form, "_monitorTimer");
                    form.CreateControl();
                    form.PerformLayout();

                    var formType = typeof(MainForm);
                    var hardwarePanel = (HardwareHealthPanel)GetField(
                        formType, "_hardwareHealthPanel").GetValue(form);
                    var panelHeader = (Panel)GetField(formType, "panelHeader").GetValue(form);
                    var headerLayout = (TableLayoutPanel)GetField(
                        formType, "headerLayout").GetValue(form);
                    var titleLabel = (Label)GetField(
                        formType, "lblPageTitle").GetValue(form);
                    var panelTop = (Panel)GetField(formType, "panelTop").GetValue(form);
                    var panelPreview = (Panel)GetField(formType, "panelPreview").GetValue(form);
                    var panelLog = (Panel)GetField(formType, "panelLog").GetValue(form);
                    var titleRequiredHeight = titleLabel.GetPreferredSize(
                        new Size(titleLabel.Width, 0)).Height;

                    Assert.AreSame(panelHeader, hardwarePanel.Parent);
                    Assert.AreEqual(DockStyle.Bottom, hardwarePanel.Dock);
                    Assert.AreEqual(5, CountControls<DeviceHealthCard>(hardwarePanel));
                    Assert.IsFalse(titleLabel.AutoEllipsis,
                        "主标题在常规宽度下应完整显示，不应主动启用省略号");
                    Assert.IsTrue(headerLayout.Height >= titleRequiredHeight,
                        "顶部运行信息区高度必须足够显示主标题，避免健康检测面板挤压导致裁切");
                    Assert.IsTrue(headerLayout.Bottom <= hardwarePanel.Top,
                        "顶部运行信息区不得与硬件健康卡片重叠");
                    Assert.AreEqual(panelHeader.Bottom, panelTop.Top,
                        "硬件健康卡片加入后，下方操作区必须随 Header 高度顺延");
                    Assert.AreEqual(
                        panelHeader.Height + panelTop.Height + panelPreview.Height,
                        panelLog.Top,
                        "新增健康区域后应保持原有固定内容总高度");
                }
            });
        }

        [TestMethod]
        public void HardwareHealthPanel_UsesIcCardDisplayName()
        {
            RunInSta(() =>
            {
                using (var panel = new HardwareHealthPanel())
                {
                    panel.CreateControl();
                    var texts = CollectLabels(panel)
                        .Select(l => l.Text)
                        .ToList();

                    Assert.IsTrue(texts.Contains("IC 卡"));
                    Assert.IsFalse(texts.Contains("NFC / IC 卡"));
                }
            });
        }

        [TestMethod]
        public void HardwareHealthPanel_ProvidesManualRefreshButton()
        {
            RunInSta(() =>
            {
                using (var panel = new HardwareHealthPanel())
                {
                    panel.Width = 1480;
                    panel.Height = HardwareHealthPanel.DefaultHeight;
                    panel.CreateControl();
                    panel.PerformLayout();
                    var refreshButtons = CollectControls<Button>(panel).ToList();

                    Assert.AreEqual(1, refreshButtons.Count);
                    Assert.AreEqual("刷新状态", refreshButtons[0].Text);
                    Assert.IsInstanceOfType(refreshButtons[0].Parent, typeof(TableLayoutPanel),
                        "刷新按钮应位于右侧横向布局中，避免被摘要文字遮挡");
                    var refreshTextSize = TextRenderer.MeasureText(
                        refreshButtons[0].Text, refreshButtons[0].Font);
                    Assert.IsTrue(refreshButtons[0].Width >= refreshTextSize.Width + 16,
                        "刷新按钮必须有足够宽度，避免文字强制换行");
                    Assert.IsTrue(refreshButtons[0].Height >= refreshTextSize.Height + 8,
                        "刷新按钮必须有足够高度，避免文字和边框挤压");
                    Assert.IsTrue(refreshButtons[0].Height >= 40,
                        "刷新按钮实际高度不得低于 40px，避免高 DPI 或微软雅黑渲染时底部被裁切");
                    Assert.AreEqual(DockStyle.Fill, refreshButtons[0].Dock,
                        "刷新按钮应填满固定宽度按钮列，确保文本不换行");
                    var statusRow = (TableLayoutPanel)refreshButtons[0].Parent;
                    Assert.AreEqual(1, statusRow.RowCount,
                        "右侧状态区应为单行横向布局");
                    Assert.AreEqual(3, statusRow.ColumnCount,
                        "右侧状态区应由摘要、固定间隔、按钮三列组成");
                    Assert.AreEqual(2, statusRow.GetColumn(refreshButtons[0]));
                    Assert.AreEqual(0, statusRow.GetRow(refreshButtons[0]));
                    var summaryLabel = CollectLabels(panel)
                        .SingleOrDefault(l => l.Text == "等待服务启动");
                    Assert.IsNotNull(summaryLabel, "健康检测状态摘要应独立显示");
                    Assert.AreSame(statusRow, summaryLabel.Parent);
                    Assert.AreEqual(0, statusRow.GetRow(summaryLabel));
                    Assert.AreEqual(0, statusRow.GetColumn(summaryLabel));
                    Assert.AreEqual(ContentAlignment.MiddleRight, summaryLabel.TextAlign);
                    Assert.IsTrue(summaryLabel.AutoEllipsis,
                        "健康检测摘要应保持单行显示，过长时只能省略自身");
                    var contentGrid = panel.Controls
                        .OfType<TableLayoutPanel>()
                        .Single(t => t.Dock == DockStyle.Fill);
                    var buttonBounds = BoundsRelativeTo(panel, refreshButtons[0]);
                    var summaryBounds = BoundsRelativeTo(panel, summaryLabel);
                    var contentGridBounds = BoundsRelativeTo(panel, contentGrid);
                    var statusRowBounds = BoundsRelativeTo(panel, statusRow);
                    var lastCard = contentGrid.Controls
                        .OfType<DeviceHealthCard>()
                        .Single(c => contentGrid.GetColumn(c) == 4);
                    var lastCardBounds = BoundsRelativeTo(panel, lastCard);

                    Assert.AreEqual(12, buttonBounds.Left - summaryBounds.Right,
                        "检测摘要与刷新按钮之间应保留 12px 横向间距");
                    Assert.IsTrue(contentGridBounds.Top - buttonBounds.Bottom >= 20,
                        "Header Row 下方距离设备卡片上边缘应至少保留 20px");
                    Assert.IsTrue(contentGridBounds.Top - summaryBounds.Bottom >= 20,
                        "检测摘要下方距离设备卡片上边缘应至少保留 20px");
                    Assert.AreEqual(lastCardBounds.Right, buttonBounds.Right,
                        "刷新按钮右边界必须与最右侧人脸设备卡片右边框对齐");
                    Assert.AreEqual(lastCardBounds.Right, statusRowBounds.Right,
                        "右侧状态组件整体右边界必须与最右侧人脸设备卡片右边框对齐");

                    panel.SetRefreshEnabled(false);
                    Assert.IsFalse(refreshButtons[0].Enabled);

                    panel.SetRefreshEnabled(true);
                    Assert.IsTrue(refreshButtons[0].Enabled);
                }
            });
        }

        [TestMethod]
        public void HardwareHealthPanel_DeviceCardLabelsHaveSingleLineSpace()
        {
            RunInSta(() =>
            {
                using (var panel = new HardwareHealthPanel())
                {
                    panel.Width = 1480;
                    panel.Height = HardwareHealthPanel.DefaultHeight;
                    panel.CreateControl();
                    panel.PerformLayout();

                    foreach (var card in CollectControls<DeviceHealthCard>(panel))
                    {
                        var codeLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_codeLabel").GetValue(card);
                        var statusLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_statusLabel").GetValue(card);

                        Assert.IsFalse(codeLabel.Text.Contains(Environment.NewLine));
                        Assert.IsTrue(
                            codeLabel.Width >= codeLabel.GetPreferredSize(Size.Empty).Width,
                            "设备短码列必须足够显示单行文本，避免 OCR 被拆成 OC/R");
                        Assert.IsTrue(
                            statusLabel.Width >= statusLabel.GetPreferredSize(Size.Empty).Width,
                            "设备状态列必须足够显示单行文本，避免“待检测”被拆行");
                    }
                }
            });
        }

        [TestMethod]
        public void PreviewControl_UsesDeviceSelectorAndTwoActions()
        {
            RunInSta(() =>
            {
                using (var form = new MainForm())
                {
                    StopTimer(form, "_uiLogTimer");
                    StopTimer(form, "_monitorTimer");
                    form.CreateControl();
                    form.PerformLayout();

                    var formType = typeof(MainForm);
                    var previewControl = (TableLayoutPanel)GetField(
                        formType, "tlpPreviewControl").GetValue(form);
                    var comboBox = CollectControls<ComboBox>(previewControl)
                        .SingleOrDefault();
                    var actionButtons = previewControl.Controls
                        .OfType<Button>()
                        .ToList();

                    Assert.IsNotNull(comboBox, "预览控制应提供设备下拉框");
                    Assert.AreEqual(6, comboBox.Items.Count);
                    Assert.AreEqual(DrawMode.OwnerDrawFixed, comboBox.DrawMode,
                        "预览设备下拉框应自绘，避免选择后显示系统默认蓝色背景");
                    Assert.AreEqual(DockStyle.Top, comboBox.Dock,
                        "预览设备下拉框不应被拉伸填满整行，否则文本可能被裁剪");
                    Assert.IsInstanceOfType(comboBox.Parent, typeof(Panel));
                    var comboHost = (Panel)comboBox.Parent;
                    Assert.AreEqual(BorderStyle.FixedSingle, comboHost.BorderStyle,
                        "预览设备下拉框应有独立边框，避免白底下看起来显示不全");
                    Assert.IsTrue(comboBox.Height >= 32 && comboBox.Height < 44,
                        "预览设备下拉框应保持足够高度且不填满整行");
                    Assert.AreEqual(2, actionButtons.Count,
                        "预览控制应仅保留开始/停止两个操作按钮，避免按钮重叠");
                    Assert.IsFalse(previewControl.Controls.ContainsKey("btnStartCameraPreview"));
                    Assert.IsFalse(previewControl.Controls.ContainsKey("btnStopPlatePreviewRJ3"));
                }
            });
        }

        [TestMethod]
        public void PrependHistoryLines_WithColorChange_DoesNotSplitActiveLine()
        {
            RunInSta(() =>
            {
                using (var form = new MainForm())
                {
                    StopTimer(form, "_uiLogTimer");
                    StopTimer(form, "_monitorTimer");

                    var formType = typeof(MainForm);
                    var appendLogToMemo = GetMethod(formType, "AppendLogToMemo");
                    var prependHistoryLines = GetMethod(formType, "PrependHistoryLines");
                    var memoLog = (RichTextBox)GetField(formType, "memoLog").GetValue(form);

                    const string activeLine =
                        "[2026-07-03 15:52:42.823] DLL 服务监听: 127.0.0.1:8089";
                    appendLogToMemo.Invoke(form, new object[] { activeLine });

                    var historyLines = new List<string>
                    {
                        "[2026-07-03 15:52:42.792] [信息] 配置文件已加载",
                        "[2026-07-03 15:52:42.792] [信息] 应用程序启动中...",
                        "[2026-07-03 15:52:42.822] [警告] No IP found matching subnet prefix: 192.168.20"
                    };
                    prependHistoryLines.Invoke(form, new object[] { historyLines });

                    var actualLines = memoLog.Text
                        .Replace("\r\n", "\n")
                        .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToArray();
                    var expectedLines = historyLines.Concat(new[] { activeLine }).ToArray();

                    CollectionAssert.AreEqual(expectedLines, actualLines,
                        "Expected=" + string.Join(" | ", expectedLines) +
                        "; Actual=" + string.Join(" | ", actualLines));
                }
            });
        }

        private static void RunInSta(Action action)
        {
            Exception capturedException = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "UI 日志测试线程执行超时");

            if (capturedException != null)
                throw new AssertFailedException("UI 日志测试执行失败: " + capturedException);
        }

        private static MethodInfo GetMethod(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "未找到方法: " + name);
            return method;
        }

        private static int CountControls<T>(Control root) where T : Control
        {
            var count = root is T ? 1 : 0;
            foreach (Control child in root.Controls)
                count += CountControls<T>(child);
            return count;
        }

        private static IEnumerable<Label> CollectLabels(Control root)
        {
            if (root is Label label)
                yield return label;

            foreach (Control child in root.Controls)
            {
                foreach (var nested in CollectLabels(child))
                    yield return nested;
            }
        }

        private static IEnumerable<T> CollectControls<T>(Control root) where T : Control
        {
            if (root is T match)
                yield return match;

            foreach (Control child in root.Controls)
            {
                foreach (var nested in CollectControls<T>(child))
                    yield return nested;
            }
        }

        private static Rectangle BoundsRelativeTo(Control root, Control child)
        {
            var location = root.PointToClient(child.Parent.PointToScreen(child.Location));
            return new Rectangle(location, child.Size);
        }

        private static FieldInfo GetField(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "未找到字段: " + name);
            return field;
        }

        private static void StopTimer(MainForm form, string fieldName)
        {
            var timer = GetField(typeof(MainForm), fieldName).GetValue(form) as System.Windows.Forms.Timer;
            timer?.Stop();
        }
    }
}
