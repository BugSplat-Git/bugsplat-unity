using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using System;
using System.Collections;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Reporter
{
    class DotNetStandardExceptionReporterTests
    {
        const int UnityLegacyCrashTypeId = 12;
        const int UnityCrashTypeId = 24;


        [Test]
        public void LogMessageReceived_WhenReportUploadGuardServiceReturnsFalse_ShouldNotCallPost()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = false;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);
            sut._reportUploadGuardService = new FakeFalseReportUploadGuardService();

            sut.LogMessageReceived(logMessage, stackTrace, LogType.Exception);

            Assert.IsEmpty(fakeExceptionClient.Calls);
        }

        [Test]
        public void LogMessageReceived_WhenReportUploadGuardServiceReturnsTrue_ShouldCallPostWithStackTraceAndOptions()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => true;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());

            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);
            sut._reportUploadGuardService = new FakeTrueReportUploadGuardService();

            sut.LogMessageReceived(logMessage, stackTrace, LogType.Exception);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.NotNull(fakeExceptionClient.Calls[0].Options);
            Assert.AreEqual(UnityLegacyCrashTypeId, fakeExceptionClient.Calls[0].Options.CrashTypeId);
            StringAssert.AreEqualIgnoringCase($"{logMessage}\n{stackTrace}", fakeExceptionClient.Calls[0].StackTrace);
        }

        [UnityTest]
        public IEnumerator Post_WhenReportUploadGuardServiceReturnsFalse_ShouldNotCallPost()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => false;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);
            sut._reportUploadGuardService = new FakeFalseReportUploadGuardService();

            yield return sut.Post(exception);

            Assert.IsEmpty(fakeExceptionClient.Calls);
        }


        [UnityTest]
        public IEnumerator Post_WithoutOptions_ShouldCallPostWithExceptionAndDefaultedOptions()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.Description = "BugSplat rocks!";
            clientSettings.Email = "fred@bugsplat.com";
            clientSettings.Key = "key";
            clientSettings.User = "fred";
            clientSettings.ShouldPostException = (ex) => true;
            var httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.Content = new StringContent(string.Empty);
            var fakeExceptionClient = new FakeDotNetExceptionClient(httpResponseMessage);
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);
            sut._reportUploadGuardService = new FakeTrueReportUploadGuardService();

            var completed = new Task<bool>(() => true);
            yield return sut.Post(exception, callback: () => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception, fakeExceptionClient.Calls[0].Exception);
            Assert.AreEqual(clientSettings.Description, fakeExceptionClient.Calls[0].Options.Description);
            Assert.AreEqual(clientSettings.Email, fakeExceptionClient.Calls[0].Options.Email);
            Assert.AreEqual(clientSettings.Key, fakeExceptionClient.Calls[0].Options.Key);
            Assert.AreEqual(clientSettings.User, fakeExceptionClient.Calls[0].Options.User);
            Assert.AreEqual(UnityCrashTypeId, fakeExceptionClient.Calls[0].Options.CrashTypeId);
        }

        [UnityTest]
        public IEnumerator Post_WithOptions_ShouldCallPostWithExceptionAndOverriddenOptions()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.Description = "BugSplat rocks!";
            clientSettings.Email = "fred@bugsplat.com";
            clientSettings.Key = "key";
            clientSettings.User = "fred";
            clientSettings.ShouldPostException = (ex) => true;
            var options = new ReportPostOptions();
            options.Description = "new description";
            options.Email = "barney@bugsplat.com";
            options.Key = "new key";
            options.User = "barney";
            var httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.Content = new StringContent(string.Empty);
            var fakeExceptionClient = new FakeDotNetExceptionClient(httpResponseMessage);
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);
            sut._reportUploadGuardService = new FakeTrueReportUploadGuardService();

            var completed = new Task<bool>(() => true);
            yield return sut.Post(exception, options, () => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception, fakeExceptionClient.Calls[0].Exception);
            Assert.AreEqual(options.Description, fakeExceptionClient.Calls[0].Options.Description);
            Assert.AreEqual(options.Email, fakeExceptionClient.Calls[0].Options.Email);
            Assert.AreEqual(options.Key, fakeExceptionClient.Calls[0].Options.Key);
            Assert.AreEqual(options.User, fakeExceptionClient.Calls[0].Options.User);
            Assert.AreEqual(UnityCrashTypeId, fakeExceptionClient.Calls[0].Options.CrashTypeId);
        }

        [UnityTest]
        public IEnumerator Post_WithCallback_ShouldInvokeCallback()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => true;
            var httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.Content = new StringContent(string.Empty);
            var fakeExceptionClient = new FakeDotNetExceptionClient(httpResponseMessage);
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);
            sut._reportUploadGuardService = new FakeTrueReportUploadGuardService();

            var invoked = false;
            var completed = new Task<bool>(() => invoked = true);
            yield return sut.Post(exception, callback: () => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsTrue(invoked);
        }
    }
}
