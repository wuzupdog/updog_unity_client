using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Updog.Unity
{
    [Serializable]
    public sealed class UpdogNotice
    {
        public string error_class;
        public string message;
        public List<UpdogStackFrame> stacktrace;
        public List<Dictionary<string, object>> breadcrumbs;
        public Dictionary<string, object> request;
        public Dictionary<string, object> context;
        public string environment;
        public string hostname;
        public string fingerprint;

        public static UpdogNotice Create(
            string errorClass,
            string message,
            List<UpdogStackFrame> stackFrames,
            IDictionary<string, object> context,
            UpdogConfig config,
            string fingerprint = null)
        {
            stackFrames = stackFrames ?? new List<UpdogStackFrame>();

            return new UpdogNotice
            {
                error_class = errorClass,
                message = message,
                stacktrace = stackFrames,
                breadcrumbs = new List<Dictionary<string, object>>(),
                request = new Dictionary<string, object>(),
                context = CopyContext(context),
                environment = config.Environment,
                hostname = Hostname(),
                fingerprint = string.IsNullOrWhiteSpace(fingerprint)
                    ? UpdogFingerprint.Generate(errorClass, stackFrames)
                    : fingerprint
            };
        }

        public static string ErrorClassFromUnityLog(LogType type, string condition)
        {
            if (type == LogType.Assert)
            {
                return "Unity.Assert";
            }

            if (type != LogType.Exception || string.IsNullOrWhiteSpace(condition))
            {
                return "Unity.Error";
            }

            var firstLine = condition.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0];
            var colonIndex = firstLine.IndexOf(':');
            return colonIndex > 0 ? firstLine.Substring(0, colonIndex).Trim() : firstLine.Trim();
        }

        private static string Hostname()
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

        private static Dictionary<string, object> CopyContext(IDictionary<string, object> context)
        {
            var copy = new Dictionary<string, object>();

            if (context == null)
            {
                return copy;
            }

            foreach (var pair in context)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
