using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using static UnityEngine.Networking.UnityWebRequest;

namespace BugSplatUnity.Runtime.Client
{
    internal interface IDownloadHandler
    {
        string text { get; }
    }

    internal interface IUnityWebRequest
    {
        UnityWebRequestAsyncOperation SendWebRequest();
        Result result { get; }
        string error { get; }
        long responseCode { get; }
        IDownloadHandler downloadHandler { get; }
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
        public Result result => _request.result;
        public string error => _request.error;
        public long responseCode => _request.responseCode;
        public IDownloadHandler downloadHandler => new WrappedDownloadHandler(_request.downloadHandler);

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
        public string text => _downloadHandler.text;

        private readonly DownloadHandler _downloadHandler;

        public WrappedDownloadHandler(DownloadHandler downloadHandler)
        {
            _downloadHandler = downloadHandler;
        }
    }
}
