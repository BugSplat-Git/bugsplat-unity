using BugSplatUnity.Runtime.Client;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.TestTools;

public class WebGLExceptionClientTest
{
    [UnityTest]
    public IEnumerator Post_ShouldCallPostWithUrlAndFormData()
    {
        var database = "database";
        var application = "application";
        var version = "version";
        var stackTrace = "BugSplat!";
        var fakeUnityWebRequest = new FakeUnityWebRequest();
        var fakeUnityWebClient = new FakeUnityWebClient(fakeUnityWebRequest);
        var options = new FakeExceptionPostOptions();
        options.Description = "description";
        options.Email = "fred@bugsplat.com";
        options.Key = "key";
        options.User = "fred";

        var sut = new WebGLExceptionClient(database, application, version);
        sut.UnityWebClient = fakeUnityWebClient;
        yield return sut.Post(stackTrace, options);

        StringAssert.AreEqualIgnoringCase($"https://{database}.bugsplat.com/post/dotnetstandard/", fakeUnityWebClient.Url);
        StringAssert.AreEqualIgnoringCase(database, fakeUnityWebClient.FormData["database"]);
        StringAssert.AreEqualIgnoringCase(application, fakeUnityWebClient.FormData["appName"]);
        StringAssert.AreEqualIgnoringCase(version, fakeUnityWebClient.FormData["appVersion"]);
        StringAssert.AreEqualIgnoringCase(options.Description, fakeUnityWebClient.FormData["description"]);
        StringAssert.AreEqualIgnoringCase(options.Email, fakeUnityWebClient.FormData["email"]);
        StringAssert.AreEqualIgnoringCase(options.Key, fakeUnityWebClient.FormData["appKey"]);
        StringAssert.AreEqualIgnoringCase(options.User, fakeUnityWebClient.FormData["user"]);
    }

    class FakeUnityWebClient : IUnityWebClient
    {
        public string Url { get; private set; }
        public Dictionary<string, string> FormData { get; private set; }

        private readonly IUnityWebRequest _postReturnValue;

        public FakeUnityWebClient(IUnityWebRequest postReturnValue)
        {
            _postReturnValue = postReturnValue;
        }

        public IExceptionPostOptions CreateExceptionPostOptions()
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

    class FakeDownloadHandler: IDownloadHandler
    {
        public string text { get; }

        public FakeDownloadHandler(string txt)
        {
            text = txt;
        }
    }

    class FakeExceptionPostOptions : IExceptionPostOptions
    {
        public List<FileInfo> AdditionalAttachments { get; }
        public List<IFormDataParam> AdditionalFormDataParams { get; }
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string User { get; set; }
        public int ExceptionType { get; set; }
    }
}
