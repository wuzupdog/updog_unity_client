using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace Updog.Unity
{
    [Serializable]
    public sealed class UpdogMetric
    {
        public string name;
        public double value;
        public string type;
        public string unit;
        public Dictionary<string, string> tags;
        public string service;
        public string environment;
        public string release;
        public string hostname;
        public string sdk_name;
        public string sdk_version;

        internal static UpdogMetric Create(
            string metricName,
            double metricValue,
            string metricType,
            string metricUnit,
            IDictionary<string, string> metricTags,
            UpdogConfig config)
        {
            return new UpdogMetric
            {
                name = metricName,
                value = metricValue,
                type = string.IsNullOrWhiteSpace(metricType) ? "gauge" : metricType,
                unit = metricUnit,
                tags = metricTags == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(metricTags),
                service = config.Service,
                environment = config.Environment,
                release = config.Release,
                hostname = Hostname(),
                sdk_name = "updog_unity_client",
                sdk_version = "0.3.0"
            };
        }

        internal string ToStatsd()
        {
            var wireType = type == "counter" ? "c" : type == "timer" ? "ms" : "g";
            var allTags = new Dictionary<string, string>(tags ?? new Dictionary<string, string>())
            {
                ["service"] = service ?? "",
                ["environment"] = environment ?? "",
                ["release"] = release ?? "",
                ["hostname"] = hostname ?? "",
                ["sdk_name"] = sdk_name ?? "",
                ["sdk_version"] = sdk_version ?? "",
                ["unit"] = unit ?? ""
            };

            var builder = new StringBuilder();
            builder.Append(Sanitize(name))
                .Append(':')
                .Append(value.ToString("R", CultureInfo.InvariantCulture))
                .Append('|')
                .Append(wireType);

            var encodedTags = allTags
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .Select(pair => Sanitize(pair.Key) + ":" + Sanitize(pair.Value));
            var joinedTags = string.Join(",", encodedTags);
            if (!string.IsNullOrEmpty(joinedTags))
            {
                builder.Append("|#").Append(joinedTags);
            }

            return builder.ToString();
        }

        private static string Hostname()
        {
            try
            {
                return Dns.GetHostName();
            }
            catch
            {
                return "";
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return value.Replace('|', '_').Replace(',', '_').Replace(':', '_').Replace('\n', '_').Replace('\r', '_');
        }
    }
}
