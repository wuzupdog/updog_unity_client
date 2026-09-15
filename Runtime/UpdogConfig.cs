using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Updog.Unity
{
    public class UpdogConfig
    {
        public string ApiKey { get; set; }
        public string Endpoint { get; set; } = "https://wuzupdog.com";
        public string Environment { get; set; }
        public string Service { get; set; }
        public string Release { get; set; }
        public string Hostname { get; set; }
        public string StatsdEndpoint { get; set; }
        public bool CaptureUnityLogs { get; set; } = true;
        public int MaxQueueSize { get; set; } = 2048;
        public int MaxQueueBytes { get; set; } = 8 * 1024 * 1024;
        public int MaxRecordBytes { get; set; } = 64 * 1024;
        public int MaxBatchSize { get; set; } = 512;
        public int MaxBatchBytes { get; set; } = 512 * 1024;
        public float FlushIntervalSeconds { get; set; } = 5f;
        public int TimeoutSeconds { get; set; } = 5;
        public int MaxRetries { get; set; } = 3;
        public Func<IDictionary<string, object>> ContextProvider { get; set; }

        public static UpdogConfig FromEnvironment()
        {
            var config = new UpdogConfig();
            config.ApplyDefaults();
            return config;
        }

        internal void ApplyDefaults()
        {
            ApiKey = FirstNonEmpty(ApiKey, GetEnvironmentVariable("UPDOG_API_KEY"));
            Endpoint = FirstNonEmpty(Endpoint, GetEnvironmentVariable("UPDOG_ENDPOINT"), "https://wuzupdog.com");
            Environment = FirstNonEmpty(Environment, GetEnvironmentVariable("UPDOG_ENVIRONMENT"), Application.isEditor ? "development" : "production");
            Service = FirstNonEmpty(Service, GetEnvironmentVariable("UPDOG_SERVICE"), Application.productName, "unity");
            Release = FirstNonEmpty(Release, GetEnvironmentVariable("UPDOG_RELEASE"), Application.version);
            Hostname = ResolveHostname();
            StatsdEndpoint = FirstNonEmpty(StatsdEndpoint, GetEnvironmentVariable("UPDOG_STATSD_ENDPOINT"));
            MaxQueueSize = MaxQueueSize > 0 ? MaxQueueSize : 2048;
            MaxQueueBytes = MaxQueueBytes > 0 ? MaxQueueBytes : 8 * 1024 * 1024;
            MaxRecordBytes = MaxRecordBytes > 0 ? MaxRecordBytes : 64 * 1024;
            MaxBatchSize = MaxBatchSize > 0 ? MaxBatchSize : 512;
            MaxBatchBytes = MaxBatchBytes > 0 ? MaxBatchBytes : 512 * 1024;
            FlushIntervalSeconds = FlushIntervalSeconds > 0 ? FlushIntervalSeconds : 5f;
            TimeoutSeconds = TimeoutSeconds > 0 ? TimeoutSeconds : 5;
            MaxRetries = MaxRetries >= 0 ? MaxRetries : 3;
        }

        internal bool IsEnabled()
        {
            return !IsTruthy(GetEnvironmentVariable("UPDOG_DISABLED")) &&
                   !IsFalsey(GetEnvironmentVariable("UPDOG_ENABLED")) &&
                   (CanSendErrors() || CanSendMetrics());
        }

        internal bool CanSendErrors() => !string.IsNullOrWhiteSpace(ApiKey);

        internal bool CanSendMetrics() => !string.IsNullOrWhiteSpace(StatsdEndpoint);

        internal string NormalizedEndpoint()
        {
            return FirstNonEmpty(Endpoint, "https://wuzupdog.com").Trim().TrimEnd('/');
        }

        internal string ResolveHostname()
        {
            return FirstNonEmpty(Hostname, GetEnvironmentVariable("UPDOG_HOSTNAME"), DetectHostname());
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsFalsey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "0":
                case "false":
                case "no":
                case "off":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetEnvironmentVariable(string name)
        {
            try
            {
                return System.Environment.GetEnvironmentVariable(name);
            }
            catch
            {
                return null;
            }
        }

        private static string DetectHostname()
        {
            try
            {
                return Dns.GetHostName();
            }
            catch
            {
                return SystemInfo.deviceName ?? "";
            }
        }
    }
}
