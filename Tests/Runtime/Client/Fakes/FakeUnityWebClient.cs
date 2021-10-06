using BugSplatUnity.Runtime.Client;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace BugSplatUnity.RuntimeTests.Client.Fakes
{
    class FakeUnityWebClient : IUnityWebClient
    {
        public string Url { get; private set; }
        public Dictionary<string, string> FormData { get; private set; }

        private readonly IUnityWebRequest _postReturnValue;

        public FakeUnityWebClient(IUnityWebRequest postReturnValue)
        {
            _postReturnValue = postReturnValue;
        }

        public IReportPostOptions CreateExceptionPostOptions()
        {
            return new FakeExceptionPostOptions();
        }

        public IUnityWebRequest Post(string url, Dictionary<string, string> formData)
        {
            Url = url;
            FormData = formData;

            return _postReturnValue;
        }
    }

    class FakeUnityWebRequest : IUnityWebRequest
    {
        public UnityWebRequest.Result result { get; set; } = UnityWebRequest.Result.Success;
        public string error { get; set; } = string.Empty;
        public long responseCode { get; set; } = 200;
        public IDownloadHandler downloadHandler { get; set; } = new FakeDownloadHandler(string.Empty);

        public IEnumerator SendWebRequest()
        {
            yield return null;
        }
    }

    class FakeDownloadHandler : IDownloadHandler
    {
        public string text { get; }

        public FakeDownloadHandler(string txt)
        {
            text = txt;
        }
    }

    class FakeExceptionPostOptions : IReportPostOptions
    {
        public List<FileInfo> AdditionalAttachments { get; }
        public List<IFormDataParam> AdditionalFormDataParams { get; }
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string User { get; set; }
        public int CrashTypeId { get; set; }
    }
}
