using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Updog.Unity
{
    internal sealed class UpdogHttpClient
    {
        private const string NoticesPath = "/api/v1/notices/bulk";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly UpdogConfig config;

        public UpdogHttpClient(UpdogConfig config)
        {
            this.config = config;
        }

        public PostBatchOperation PostNotices(IList<UpdogNotice> notices)
        {
            var envelope = new Dictionary<string, object> { ["notices"] = notices };
            return new PostBatchOperation(config, JsonConvert.SerializeObject(envelope, JsonSettings));
        }

        internal sealed class PostBatchOperation : IEnumerator
        {
            private readonly UpdogConfig config;
            private readonly byte[] body;
            private readonly string requestId = "req_" + Guid.NewGuid().ToString("N");
            private UnityWebRequest request;
            private UnityWebRequestAsyncOperation operation;
            private int attempts;
            private float retryAt;

            public bool Success { get; private set; }
            public bool PayloadTooLarge { get; private set; }
            public int RetryCount { get; private set; }
            public string DropReason { get; private set; }
            public object Current => null;

            public PostBatchOperation(UpdogConfig config, string json)
            {
                this.config = config;
                body = Encoding.UTF8.GetBytes(json);
            }

            public bool MoveNext()
            {
                if (retryAt > 0f && Time.realtimeSinceStartup < retryAt)
                {
                    return true;
                }

                if (operation == null)
                {
                    StartRequest();
                    return true;
                }

                if (!operation.isDone)
                {
                    return true;
                }

                var status = request.responseCode;
                Success = request.result == UnityWebRequest.Result.Success && status >= 200 && status <= 299;
                PayloadTooLarge = status == 413;

                if (Success || PayloadTooLarge || !IsRetryable(status, request.result) || attempts > config.MaxRetries)
                {
                    if (!Success && !PayloadTooLarge)
                    {
                        DropReason = IsRetryable(status, request.result) ? "retries_exhausted" : "permanent_http_error";
                        Debug.LogWarning($"[Updog] Notice batch dropped after HTTP {status}: {request.error}");
                    }

                    request.Dispose();
                    return false;
                }

                var delay = RetryAfterSeconds(request) ?? FullJitterSeconds(attempts);
                RetryCount++;
                request.Dispose();
                request = null;
                operation = null;
                retryAt = Time.realtimeSinceStartup + delay;
                return true;
            }

            public void Reset()
            {
            }

            private void StartRequest()
            {
                attempts++;
                retryAt = 0f;
                request = new UnityWebRequest(config.NormalizedEndpoint() + NoticesPath, "POST")
                {
                    timeout = config.TimeoutSeconds,
                    uploadHandler = new UploadHandlerRaw(body),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-API-Key", config.ApiKey);
                request.SetRequestHeader("X-Updog-Request-ID", requestId);
                operation = request.SendWebRequest();
            }

            private static bool IsRetryable(long status, UnityWebRequest.Result result)
            {
                return result == UnityWebRequest.Result.ConnectionError || status == 408 || status == 429 || status >= 500;
            }

            private static float? RetryAfterSeconds(UnityWebRequest request)
            {
                var value = request.GetResponseHeader("Retry-After");
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (float.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
                {
                    return Mathf.Clamp(seconds, 0f, 30f);
                }

                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                {
                    return Mathf.Clamp((float)(date - DateTimeOffset.UtcNow).TotalSeconds, 0f, 30f);
                }

                return null;
            }

            private static float FullJitterSeconds(int attempts)
            {
                var ceiling = Mathf.Min(0.25f * Mathf.Pow(2f, attempts - 1), 30f);
                return UnityEngine.Random.Range(0f, ceiling);
            }
        }
    }
}
