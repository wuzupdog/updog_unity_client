using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Updog.Unity
{
    internal sealed class UpdogHttpClient
    {
        private const string NoticesPath = "/api/v1/notices";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly UpdogConfig config;

        public UpdogHttpClient(UpdogConfig config)
        {
            this.config = config;
        }

        public PostNoticeOperation PostNotice(UpdogNotice notice)
        {
            return new PostNoticeOperation(config, notice);
        }

        public sealed class PostNoticeOperation : IEnumerator
        {
            private readonly UnityWebRequest request;
            private UnityWebRequestAsyncOperation operation;

            public bool Success { get; private set; }

            public PostNoticeOperation(UpdogConfig config, UpdogNotice notice)
            {
                var json = JsonConvert.SerializeObject(notice, JsonSettings);
                request = new UnityWebRequest(config.NormalizedEndpoint() + NoticesPath, "POST")
                {
                    timeout = config.TimeoutSeconds,
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", config.ApiKey);
            }

            public object Current => null;

            public bool MoveNext()
            {
                if (operation == null)
                {
                    operation = request.SendWebRequest();
                    return true;
                }

                if (!operation.isDone)
                {
                    return true;
                }

                Success = request.result == UnityWebRequest.Result.Success &&
                          request.responseCode >= 200 &&
                          request.responseCode <= 299;

                if (!Success)
                {
                    Debug.LogWarning(
                        $"[Updog] Notice POST returned {(long)request.responseCode}: {request.error} {request.downloadHandler?.text}");
                }

                request.Dispose();
                return false;
            }

            public void Reset()
            {
            }
        }
    }
}
