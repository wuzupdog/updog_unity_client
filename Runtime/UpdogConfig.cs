using System;
using System.Collections.Generic;
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
        public bool CaptureUnityLogs { get; set; } = true;
        public int MaxQueueSize { get; set; } = 50;
        public float FlushIntervalSeconds { get; set; } = 30f;
        public int TimeoutSeconds { get; set; } = 5;
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
            Service = FirstNonEmpty(Service, Application.productName, "unity");
            Release = FirstNonEmpty(Release, Application.version);
            MaxQueueSize = MaxQueueSize > 0 ? MaxQueueSize : 50;
            FlushIntervalSeconds = FlushIntervalSeconds > 0 ? FlushIntervalSeconds : 30f;
            TimeoutSeconds = TimeoutSeconds > 0 ? TimeoutSeconds : 5;
        }

        internal bool IsEnabled()
        {
            return !IsTruthy(GetEnvironmentVariable("UPDOG_DISABLED")) &&
                   !IsFalsey(GetEnvironmentVariable("UPDOG_ENABLED")) &&
                   !string.IsNullOrWhiteSpace(ApiKey);
        }

        internal string NormalizedEndpoint()
        {
            return FirstNonEmpty(Endpoint, "https://wuzupdog.com").Trim().TrimEnd('/');
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
    }
}
