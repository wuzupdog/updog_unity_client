using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Updog.Unity
{
    public sealed class UpdogErrorReporter : MonoBehaviour
    {
        private readonly List<UpdogNotice> queue = new List<UpdogNotice>();
        private UpdogConfig config;
        private UpdogHttpClient httpClient;
        private float lastFlushTime;
        private bool isFlushing;
        private bool isSubscribed;

        public bool IsInitialized { get; private set; }

        public bool Initialize(UpdogConfig newConfig)
        {
            config = newConfig ?? UpdogConfig.FromEnvironment();
            config.ApplyDefaults();

            if (!config.IsEnabled())
            {
                Shutdown();
                Debug.LogWarning("[Updog] Disabled. Set UPDOG_API_KEY to send errors to Updog.");
                return false;
            }

            httpClient = new UpdogHttpClient(config);
            lastFlushTime = Time.realtimeSinceStartup;
            IsInitialized = true;

            if (config.CaptureUnityLogs && !isSubscribed)
            {
                Application.logMessageReceived += OnLogMessageReceived;
                isSubscribed = true;
            }
            else if (!config.CaptureUnityLogs && isSubscribed)
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                isSubscribed = false;
            }

            Debug.Log($"[Updog] Initialized - Endpoint: {config.NormalizedEndpoint()}, Environment: {config.Environment}");
            return true;
        }

        public void Shutdown()
        {
            if (isSubscribed)
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                isSubscribed = false;
            }

            IsInitialized = false;
            queue.Clear();
        }

        public void NotifyError(
            string errorClass,
            string message,
            string stackTrace = null,
            IDictionary<string, object> context = null,
            string fingerprint = null)
        {
            if (!IsInitialized)
            {
                return;
            }

            errorClass = string.IsNullOrWhiteSpace(errorClass) ? "Unity.Error" : errorClass;
            message = string.IsNullOrWhiteSpace(message) ? errorClass : message;

            var frames = UpdogStacktraceParser.Parse(stackTrace);
            var notice = UpdogNotice.Create(
                errorClass,
                message,
                frames,
                MergeContext(context),
                config,
                fingerprint);

            Enqueue(notice);
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (!isFlushing && Time.realtimeSinceStartup - lastFlushTime >= config.FlushIntervalSeconds)
            {
                StartCoroutine(Flush());
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            condition = condition ?? "";

            if (condition.Contains("[Updog]"))
            {
                return;
            }

            var errorClass = UpdogNotice.ErrorClassFromUnityLog(type, condition);
            var context = new Dictionary<string, object>
            {
                ["log_type"] = type.ToString(),
                ["source"] = "unity_log"
            };

            NotifyError(errorClass, string.IsNullOrWhiteSpace(condition) ? errorClass : condition, stackTrace, context);
        }

        private void Enqueue(UpdogNotice notice)
        {
            queue.Add(notice);

            if (queue.Count > config.MaxQueueSize)
            {
                queue.RemoveRange(0, queue.Count - config.MaxQueueSize);
            }

            if (!isFlushing && queue.Count >= config.MaxQueueSize)
            {
                StartCoroutine(Flush());
            }
        }

        private IEnumerator Flush()
        {
            if (isFlushing || queue.Count == 0)
            {
                yield break;
            }

            isFlushing = true;
            var notices = new List<UpdogNotice>(queue);
            queue.Clear();

            var failed = new List<UpdogNotice>();
            foreach (var notice in notices)
            {
                var operation = httpClient.PostNotice(notice);
                yield return operation;

                if (!operation.Success)
                {
                    failed.Add(notice);
                }
            }

            if (failed.Count > 0)
            {
                var keepCount = Math.Min(config.MaxQueueSize - queue.Count, failed.Count);
                if (keepCount > 0)
                {
                    queue.InsertRange(0, failed.GetRange(0, keepCount));
                }
            }

            isFlushing = false;
            lastFlushTime = Time.realtimeSinceStartup;
        }

        private IDictionary<string, object> MergeContext(IDictionary<string, object> context)
        {
            var merged = new Dictionary<string, object>();

            if (config.ContextProvider != null)
            {
                try
                {
                    var provided = config.ContextProvider();
                    if (provided != null)
                    {
                        foreach (var pair in provided)
                        {
                            merged[pair.Key] = pair.Value;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Updog] ContextProvider failed: {e.Message}");
                }
            }

            if (context != null)
            {
                foreach (var pair in context)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            merged["unity_version"] = Application.unityVersion;
            merged["platform"] = Application.platform.ToString();
            merged["is_editor"] = Application.isEditor;

            if (!string.IsNullOrWhiteSpace(config.Service))
            {
                merged["service"] = config.Service;
            }

            if (!string.IsNullOrWhiteSpace(config.Release))
            {
                merged["release"] = config.Release;
            }

            return merged;
        }
    }
}
