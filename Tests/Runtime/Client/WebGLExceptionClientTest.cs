using BugSplatUnity.Runtime.Client;
using BugSplatUnity.RuntimeTests.Client.Fakes;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Client
{
    public class WebGLExceptionClientTests
    {
        [UnityTest]
        public IEnumerator Post_WithStackTrace_ShouldCallPostWithUrlAndFormData()
        {
            var database = "database";
            var application = "application";
            var version = "version";
            var stackTrace = "BugSplat!";
            var exceptionType = 12;
            var fakeUnityWebRequest = new FakeUnityWebRequest();
            var fakeUnityWebClient = new FakeUnityWebClient(fakeUnityWebRequest);
            var options = new FakeExceptionPostOptions();
            options.Description = "description";
            options.Email = "fred@bugsplat.com";
            options.Key = "key";
            options.User = "fred";
            options.CrashTypeId = exceptionType;

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
            StringAssert.AreEqualIgnoringCase(stackTrace, fakeUnityWebClient.FormData["callstack"]);
            StringAssert.AreEqualIgnoringCase(exceptionType.ToString(), fakeUnityWebClient.FormData["crashTypeId"]);
        }

        [UnityTest]
        public IEnumerator Post_WithException_ShouldCallPostWithUrlAndFormData()
        {
            var database = "database";
            var application = "application";
            var version = "version";
            var exception = new Exception("BugSplat");
            var exceptionType = 12;
            var fakeUnityWebRequest = new FakeUnityWebRequest();
            var fakeUnityWebClient = new FakeUnityWebClient(fakeUnityWebRequest);
            var options = new FakeExceptionPostOptions();
            options.Description = "description";
            options.Email = "fred@bugsplat.com";
            options.Key = "key";
            options.User = "fred";
            options.CrashTypeId = exceptionType;

            var sut = new WebGLExceptionClient(database, application, version);
            sut.UnityWebClient = fakeUnityWebClient;
            yield return sut.Post(exception, options);

            StringAssert.AreEqualIgnoringCase($"https://{database}.bugsplat.com/post/dotnetstandard/", fakeUnityWebClient.Url);
            StringAssert.AreEqualIgnoringCase(database, fakeUnityWebClient.FormData["database"]);
            StringAssert.AreEqualIgnoringCase(application, fakeUnityWebClient.FormData["appName"]);
            StringAssert.AreEqualIgnoringCase(version, fakeUnityWebClient.FormData["appVersion"]);
            StringAssert.AreEqualIgnoringCase(options.Description, fakeUnityWebClient.FormData["description"]);
            StringAssert.AreEqualIgnoringCase(options.Email, fakeUnityWebClient.FormData["email"]);
            StringAssert.AreEqualIgnoringCase(options.Key, fakeUnityWebClient.FormData["appKey"]);
            StringAssert.AreEqualIgnoringCase(options.User, fakeUnityWebClient.FormData["user"]);
            StringAssert.AreEqualIgnoringCase(exception.ToString(), fakeUnityWebClient.FormData["callstack"]);
            StringAssert.AreEqualIgnoringCase(exceptionType.ToString(), fakeUnityWebClient.FormData["crashTypeId"]);
        }
    }
}
