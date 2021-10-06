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
        IEnumerator SendWebRequest();
        Result result { get; }
        string error { get; }
        long responseCode { get; }
        IDownloadHandler downloadHandler { get; }
    }

    internal interface IUnityWebClient
    {
        IReportPostOptions CreateExceptionPostOptions();
        IUnityWebRequest Post(string url, Dictionary<string, string> formData);
    }

    internal class UnityWebClient : IUnityWebClient
    {
        public IReportPostOptions CreateExceptionPostOptions()
        {
            return new ReportPostOptions();
        }

        public IUnityWebRequest Post(string url, Dictionary<string, string> formData)
        {
            return (IUnityWebRequest)UnityWebRequest.Post(url, formData);
        }
    }
}
