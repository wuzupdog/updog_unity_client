using NUnit.Framework;

namespace Updog.Unity.Tests
{
    public class UpdogHostnameTests
    {
        [Test]
        public void UsesConfiguredHostnameForErrorNotices()
        {
            var config = new UpdogConfig { Hostname = "app-01" };

            var notice = UpdogNotice.Create("RuntimeError", "boom", null, null, config);

            Assert.That(notice.hostname, Is.EqualTo("app-01"));
        }

        [Test]
        public void UsesConfiguredHostnameForMetricsWithoutInitialization()
        {
            var config = new UpdogConfig { Hostname = "app-01" };

            var metric = UpdogMetric.Create("jobs.active", 3, "gauge", null, null, config);

            Assert.That(metric.hostname, Is.EqualTo("app-01"));
        }

        [Test]
        public void PreservesConfiguredHostnameWhenApplyingDefaults()
        {
            var config = new UpdogConfig { Hostname = " app-01 " };

            config.ApplyDefaults();

            Assert.That(config.Hostname, Is.EqualTo("app-01"));
        }
    }
}
