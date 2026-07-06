using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.UI
{
    [TestClass]
    public class MainFormLogRenderingTests
    {
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
