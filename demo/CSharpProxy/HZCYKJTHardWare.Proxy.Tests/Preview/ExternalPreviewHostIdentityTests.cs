using System;
using System.Windows.Forms;
using HZCYKJTHardWare.Proxy.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Preview
{
    [TestClass]
    public class ExternalPreviewHostIdentityTests
    {
        [TestMethod]
        public void IsExternalHostCurrent_RejectsPidMismatchAndDestroyedWindow()
        {
            var form = new Form();
            try
            {
                var hwnd = form.Handle;
                Assert.AreNotEqual(IntPtr.Zero, hwnd);
                Assert.IsTrue(PreviewManager.TryGetWindowOwnerIdentity(hwnd,
                    out var processId, out var processStartTimeUtcTicks));

                var session = new PreviewSession
                {
                    TargetHwnd = hwnd,
                    HostHwnd = hwnd,
                    SessionType = PreviewSessionType.External,
                    OwnerProcessId = processId,
                    OwnerProcessStartTimeUtcTicks = processStartTimeUtcTicks
                };

                Assert.IsTrue(PreviewManager.IsExternalHostCurrent(session));
                session.OwnerProcessId = processId + 1;
                Assert.IsFalse(PreviewManager.IsExternalHostCurrent(session));
                session.OwnerProcessId = processId;

                form.Dispose();
                Assert.IsFalse(PreviewManager.IsExternalHostCurrent(session));
            }
            finally
            {
                form.Dispose();
            }
        }
    }
}
