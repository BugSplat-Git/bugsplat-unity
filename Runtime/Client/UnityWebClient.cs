using System.Collections.Generic;
using UnityEngine.Networking;
#if UNITY_2020_1_OR_NEWER
using static UnityEngine.Networking.UnityWebRequest;
#endif

namespace BugSplatUnity.Runtime.Client
{
    internal interface IDownloadHandler
    {
        string Text { get; }
    }

    internal interface IUnityWebRequest
    {
        UnityWebRequestAsyncOperation SendWebRequest();
        bool Success { get; }
        string Error { get; }
        long ResponseCode { get; }
        IDownloadHandler DownloadHandler { get; }
    }

    internal interface IUnityWebClient
    {
        IUnityWebRequest Post(string url, Dictionary<string, string> formData);
    }

    internal class UnityWebClient : IUnityWebClient
    {
        public IUnityWebRequest Post(string url, Dictionary<string, string> formData)
        {
            var request = UnityWebRequest.Post(url, formData);
            return new WrappedUnityWebRequest(request);
        }
    }

    internal class WrappedUnityWebRequest: IUnityWebRequest
    {
#if UNITY_2020_1_OR_NEWER
        public bool Success => _request.result == UnityWebRequest.Result.Success;
#else
        public bool Success => !_request.isHttpError && !_request.isNetworkError;
#endif
        public string Error => _request.error;
        public long ResponseCode => _request.responseCode;
        public IDownloadHandler DownloadHandler => new WrappedDownloadHandler(_request.downloadHandler);

        private readonly UnityWebRequest _request;

        public WrappedUnityWebRequest(UnityWebRequest request)
        {
            _request = request;
        }

        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            return _request.SendWebRequest();
        }
    }

    internal class WrappedDownloadHandler : IDownloadHandler
    {
        public string Text => _downloadHandler.text;

        private readonly DownloadHandler _downloadHandler;

        public WrappedDownloadHandler(DownloadHandler downloadHandler)
        {
            _downloadHandler = downloadHandler;
        }
    }
}
