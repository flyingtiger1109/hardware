using System;
using System.Collections.Generic;
using HZCYKJTHardWare.Proxy.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HZCYKJTHardWare.Proxy.Tests.Infrastructure
{
    [TestClass]
    public class PingLogAggregatorTests
    {
        [TestMethod]
        public void Success_IsSummarizedAfterOneMinuteInsteadOfLoggedPerRequest()
        {
            var now = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
            var messages = new List<string>();
            var aggregator = new PingLogAggregator(messages.Add, () => now);

            aggregator.RecordSuccess(20);
            now = now.AddSeconds(59);
            aggregator.RecordSuccess(40);
            Assert.AreEqual(0, messages.Count);

            now = now.AddSeconds(2);
            aggregator.RecordSuccess(60);

            Assert.AreEqual(1, messages.Count);
            StringAssert.Contains(messages[0], "[健康检查][信息]");
            StringAssert.Contains(messages[0], "请求次数=2");
            StringAssert.Contains(messages[0], "成功次数=2");

            aggregator.Dispose();
        }

        [TestMethod]
        public void Failure_IsImmediate_RateLimited_AndRecoveryIsImmediate()
        {
            var now = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
            var messages = new List<string>();
            var aggregator = new PingLogAggregator(messages.Add, () => now);

            aggregator.RecordFailure("连接失败", false, 100);
            Assert.AreEqual(1, messages.Count);
            StringAssert.Contains(messages[0], "[健康检查][警告]");

            now = now.AddSeconds(1);
            aggregator.RecordFailure("连接失败", false, 120);
            Assert.AreEqual(1, messages.Count);

            now = now.AddSeconds(10);
            aggregator.RecordFailure("连接失败", true, 130);
            Assert.AreEqual(2, messages.Count);
            StringAssert.Contains(messages[1], "[健康检查][错误]");

            now = now.AddMilliseconds(1);
            aggregator.RecordSuccess(30);
            Assert.AreEqual(3, messages.Count);
            StringAssert.Contains(messages[2], "/ping恢复");
            StringAssert.Contains(messages[2], "当前状态=正常");

            aggregator.Dispose();
        }

        [TestMethod]
        public void FailureReasonChange_IsReportedImmediately()
        {
            var now = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
            var messages = new List<string>();
            var aggregator = new PingLogAggregator(messages.Add, () => now);

            aggregator.RecordFailure("超时", false, 100);
            aggregator.RecordFailure("连接被拒绝", false, 100);

            Assert.AreEqual(2, messages.Count);
            StringAssert.Contains(messages[1], "原因=连接被拒绝");

            aggregator.Dispose();
        }
    }
}
