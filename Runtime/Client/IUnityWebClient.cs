using System.Collections;
using System.Collections.Generic;
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
        IExceptionPostOptions CreateExceptionPostOptions();
        IUnityWebRequest Post(string url, Dictionary<string, string> formData);
    }
}
