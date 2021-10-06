using BugSplatDotNetStandard;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace BugSplatUnity.Runtime.Client
{
    internal class UnityWebClient : IUnityWebClient
    {
        public IReportPostOptions CreateExceptionPostOptions()
        {
            return (IReportPostOptions)new ReportPostOptions();
        }

        public IUnityWebRequest Post(string url, Dictionary<string, string> formData)
        {
            return (IUnityWebRequest)UnityWebRequest.Post(url, formData);
        }
    }
}
