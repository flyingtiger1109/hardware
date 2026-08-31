using System.Collections.Generic;
using HZCYKJTHardWare.Proxy.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace HZCYKJTHardWare.Proxy.Tests.Infrastructure
{
    [TestClass]
    public class IcCardCallbackConfigurationTests
    {
        [TestMethod]
        public void MissingOrNullValue_DefaultsToEnabled()
        {
            var warnings = new List<string>();

            Assert.IsTrue(AppConfig.ResolveEnableIcCardCallback(
                null, warnings.Add));
            Assert.IsTrue(AppConfig.ResolveEnableIcCardCallback(
                JValue.CreateNull(), warnings.Add));
            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public void BooleanValue_IsUsedAsConfigured()
        {
            Assert.IsTrue(AppConfig.ResolveEnableIcCardCallback(
                new JValue(true), null));
            Assert.IsFalse(AppConfig.ResolveEnableIcCardCallback(
                new JValue(false), null));
        }

        [TestMethod]
        public void BooleanString_IsAcceptedAndTrimmed()
        {
            Assert.IsTrue(AppConfig.ResolveEnableIcCardCallback(
                new JValue(" TRUE "), null));
            Assert.IsFalse(AppConfig.ResolveEnableIcCardCallback(
                new JValue("false"), null));
        }

        [TestMethod]
        public void EmptyOrInvalidValue_FallsBackToEnabledWithWarning()
        {
            var warnings = new List<string>();

            Assert.IsTrue(AppConfig.ResolveEnableIcCardCallback(
                new JValue(""), warnings.Add));
            Assert.IsTrue(AppConfig.ResolveEnableIcCardCallback(
                new JValue("not-a-bool"), warnings.Add));
            Assert.IsTrue(AppConfig.ResolveEnableIcCardCallback(
                new JValue(0), warnings.Add));
            Assert.AreEqual(3, warnings.Count);
        }
    }
}
