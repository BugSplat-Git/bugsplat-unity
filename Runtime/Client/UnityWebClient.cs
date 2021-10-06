using BugSplatDotNetStandard;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace BugSplatUnity.Runtime.Client
{
    internal class UnityWebClient : IUnityWebClient
    {
        public IExceptionPostOptions CreateExceptionPostOptions()
        {
            return (IExceptionPostOptions)new ExceptionPostOptions();
        }

        public IUnityWebRequest Post(string url, Dictionary<string, string> formData)
        {
            return (IUnityWebRequest)UnityWebRequest.Post(url, formData);
        }
    }
}
