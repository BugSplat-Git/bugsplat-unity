using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Reporter.Fakes;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Reporter
{
    class WindowsReporterTests
    {
        [Test]
        public void LogMessageReceived_ShouldCallLogMessageReceived()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var type = LogType.Log;
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);

            sut.LogMessageReceived(logMessage, stackTrace, type);

            Assert.IsNotEmpty(fakeDotNetStandardExceptionReporter.Calls.LogMessageReceived);
            Assert.AreEqual(logMessage, fakeDotNetStandardExceptionReporter.Calls.LogMessageReceived[0].LogMessage);
            Assert.AreEqual(stackTrace, fakeDotNetStandardExceptionReporter.Calls.LogMessageReceived[0].StackTrace);
            Assert.AreEqual(type, fakeDotNetStandardExceptionReporter.Calls.LogMessageReceived[0].Type);
        }

        [UnityTest]
        public IEnumerator Post_ShouldCallPostWIthExceptionAndOptions()
        {
            var exception = new Exception("BugSplat rocks!");
            var options = new ReportPostOptions();
            options.Description = "new description";
            options.Email = "barney@bugsplat.com";
            options.Key = "new key";
            options.User = "barney";
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);

            yield return sut.Post(exception, options);

            Assert.IsNotEmpty(fakeDotNetStandardExceptionReporter.Calls.Post);
            Assert.AreEqual(exception, fakeDotNetStandardExceptionReporter.Calls.Post[0].Exception);
            Assert.AreEqual(options, fakeDotNetStandardExceptionReporter.Calls.Post[0].Options);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_WhenCrashFolderExistsFalse_ShouldNotCallPost()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeDirectoryInfo = new FakeDirectoryInfo() { Exists = false };
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);

            yield return sut.PostAllCrashes();

            Assert.IsEmpty(fakeDotNetStandardExceptionReporter.Calls.Post);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_WithoutOptions_ShouldCallPostWithDefaultedOptions()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.Description = "BugSplat rocks!";
            clientSettings.Email = "fred@bugsplat.com";
            clientSettings.Key = "key";
            clientSettings.User = "fred";
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolders = new FakeDirectoryInfo[] { new FakeDirectoryInfo(files: fakeCrashFiles) };
            var fakeDirectoryInfo = new FakeDirectoryInfo(fakeCrashFolders) { Exists = true };
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);

            yield return sut.PostAllCrashes();

            Assert.IsNotEmpty(fakeNativeCrashReportClient.Calls);
            Assert.AreEqual(clientSettings.Description, fakeNativeCrashReportClient.Calls[0].Options.Description);
            Assert.AreEqual(clientSettings.Email, fakeNativeCrashReportClient.Calls[0].Options.Email);
            Assert.AreEqual(clientSettings.Key, fakeNativeCrashReportClient.Calls[0].Options.Key);
            Assert.AreEqual(clientSettings.User, fakeNativeCrashReportClient.Calls[0].Options.User);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_WithoutCallback_ShouldNotThrow()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var options = new ReportPostOptions();
            options.Description = "new description";
            options.Email = "barney@bugsplat.com";
            options.Key = "new key";
            options.User = "barney";
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolders = new FakeDirectoryInfo[] { new FakeDirectoryInfo(files: fakeCrashFiles) };
            var fakeDirectoryInfo = new FakeDirectoryInfo(fakeCrashFolders) { Exists = true };
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);

            yield return sut.PostAllCrashes(options);

            Assert.IsNotEmpty(fakeNativeCrashReportClient.Calls);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_WithOptions_ShouldCallPostWithOverriddenOptions()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.Description = "BugSplat rocks!";
            clientSettings.Email = "fred@bugsplat.com";
            clientSettings.Key = "key";
            clientSettings.User = "fred";
            var options = new ReportPostOptions();
            options.Description = "new description";
            options.Email = "barney@bugsplat.com";
            options.Key = "new key";
            options.User = "barney";
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolders = new FakeDirectoryInfo[] { new FakeDirectoryInfo(files: fakeCrashFiles) };
            var fakeDirectoryInfo = new FakeDirectoryInfo(fakeCrashFolders) { Exists = true };
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);

            yield return sut.PostAllCrashes(options);

            Assert.IsNotEmpty(fakeNativeCrashReportClient.Calls);
            Assert.AreEqual(options.Description, fakeNativeCrashReportClient.Calls[0].Options.Description);
            Assert.AreEqual(options.Email, fakeNativeCrashReportClient.Calls[0].Options.Email);
            Assert.AreEqual(options.Key, fakeNativeCrashReportClient.Calls[0].Options.Key);
            Assert.AreEqual(options.User, fakeNativeCrashReportClient.Calls[0].Options.User);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_WhenResponseStatusCodeOK_ShouldWriteResponseContentToSentinelFile()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeNativeCrashReportPostResponseContent = "success!";
            var fakeNativeCrashReportPostResponse = new HttpResponseMessage();
            fakeNativeCrashReportPostResponse.StatusCode = System.Net.HttpStatusCode.OK;
            fakeNativeCrashReportPostResponse.Content = new StringContent(fakeNativeCrashReportPostResponseContent);
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(fakeNativeCrashReportPostResponse);
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolders = new FakeDirectoryInfo[] { new FakeDirectoryInfo(files: fakeCrashFiles) };
            var fakeDirectoryInfo = new FakeDirectoryInfo(fakeCrashFolders) { Exists = true };
            var fakeFileContentsWriter = new FakeFileContentsWriter();
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);
            sut.FileContentsWriter = fakeFileContentsWriter;

            var completed = new Task<bool>(() => true);
            yield return sut.PostAllCrashes(callback: (responses) => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsNotEmpty(fakeFileContentsWriter.Calls);
            StringAssert.AreEqualIgnoringCase(WindowsReporter.SentinelFileName, fakeFileContentsWriter.Calls[0].Path);
            StringAssert.AreEqualIgnoringCase(fakeNativeCrashReportPostResponseContent, fakeFileContentsWriter.Calls[0].Contents);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_WhenResponseStatusCodeNotOK_ShouldNotWriteResponseContentToSentinelFile()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeNativeCrashReportPostResponse = new HttpResponseMessage();
            fakeNativeCrashReportPostResponse.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(fakeNativeCrashReportPostResponse);
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolders = new FakeDirectoryInfo[] { new FakeDirectoryInfo(files: fakeCrashFiles) };
            var fakeDirectoryInfo = new FakeDirectoryInfo(fakeCrashFolders) { Exists = true };
            var fakeFileContentsWriter = new FakeFileContentsWriter();
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);
            sut.FileContentsWriter = fakeFileContentsWriter;

            var completed = new Task<bool>(() => true);
            yield return sut.PostAllCrashes(callback: (responses) => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsEmpty(fakeFileContentsWriter.Calls);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_WithCallback_ShouldInvokeCallbackWithResults()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeNativeCrashReportPostResponse = new HttpResponseMessage();
            fakeNativeCrashReportPostResponse.StatusCode = System.Net.HttpStatusCode.OK;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(fakeNativeCrashReportPostResponse);
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolders = new FakeDirectoryInfo[] { new FakeDirectoryInfo(files: fakeCrashFiles) };
            var fakeDirectoryInfo = new FakeDirectoryInfo(fakeCrashFolders) { Exists = true };
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);
            sut.FileContentsWriter = new FakeFileContentsWriter();

            var results = new List<HttpResponseMessage>();
            var completed = new Task<bool>(() => true);
            Action<List<HttpResponseMessage>> callback = (responses) =>
            {
                results = responses;
                completed.Start();
            };
            yield return sut.PostAllCrashes(callback: callback);
            yield return completed.AsCoroutine();

            Assert.AreEqual(fakeNativeCrashReportPostResponse, results[0]);
        }

        [UnityTest]
        public IEnumerator PostAllCrashes_ShouldCallPostWithAllMinidumpFiles()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeNativeCrashReportPostResponse = new HttpResponseMessage();
            fakeNativeCrashReportPostResponse.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(fakeNativeCrashReportPostResponse);
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFile0 = new FileInfo[] { new FileInfo("bugsplat0.dmp") };
            var fakeCrashFile1 = new FileInfo[] { new FileInfo("bugsplat1.dmp") };
            var fakeCrashFolders = new FakeDirectoryInfo[] {
                new FakeDirectoryInfo(files: fakeCrashFile0),
                new FakeDirectoryInfo(files: fakeCrashFile1)
            };
            var fakeDirectoryInfo = new FakeDirectoryInfo(fakeCrashFolders) { Exists = true };
            var fakeFileContentsWriter = new FakeFileContentsWriter();
            sut.DirectoryInfoFactory = new FakeDirectoryInfoFactory(fakeDirectoryInfo);
            sut.FileContentsWriter = fakeFileContentsWriter;

            yield return sut.PostAllCrashes();

            Assert.IsNotEmpty(fakeNativeCrashReportClient.Calls);
            Assert.AreEqual(fakeCrashFile0[0], fakeNativeCrashReportClient.Calls[0].MinidumpFileInfo);
            Assert.AreEqual(fakeCrashFile1[0], fakeNativeCrashReportClient.Calls[1].MinidumpFileInfo);
        }

        [UnityTest]
        public IEnumerator PostCrash_WithoutOptions_ShouldCallPostWithDefaultOptions()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.Description = "BugSplat rocks!";
            clientSettings.Email = "fred@bugsplat.com";
            clientSettings.Key = "key";
            clientSettings.User = "fred";
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolder = new FakeDirectoryInfo(files: fakeCrashFiles) { Exists = true };
            
            yield return sut.PostCrash(fakeCrashFolder);

            Assert.IsNotEmpty(fakeNativeCrashReportClient.Calls);
            Assert.AreEqual(clientSettings.Description, fakeNativeCrashReportClient.Calls[0].Options.Description);
            Assert.AreEqual(clientSettings.Email, fakeNativeCrashReportClient.Calls[0].Options.Email);
            Assert.AreEqual(clientSettings.Key, fakeNativeCrashReportClient.Calls[0].Options.Key);
            Assert.AreEqual(clientSettings.User, fakeNativeCrashReportClient.Calls[0].Options.User);
        }

        [UnityTest]
        public IEnumerator PostCrash_WithoutCallback_ShouldNotThrow()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolder = new FakeDirectoryInfo(files: fakeCrashFiles) { Exists = true };

            yield return sut.PostCrash(fakeCrashFolder);

            Assert.IsNotEmpty(fakeNativeCrashReportClient.Calls);
        }

        [UnityTest]
        public IEnumerator PostCrash_WithFolderContainingSentinelFile_ShouldNotCallPost()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] {
                new FileInfo("bugsplat.dmp"),
                new FileInfo(WindowsReporter.SentinelFileName)
            };
            var fakeCrashFolder = new FakeDirectoryInfo(files: fakeCrashFiles) { Exists = true };

            yield return sut.PostCrash(fakeCrashFolder);

            Assert.IsEmpty(fakeNativeCrashReportClient.Calls);
        }

        [UnityTest]
        public IEnumerator PostCrash_WithFolderNotContainingMinidump_ShouldNotCallPost()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(new HttpResponseMessage());
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("log.txt") };
            var fakeCrashFolder = new FakeDirectoryInfo(files: fakeCrashFiles) { Exists = true };

            yield return sut.PostCrash(fakeCrashFolder);

            Assert.IsEmpty(fakeNativeCrashReportClient.Calls);
        }

        [UnityTest]
        public IEnumerator PostCrash_WhenResponseStatusCodeOK_ShouldWriteSentinelFile()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeNativeCrashReportPostResponseContent = "success!";
            var fakeNativeCrashReportPostResponse = new HttpResponseMessage();
            fakeNativeCrashReportPostResponse.StatusCode = System.Net.HttpStatusCode.OK;
            fakeNativeCrashReportPostResponse.Content = new StringContent(fakeNativeCrashReportPostResponseContent);
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(fakeNativeCrashReportPostResponse);
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolder = new FakeDirectoryInfo(files: fakeCrashFiles) { Exists = true };
            var fakeFileContentsWriter = new FakeFileContentsWriter();
            sut.FileContentsWriter = fakeFileContentsWriter;

            var completed = new Task<bool>(() => true);
            yield return sut.PostCrash(fakeCrashFolder, callback: (responses) => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsNotEmpty(fakeFileContentsWriter.Calls);
            StringAssert.AreEqualIgnoringCase(WindowsReporter.SentinelFileName, fakeFileContentsWriter.Calls[0].Path);
            StringAssert.AreEqualIgnoringCase(fakeNativeCrashReportPostResponseContent, fakeFileContentsWriter.Calls[0].Contents);
        }

        [UnityTest]
        public IEnumerator PostCrash_WhenResponseStatusCodeNotOK_ShouldNotWriteSentinelFile()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeNativeCrashReportPostResponseContent = "success!";
            var fakeNativeCrashReportPostResponse = new HttpResponseMessage();
            fakeNativeCrashReportPostResponse.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            fakeNativeCrashReportPostResponse.Content = new StringContent(fakeNativeCrashReportPostResponseContent);
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(fakeNativeCrashReportPostResponse);
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolder = new FakeDirectoryInfo(files: fakeCrashFiles) { Exists = true };
            var fakeFileContentsWriter = new FakeFileContentsWriter();
            sut.FileContentsWriter = fakeFileContentsWriter;

            var completed = new Task<bool>(() => true);
            yield return sut.PostCrash(fakeCrashFolder, callback: (responses) => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsEmpty(fakeFileContentsWriter.Calls);
        }

        [UnityTest]
        public IEnumerator PostCrash_WithCallback_ShouldInvokeCallbackWithResponse()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var fakeNativeCrashReportPostResponse = new HttpResponseMessage();
            fakeNativeCrashReportPostResponse.StatusCode = System.Net.HttpStatusCode.OK;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var fakeNativeCrashReportClient = new FakeNativeCrashReportClient(fakeNativeCrashReportPostResponse);
            var fakeDotNetStandardExceptionReporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
            var sut = new WindowsReporter(clientSettings, fakeDotNetStandardExceptionReporter, fakeNativeCrashReportClient);
            var fakeCrashFiles = new FileInfo[] { new FileInfo("bugsplat.dmp") };
            var fakeCrashFolder = new FakeDirectoryInfo(files: fakeCrashFiles) { Exists = true };
            sut.FileContentsWriter = new FakeFileContentsWriter();

            var result = new HttpResponseMessage();
            var completed = new Task<bool>(() => true);
            Action<HttpResponseMessage> callback = (response) =>
            {
                result = response;
                completed.Start();
            };
            yield return sut.PostCrash(fakeCrashFolder, callback: callback);
            yield return completed.AsCoroutine();

            Assert.AreEqual(fakeNativeCrashReportPostResponse, result);
        }

        [UnityTest]
        public IEnumerator PostMostRecentCrash_WithoutOptions_ShouldNotThrow()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator PostMostRecentCrash_WithoutCallback_ShouldNotThrow()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator PostMostRecentCrash_ShouldCallPostWithMostRecentCrash()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WithoutOptions_ShouldNotThrow()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WithoutCallback_ShouldNotThrow()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_ShouldCallPostWithMinidumpAndOptions()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WithCallback_ShouldInvokeCallbackWithResult()
        {
            throw new NotImplementedException();
        }
    }
}
