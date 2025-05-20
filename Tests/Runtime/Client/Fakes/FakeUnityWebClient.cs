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
        public bool Success { get; set; } = true;
        public string Error { get; set; } = string.Empty;
        public long ResponseCode { get; set; } = 200;
        public IDownloadHandler DownloadHandler { get; set; } = new FakeDownloadHandler(string.Empty);

        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            return new UnityWebRequestAsyncOperation();
        }
    }

    class FakeDownloadHandler : IDownloadHandler
    {
        public string Text { get; }

        public FakeDownloadHandler(string text)
        {
            Text = text;
        }
    }

    class FakeExceptionPostOptions : IReportPostOptions
    {
        public List<FileInfo> AdditionalAttachments { get; }
        public List<FormDataParam> AdditionalFormDataParams { get; }
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string Notes { get; set; }
        public string User { get; set; }
        public int CrashTypeId { get; set; }

        public Dictionary<string, string> AdditionalAttributes => new Dictionary<string,string>();
    }
}
