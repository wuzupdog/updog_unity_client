using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Updog.Unity
{
    public sealed class UpdogErrorReporter : MonoBehaviour
    {
        private sealed class QueueEntry
        {
            public UpdogNotice Notice;
            public int Bytes;
        }

        private sealed class MetricQueueEntry
        {
            public UpdogMetric Metric;
            public int Bytes;
        }

        private readonly List<QueueEntry> queue = new List<QueueEntry>();
        private readonly List<MetricQueueEntry> metricQueue = new List<MetricQueueEntry>();
        private readonly Dictionary<string, long> dropped = new Dictionary<string, long>();
        private UpdogConfig config;
        private UpdogHttpClient httpClient;
        private UpdogStatsdClient statsdClient;
        private float lastFlushTime;
        private float lastMetricFlushTime;
        private bool isFlushing;
        private bool isSubscribed;
        private int queueBytes;
        private int metricQueueBytes;
        private int inFlightCount;
        private long queuedCount;
        private long sentCount;
        private long retryCount;

        public bool IsInitialized { get; private set; }

        public bool Initialize(UpdogConfig newConfig)
        {
            config = newConfig ?? UpdogConfig.FromEnvironment();
            config.ApplyDefaults();

            if (!config.IsEnabled())
            {
                StopCapture();
                statsdClient?.Dispose();
                statsdClient = null;
                httpClient = null;
                Debug.LogWarning("[Updog] Disabled. Configure UPDOG_API_KEY for errors or UPDOG_STATSD_ENDPOINT for metrics.");
                return false;
            }

            httpClient = config.CanSendErrors() ? new UpdogHttpClient(config) : null;
            statsdClient?.Dispose();
            statsdClient = null;
            if (config.CanSendMetrics())
            {
                try
                {
                    statsdClient = new UpdogStatsdClient(config.StatsdEndpoint);
                }
                catch
                {
                    statsdClient = null;
                }
            }
            if (httpClient == null && statsdClient == null)
            {
                StopCapture();
                Debug.LogWarning("[Updog] Disabled because no telemetry transport could be initialized.");
                return false;
            }
            lastFlushTime = Time.realtimeSinceStartup;
            lastMetricFlushTime = lastFlushTime;
            IsInitialized = true;

            if (config.CanSendErrors() && config.CaptureUnityLogs && !isSubscribed)
            {
                Application.logMessageReceived += OnLogMessageReceived;
                isSubscribed = true;
            }
            else if ((!config.CanSendErrors() || !config.CaptureUnityLogs) && isSubscribed)
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                isSubscribed = false;
            }

            Debug.Log($"[Updog] Initialized - Endpoint: {config.NormalizedEndpoint()}, Environment: {config.Environment}, StatsD: {config.StatsdEndpoint ?? "disabled"}");
            return true;
        }

        public void BeginShutdown(float timeoutSeconds)
        {
            StopCapture();
            StartCoroutine(FlushAndDestroy(Mathf.Max(0f, timeoutSeconds)));
        }

        public IEnumerator Flush(float timeoutSeconds = 5f)
        {
            var deadline = Time.realtimeSinceStartup + Mathf.Max(0f, timeoutSeconds);

            if (!isFlushing && queue.Count > 0)
            {
                StartCoroutine(FlushQueue());
            }

            while ((isFlushing || queue.Count > 0) && Time.realtimeSinceStartup < deadline)
            {
                if (!isFlushing && queue.Count > 0)
                {
                    StartCoroutine(FlushQueue());
                }

                yield return null;
            }

            FlushMetrics();
        }

        public UpdogDeliveryStats DeliveryStats()
        {
            return new UpdogDeliveryStats
            {
                Queued = queuedCount,
                Sent = sentCount,
                Retried = retryCount,
                Dropped = new Dictionary<string, long>(dropped),
                QueueRecords = queue.Count + metricQueue.Count,
                QueueBytes = queueBytes + metricQueueBytes,
                InFlight = inFlightCount
            };
        }

        public void NotifyError(
            string errorClass,
            string message,
            string stackTrace = null,
            IDictionary<string, object> context = null,
            string fingerprint = null)
        {
            if (!IsInitialized || !config.CanSendErrors())
            {
                return;
            }

            errorClass = string.IsNullOrWhiteSpace(errorClass) ? "Unity.Error" : errorClass;
            message = string.IsNullOrWhiteSpace(message) ? errorClass : message;

            var notice = UpdogNotice.Create(
                errorClass,
                message,
                UpdogStacktraceParser.Parse(stackTrace),
                MergeContext(context),
                config,
                fingerprint);

            Enqueue(notice);
        }

        public void ReportMetric(
            string name,
            double value,
            string type,
            string unit,
            IDictionary<string, string> tags)
        {
            if (!IsInitialized || statsdClient == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            EnqueueMetric(UpdogMetric.Create(name, value, type, unit, tags, config));
        }

        private void Update()
        {
            if (IsInitialized && !isFlushing && queue.Count > 0 &&
                Time.realtimeSinceStartup - lastFlushTime >= config.FlushIntervalSeconds)
            {
                StartCoroutine(FlushQueue());
            }

            if (IsInitialized && metricQueue.Count > 0 &&
                Time.realtimeSinceStartup - lastMetricFlushTime >= config.FlushIntervalSeconds)
            {
                FlushMetrics();
            }
        }

        private void OnDestroy()
        {
            StopCapture();
            statsdClient?.Dispose();
            statsdClient = null;
            DropQueued("shutdown_timeout");
        }

        private void OnApplicationQuit()
        {
            StopCapture();
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

            var context = new Dictionary<string, object>
            {
                ["log_type"] = type.ToString(),
                ["source"] = "unity_log"
            };

            var errorClass = UpdogNotice.ErrorClassFromUnityLog(type, condition);
            NotifyError(errorClass, string.IsNullOrWhiteSpace(condition) ? errorClass : condition, stackTrace, context);
        }

        private void Enqueue(UpdogNotice notice)
        {
            int bytes;
            try
            {
                bytes = Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(notice));
            }
            catch
            {
                IncrementDrop("encoding_error");
                return;
            }

            if (bytes > config.MaxRecordBytes)
            {
                IncrementDrop("record_too_large");
                return;
            }

            while (!HasRoom(bytes) && metricQueue.Count > 0)
            {
                var removed = metricQueue[0];
                metricQueue.RemoveAt(0);
                metricQueueBytes -= removed.Bytes;
                IncrementDrop("evicted_for_priority");
            }

            if (!HasRoom(bytes))
            {
                IncrementDrop("queue_full");
                return;
            }

            queue.Add(new QueueEntry { Notice = notice, Bytes = bytes });
            queueBytes += bytes;
            queuedCount++;

            if (!isFlushing && (queue.Count >= config.MaxBatchSize || queueBytes >= config.MaxBatchBytes))
            {
                StartCoroutine(FlushQueue());
            }
        }

        private void EnqueueMetric(UpdogMetric metric)
        {
            int bytes;
            try
            {
                bytes = Encoding.UTF8.GetByteCount(metric.ToStatsd());
            }
            catch
            {
                IncrementDrop("encoding_error");
                return;
            }

            if (bytes > config.MaxRecordBytes)
            {
                IncrementDrop("record_too_large");
                return;
            }

            if (!HasRoom(bytes))
            {
                IncrementDrop("queue_full");
                return;
            }

            metricQueue.Add(new MetricQueueEntry { Metric = metric, Bytes = bytes });
            metricQueueBytes += bytes;
            queuedCount++;

            if (metricQueue.Count >= config.MaxBatchSize || metricQueueBytes >= config.MaxBatchBytes)
            {
                lastMetricFlushTime = Time.realtimeSinceStartup - config.FlushIntervalSeconds;
            }
        }

        private bool HasRoom(int bytes)
        {
            return queue.Count + metricQueue.Count < config.MaxQueueSize &&
                   queueBytes + metricQueueBytes + bytes <= config.MaxQueueBytes;
        }

        private void FlushMetrics()
        {
            if (statsdClient == null || metricQueue.Count == 0)
            {
                return;
            }

            var pending = new List<MetricQueueEntry>(metricQueue);
            metricQueue.Clear();
            metricQueueBytes = 0;
            inFlightCount += pending.Count;

            foreach (var entry in pending)
            {
                if (statsdClient.Send(entry.Metric))
                {
                    sentCount++;
                }
                else
                {
                    IncrementDrop("statsd_send_failed");
                }
            }

            inFlightCount -= pending.Count;
            lastMetricFlushTime = Time.realtimeSinceStartup;
        }

        private IEnumerator FlushQueue()
        {
            if (isFlushing)
            {
                yield break;
            }

            isFlushing = true;

            while (queue.Count > 0)
            {
                var batch = TakeBatch();
                inFlightCount = batch.Count;
                yield return SendBatch(batch);
                inFlightCount = 0;
            }

            isFlushing = false;
            lastFlushTime = Time.realtimeSinceStartup;
        }

        private List<UpdogNotice> TakeBatch()
        {
            var batch = new List<UpdogNotice>();
            var bytes = 14; // UTF-8 bytes in {"notices":[]}

            while (queue.Count > 0 && batch.Count < config.MaxBatchSize)
            {
                var entry = queue[0];
                var separatorBytes = batch.Count == 0 ? 0 : 1;
                if (batch.Count > 0 && bytes + separatorBytes + entry.Bytes > config.MaxBatchBytes)
                {
                    break;
                }

                queue.RemoveAt(0);
                queueBytes -= entry.Bytes;
                bytes += separatorBytes + entry.Bytes;
                batch.Add(entry.Notice);
            }

            return batch;
        }

        private IEnumerator SendBatch(List<UpdogNotice> notices)
        {
            var operation = httpClient.PostNotices(notices);
            yield return operation;
            retryCount += operation.RetryCount;

            if (operation.Success)
            {
                sentCount += notices.Count;
                yield break;
            }

            if (operation.PayloadTooLarge && notices.Count > 1)
            {
                var midpoint = notices.Count / 2;
                yield return SendBatch(notices.GetRange(0, midpoint));
                yield return SendBatch(notices.GetRange(midpoint, notices.Count - midpoint));
                yield break;
            }

            IncrementDrop(operation.PayloadTooLarge ? "record_too_large" : operation.DropReason ?? "delivery_failed", notices.Count);
        }

        private IEnumerator FlushAndDestroy(float timeoutSeconds)
        {
            yield return Flush(timeoutSeconds);
            FlushMetrics();
            DropQueued("shutdown_timeout");
            Destroy(gameObject);
        }

        private void StopCapture()
        {
            if (isSubscribed)
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                isSubscribed = false;
            }

            IsInitialized = false;
        }

        private void DropQueued(string reason)
        {
            if (queue.Count > 0)
            {
                IncrementDrop(reason, queue.Count);
                queue.Clear();
                queueBytes = 0;
            }

            if (metricQueue.Count > 0)
            {
                IncrementDrop(reason, metricQueue.Count);
                metricQueue.Clear();
                metricQueueBytes = 0;
            }
        }

        private void IncrementDrop(string reason, int count = 1)
        {
            dropped[reason] = dropped.TryGetValue(reason, out var current) ? current + count : count;
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
            return merged;
        }
    }
}
