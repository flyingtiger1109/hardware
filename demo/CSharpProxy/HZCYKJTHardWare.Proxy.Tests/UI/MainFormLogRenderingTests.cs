using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy;
using HZCYKJTHardWare.Proxy.Terminal;
using HZCYKJTHardWare.Proxy.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.UI
{
    [TestClass]
    public class MainFormLogRenderingTests
    {
        [TestMethod]
        public void ProductVersion_IsEmbeddedAndVisibleInMainWindow()
        {
            var assembly = typeof(MainForm).Assembly;
            Assert.AreEqual(new Version(1, 3, 1, 0), assembly.GetName().Version);
            Assert.AreEqual("1.3.1.0",
                assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version);
            Assert.AreEqual("1.3.1",
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    .InformationalVersion);
            Assert.AreEqual(ProductVersionInfo.DisplayName,
                assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title);
            Assert.AreEqual(ProductVersionInfo.DisplayName,
                assembly.GetCustomAttribute<AssemblyProductAttribute>().Product);

            RunInSta(() =>
            {
                using (var form = new MainForm())
                {
                    StopTimer(form, "_uiLogTimer");
                    StopTimer(form, "_monitorTimer");

                    var titleLabel = (Label)GetField(
                        typeof(MainForm), "lblPageTitle").GetValue(form);
                    var versionLabel = (Label)GetField(
                        typeof(MainForm), "_lblVersion").GetValue(form);
                    var trayIcon = (NotifyIcon)GetField(
                        typeof(MainForm), "_trayIcon").GetValue(form);
                    Assert.AreEqual(ProductVersionInfo.WindowTitle, form.Text);
                    Assert.AreEqual(ProductVersionInfo.DisplayName, titleLabel.Text);
                    Assert.IsFalse(titleLabel.Text.Contains(ProductVersionInfo.DisplayVersion));
                    Assert.AreEqual(ProductVersionInfo.DisplayVersion, versionLabel.Text);
                    Assert.AreEqual(ProductVersionInfo.DisplayName, form.Text);
                    Assert.AreSame(titleLabel.Parent, versionLabel.Parent);
                    Assert.IsTrue(versionLabel.Font.Size < titleLabel.Font.Size);
                    Assert.AreEqual(ProductVersionInfo.DisplayName, trayIcon.Text);
                }
            });
        }

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
                    var mainContentSplit = (SplitContainer)GetField(
                        formType, "_mainContentSplit").GetValue(form);
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
                    Assert.AreSame(form, mainContentSplit.Parent);
                    Assert.AreEqual(DockStyle.Fill, mainContentSplit.Dock);
                    Assert.AreEqual(Orientation.Horizontal, mainContentSplit.Orientation);
                    Assert.IsFalse(mainContentSplit.IsSplitterFixed,
                        "预览区与日志区之间的分隔条必须允许用户拖拽");
                    Assert.AreSame(mainContentSplit.Panel1, panelPreview.Parent);
                    Assert.AreSame(mainContentSplit.Panel2, panelLog.Parent);
                    Assert.AreEqual(DockStyle.Fill, panelPreview.Dock);
                    Assert.AreEqual(DockStyle.Fill, panelLog.Dock);
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
                    Assert.AreEqual(Color.FromArgb(78, 149, 217),
                        refreshButtons[0].ForeColor,
                        "刷新按钮文字应与主操作按钮使用相同参考蓝");
                    Assert.AreEqual(Color.White, refreshButtons[0].BackColor);
                    Assert.AreEqual(Color.FromArgb(190, 211, 233),
                        refreshButtons[0].FlatAppearance.BorderColor);
                    Assert.AreEqual(Color.FromArgb(238, 245, 252),
                        refreshButtons[0].FlatAppearance.MouseOverBackColor);
                    Assert.AreEqual(Color.FromArgb(220, 233, 247),
                        refreshButtons[0].FlatAppearance.MouseDownBackColor);
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

                    Assert.IsTrue(buttonBounds.Left - summaryBounds.Right >= 8,
                        "检测摘要与刷新按钮之间应保留可读横向间距");
                    Assert.IsTrue(contentGridBounds.Top - buttonBounds.Bottom >= 8,
                        "紧凑 Header Row 下方仍应保留至少 8px 间距");
                    Assert.IsTrue(contentGridBounds.Top - summaryBounds.Bottom >= 8,
                        "检测摘要下方仍应保留至少 8px 间距");
                    Assert.AreEqual(lastCardBounds.Right, buttonBounds.Right,
                        "刷新按钮右边界必须与最右侧人脸设备卡片右边框对齐");
                    Assert.AreEqual(lastCardBounds.Right, statusRowBounds.Right,
                        "右侧状态组件整体右边界必须与最右侧人脸设备卡片右边框对齐");

                    panel.SetRefreshEnabled(false);
                    Assert.IsFalse(refreshButtons[0].Enabled);
                    Assert.AreEqual(Color.FromArgb(148, 163, 184),
                        refreshButtons[0].ForeColor);

                    panel.SetRefreshEnabled(true);
                    Assert.IsTrue(refreshButtons[0].Enabled);
                    Assert.AreEqual(Color.FromArgb(78, 149, 217),
                        refreshButtons[0].ForeColor);

                    panel.ShowRefreshPending();
                    Assert.AreEqual("正在刷新状态…", summaryLabel.Text);
                    Assert.AreEqual(Color.FromArgb(78, 149, 217),
                        summaryLabel.ForeColor,
                        "刷新中的信息提示应与按钮使用相同参考蓝");
                }
            });
        }

        [TestMethod]
        public void HardwareHealthPanel_UsesConnectionFailureCopyInSummary()
        {
            RunInSta(() =>
            {
                using (var panel = new HardwareHealthPanel())
                {
                    panel.CreateControl();
                    panel.UpdateHealth(new HealthStatus
                    {
                        ErrorMessage = "终端连接失败或超时"
                    });

                    var summaryLabel = CollectLabels(panel)
                        .Single(l => l.Text.StartsWith("检测失败 · "));
                    Assert.AreEqual(
                        "检测失败 · 终端连接失败或超时",
                        summaryLabel.Text);
                    Assert.IsFalse(summaryLabel.Text.Contains("终端不可达"));
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
                        var nameLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_nameLabel").GetValue(card);
                        var messageLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_messageLabel").GetValue(card);
                        var statusLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_statusLabel").GetValue(card);

                        Assert.IsFalse(codeLabel.Text.Contains(Environment.NewLine));
                        Assert.AreEqual(ContentAlignment.MiddleLeft, nameLabel.TextAlign);
                        Assert.AreEqual(ContentAlignment.MiddleLeft, messageLabel.TextAlign);
                        Assert.IsTrue(
                            nameLabel.Height >= nameLabel.Font.Height + 2,
                            "Device name row must leave enough vertical space for the full glyph height.");
                        Assert.IsTrue(
                            messageLabel.Height >= messageLabel.Font.Height + 2,
                            "Device message row must leave enough vertical space for the full glyph height.");
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
        public void HardwareHealthPanel_DeviceCardsFitAtOneHundredPercentWindowWidth()
        {
            RunInSta(() =>
            {
                using (var panel = new HardwareHealthPanel())
                {
                    panel.Width = 1180;
                    panel.Height = HardwareHealthPanel.DefaultHeight;
                    panel.CreateControl();
                    panel.PerformLayout();

                    foreach (var card in CollectControls<DeviceHealthCard>(panel))
                    {
                        card.PerformLayout();
                        var codeLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_codeLabel").GetValue(card);
                        var messageLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_messageLabel").GetValue(card);
                        var statusLabel = (Label)GetField(
                            typeof(DeviceHealthCard), "_statusLabel").GetValue(card);

                        Assert.IsTrue(
                            codeLabel.Width >= codeLabel.GetPreferredSize(Size.Empty).Width,
                            "100% 缩放宽度下设备短码应完整可读");
                        Assert.IsTrue(
                            statusLabel.Width >= statusLabel.GetPreferredSize(Size.Empty).Width,
                            "100% 缩放宽度下设备状态应完整可读");
                        Assert.IsTrue(
                            messageLabel.Width >= 88,
                            "100% 缩放宽度下设备说明列不应被固定列宽挤压到只剩省略号");
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

                    var serviceButton = (Button)GetField(
                        formType, "btnStopServer").GetValue(form);
                    var previewStartButton = (Button)GetField(
                        formType, "_btnStartSelectedPreview").GetValue(form);
                    var previewHint = (Label)GetField(
                        formType, "_lblPreviewControlHint").GetValue(form);
                    var serviceLayout = (TableLayoutPanel)GetField(
                        formType, "tlpService").GetValue(form);
                    var operationLayout = (TableLayoutPanel)GetField(
                        formType, "tlpOperation").GetValue(form);
                    var previewTitle = (Label)GetField(
                        formType, "lblCardPreviewControl").GetValue(form);

                    Assert.AreEqual(serviceButton.BackColor, previewStartButton.BackColor,
                        "三组操作按钮的默认底色应一致");
                    Assert.AreEqual(serviceButton.ForeColor, previewStartButton.ForeColor,
                        "三组操作按钮的默认文字颜色应一致");
                    Assert.AreEqual(Color.FromArgb(78, 149, 217),
                        serviceButton.ForeColor,
                        "普通按钮文字应使用参考图提取的蓝色");
                    Assert.AreEqual(Color.White, serviceLayout.BackColor);
                    Assert.AreEqual(Color.White, operationLayout.BackColor);
                    Assert.AreEqual(Color.White, previewControl.BackColor);
                    Assert.AreEqual(Color.White, comboBox.BackColor);
                    Assert.AreEqual(Color.White, previewHint.BackColor,
                        "预览控制提示文字底色应与其他卡片统一为白色");
                    Assert.AreEqual(Color.White, previewTitle.BackColor,
                        "预览控制标题底色应与其他卡片统一为白色");

                    var setPersistentStyle = GetMethod(
                        formType, "SetPersistentButtonStyle");
                    setPersistentStyle.Invoke(form, new object[] { serviceButton, true });
                    setPersistentStyle.Invoke(form, new object[] { previewStartButton, true });

                    Assert.AreEqual(serviceButton.BackColor, previewStartButton.BackColor,
                        "服务状态和预览状态按钮应使用相同的选中底色");
                    Assert.AreEqual(serviceButton.ForeColor, previewStartButton.ForeColor,
                        "服务状态和预览状态按钮应使用相同的选中文字颜色");
                    Assert.AreNotEqual(Color.FromArgb(13, 110, 253),
                        serviceButton.BackColor,
                        "选中状态不得继续使用原高饱和纯蓝底色");
                    Assert.AreEqual(Color.FromArgb(78, 149, 217),
                        serviceButton.BackColor,
                        "选中状态应使用参考图提取的蓝色底色");
                    Assert.AreEqual(Color.White, serviceButton.ForeColor,
                        "参考蓝底色应使用白色选中文字");
                }
            });
        }

        [TestMethod]
        public void FinalStartupLayout_ReflowsStableClientAreaAndResizeKeepsUserSplitter()
        {
            RunInSta(() =>
            {
                using (var form = new MainForm())
                {
                    StopTimer(form, "_uiLogTimer");
                    StopTimer(form, "_monitorTimer");
                    form.CreateControl();
                    form.Size = new Size(
                        Math.Max(form.MinimumSize.Width, 1600),
                        Math.Max(form.MinimumSize.Height, 1000));
                    form.PerformLayout();

                    var formType = typeof(MainForm);
                    GetMethod(formType, "ApplyFinalStartupLayout").Invoke(form, null);

                    var panelHeader = (Panel)GetField(
                        formType, "panelHeader").GetValue(form);
                    var panelTop = (Panel)GetField(
                        formType, "panelTop").GetValue(form);
                    var headerLayout = (TableLayoutPanel)GetField(
                        formType, "headerLayout").GetValue(form);
                    var hardwareHealthPanel = (HardwareHealthPanel)GetField(
                        formType, "_hardwareHealthPanel").GetValue(form);
                    var mainContentSplit = (SplitContainer)GetField(
                        formType, "_mainContentSplit").GetValue(form);
                    var previewLayout = (TableLayoutPanel)GetField(
                        formType, "previewLayout").GetValue(form);
                    var memoLog = (RichTextBox)GetField(
                        formType, "memoLog").GetValue(form);

                    var expectedHeaderHeight = headerLayout.MinimumSize.Height +
                        panelHeader.Padding.Vertical + hardwareHealthPanel.Height;
                    Assert.AreEqual(expectedHeaderHeight, panelHeader.Height,
                        "顶部总高度只能包含一次健康检测区高度");

                    var firstLayoutHeight = panelHeader.Height;
                    GetMethod(formType, "ApplyFinalStartupLayout").Invoke(form, null);
                    Assert.AreEqual(firstLayoutHeight, panelHeader.Height,
                        "重复执行最终启动布局不应继续增加顶部高度");

                    Assert.AreEqual(panelHeader.Bottom, panelTop.Top,
                        "最终启动布局后顶部信息区与操作区应连续排列");
                    Assert.IsTrue(mainContentSplit.ClientSize.Height >
                        mainContentSplit.Panel1MinSize + mainContentSplit.Panel2MinSize,
                        "最终启动布局应为预览区和日志区保留有效客户区");
                    Assert.IsTrue(previewLayout.Width > 0 && previewLayout.Height > 0,
                        "最终启动布局后预览网格应立即获得有效尺寸");

                    var preferredLogHeight = (int)GetMethod(
                        formType, "CalculatePreferredStartupLogPanelHeight")
                        .Invoke(form, null);
                    var maximumLogHeight = mainContentSplit.ClientSize.Height -
                        mainContentSplit.Panel1MinSize - mainContentSplit.SplitterWidth;
                    var expectedLogHeight = Math.Max(mainContentSplit.Panel2MinSize,
                        Math.Min(maximumLogHeight, preferredLogHeight));
                    Assert.AreEqual(expectedLogHeight, mainContentSplit.Panel2.Height,
                        "首次启动应优先使用日志首选高度，空间不足时必须为预览区保留最小高度");
                    if (maximumLogHeight >= preferredLogHeight)
                    {
                        Assert.IsTrue(memoLog.Height >= 180,
                            "空间充足时应至少保留 180px 日志正文可视区域");
                    }
                    else
                    {
                        Assert.AreEqual(mainContentSplit.Panel1MinSize,
                            mainContentSplit.Panel1.Height,
                            "空间不足时只能压缩日志区，不能继续挤压预览区");
                    }
                    Assert.IsTrue(mainContentSplit.Panel1.Height >=
                        mainContentSplit.Panel1MinSize,
                        "增加日志区高度时不能挤压预览区到最小高度以下");

                    var maximumDistance = mainContentSplit.ClientSize.Height -
                        mainContentSplit.Panel2MinSize - mainContentSplit.SplitterWidth;
                    if (maximumDistance > mainContentSplit.Panel1MinSize)
                    {
                        mainContentSplit.SplitterDistance =
                            mainContentSplit.Panel1MinSize +
                            (maximumDistance - mainContentSplit.Panel1MinSize) / 2;
                        var userDistance = mainContentSplit.SplitterDistance;

                        GetMethod(formType, "RefreshResponsiveWindowLayout")
                            .Invoke(form, new object[] { false });

                        Assert.AreEqual(userDistance, mainContentSplit.SplitterDistance,
                            "普通 Resize 刷新不能重置用户拖动后的日志分隔条位置");
                    }
                }
            });
        }

        [TestMethod]
        public void PreviewArea_UsesResponsiveSixteenByNineGrid()
        {
            RunInSta(() =>
            {
                using (var form = new MainForm())
                {
                    StopTimer(form, "_uiLogTimer");
                    StopTimer(form, "_monitorTimer");
                    form.CreateControl();

                    var formType = typeof(MainForm);
                    var gridHost = (Panel)GetField(
                        formType, "_previewGridHost").GetValue(form);
                    var previewLayout = (TableLayoutPanel)GetField(
                        formType, "previewLayout").GetValue(form);
                    var panelCamera = (Panel)GetField(
                        formType, "panelCamera").GetValue(form);
                    var panelPlateRJ3 = (Panel)GetField(
                        formType, "panelPlateRJ3").GetValue(form);
                    var toggleLog = (Button)GetField(
                        formType, "_btnToggleLog").GetValue(form);

                    gridHost.Dock = DockStyle.None;
                    gridHost.Size = new Size(1800, 700);
                    var layoutMethod = formType.GetMethod(
                        "LayoutResponsivePreviewGrid",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(layoutMethod);
                    layoutMethod.Invoke(form, null);
                    previewLayout.PerformLayout();

                    Assert.AreSame(gridHost, previewLayout.Parent);
                    Assert.AreEqual(5, previewLayout.ColumnCount);
                    Assert.AreEqual(3, previewLayout.RowCount);
                    Assert.IsTrue(panelCamera.Width > 0 && panelCamera.Height > 0);
                    Assert.AreEqual(
                        16.0 / 9.0,
                        (double)panelCamera.Width / panelCamera.Height,
                        0.02,
                        "每个预览卡片必须保持 16:9");
                    Assert.IsTrue(panelPlateRJ3.Left > panelCamera.Left);
                    Assert.IsTrue(panelPlateRJ3.Top > panelCamera.Top);
                    Assert.AreEqual("折叠日志", toggleLog.Text);
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
                        "[2026-07-03 15:52:42.822] [警告] 未找到匹配子网前缀的IP地址: 192.168.20"
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
