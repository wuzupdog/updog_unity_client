using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace Updog.Unity
{
    public static class Updog
    {
        private const string ReporterObjectName = "UpdogErrorReporter";
        private static UpdogErrorReporter reporter;

        public static bool IsInitialized => reporter != null && reporter.IsInitialized;

        public static bool Initialize(UpdogConfig config = null)
        {
            config = config ?? UpdogConfig.FromEnvironment();

            if (reporter == null)
            {
                var gameObject = new GameObject(ReporterObjectName);
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                reporter = gameObject.AddComponent<UpdogErrorReporter>();
            }

            return reporter.Initialize(config);
        }

        public static void Shutdown(float timeoutSeconds = 5f)
        {
            if (reporter == null)
            {
                return;
            }

            reporter.BeginShutdown(timeoutSeconds);
            reporter = null;
        }

        public static IEnumerator Flush(float timeoutSeconds = 5f)
        {
            if (reporter != null)
            {
                yield return reporter.Flush(timeoutSeconds);
            }
        }

        public static UpdogDeliveryStats DeliveryStats()
        {
            return reporter != null ? reporter.DeliveryStats() : new UpdogDeliveryStats
            {
                Dropped = new Dictionary<string, long>()
            };
        }

        public static void Notify(Exception exception, IDictionary<string, object> context = null)
        {
            if (exception == null)
            {
                return;
            }

            NotifyError(
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message,
                exception.StackTrace,
                context);
        }

        public static void NotifyError(
            string errorClass,
            string message,
            string stackTrace = null,
            IDictionary<string, object> context = null,
            string fingerprint = null)
        {
            if (reporter == null)
            {
                return;
            }

            reporter.NotifyError(errorClass, message, stackTrace, context, fingerprint);
        }

        public static void ReportMetric(
            string name,
            double value,
            string type = "gauge",
            string unit = null,
            IDictionary<string, string> tags = null)
        {
            if (reporter == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            reporter.ReportMetric(name, value, type, unit, tags);
        }
    }
}
