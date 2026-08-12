using System;
using System.Linq;
using System.Reflection;
using HZCYKJTHardWare.Proxy.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class PreviewRestartInfoTests
    {
        [TestMethod]
        public void FromSession_CopiesOnlyRestartParameters()
        {
            var session = new PreviewSession
            {
                Player = new FakePreviewController(),
                TargetHwnd = new IntPtr(1234),
                ResourceType = PreviewResourceType.Fingerprint,
                SessionType = PreviewSessionType.External,
                ExplicitPreviewUrl = "rtsp://terminal/fingerprint",
                TerminalBound = true,
                DirectRenderTarget = true
            };

            var restartInfo = PreviewRestartInfo.FromSession(session);

            Assert.AreEqual(session.TargetHwnd, restartInfo.TargetHwnd);
            Assert.AreEqual(session.ResourceType, restartInfo.ResourceType);
            Assert.AreEqual(session.SessionType, restartInfo.SessionType);
            Assert.AreEqual(session.ExplicitPreviewUrl, restartInfo.ExplicitPreviewUrl);
            Assert.AreEqual(session.TerminalBound, restartInfo.TerminalBound);
            Assert.AreEqual(session.DirectRenderTarget, restartInfo.DirectRenderTarget);
        }

        [TestMethod]
        public void RestartInfo_DoesNotRetainPlayerOrSession()
        {
            var retainedMembers = typeof(PreviewRestartInfo)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(member =>
                {
                    var field = member as FieldInfo;
                    if (field != null)
                        return typeof(IPreviewController).IsAssignableFrom(field.FieldType) ||
                               typeof(PreviewSession).IsAssignableFrom(field.FieldType);

                    var property = member as PropertyInfo;
                    return property != null &&
                           (typeof(IPreviewController).IsAssignableFrom(property.PropertyType) ||
                            typeof(PreviewSession).IsAssignableFrom(property.PropertyType));
                })
                .Select(member => member.Name)
                .ToArray();

            CollectionAssert.AreEqual(new string[0], retainedMembers);
        }

        [TestMethod]
        public void ShouldStopForTerminalSwitch_OnlyStopsTerminalBoundSessions()
        {
            var terminalPreview = new PreviewSession { TerminalBound = true };
            var platePreview = new PreviewSession { TerminalBound = false };

            Assert.IsTrue(PreviewManager.ShouldStopForTerminalSwitch(terminalPreview));
            Assert.IsFalse(PreviewManager.ShouldStopForTerminalSwitch(platePreview));
            Assert.IsFalse(PreviewManager.ShouldStopForTerminalSwitch(null));
        }

        private sealed class FakePreviewController : IPreviewController
        {
            public bool IsRunning => true;

            public System.Threading.Tasks.Task DisposeAsync(int timeoutMs)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }
    }
}
