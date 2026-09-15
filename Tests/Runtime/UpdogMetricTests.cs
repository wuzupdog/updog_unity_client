using System.Collections.Generic;
using NUnit.Framework;

namespace Updog.Unity.Tests
{
    public class UpdogMetricTests
    {
        [Test]
        public void EncodesTaggedGaugeForTheLocalAgent()
        {
            var metric = UpdogMetric.Create(
                "zone.players",
                350,
                "gauge",
                null,
                new Dictionary<string, string> { ["zone"] = "night:harbor" },
                Config());

            var wire = metric.ToStatsd();

            Assert.That(wire, Does.StartWith("zone.players:350|g|#"));
            Assert.That(wire, Does.Contain("zone:night_harbor"));
            Assert.That(wire, Does.Contain("service:zone-server"));
            Assert.That(wire, Does.Contain("environment:test"));
            Assert.That(wire, Does.Contain("hostname:app-01"));
            Assert.That(wire, Does.Contain("sdk_version:0.3.3"));
        }

        [Test]
        public void EncodesTimerAndUnit()
        {
            var metric = UpdogMetric.Create("zone.tick", 42.5, "timer", "ms", null, Config());

            var wire = metric.ToStatsd();

            Assert.That(wire, Does.StartWith("zone.tick:42.5|ms|#"));
            Assert.That(wire, Does.Contain("unit:ms"));
        }

        private static UpdogConfig Config()
        {
            return new UpdogConfig
            {
                Service = "zone-server",
                Environment = "test",
                Release = "test-release",
                Hostname = "app-01"
            };
        }
    }
}
