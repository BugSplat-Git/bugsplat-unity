using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.RuntimeTests.Client.Fakes;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Reporter
{
    class WebGLReporterTests
    {
        const int UnityLegacyCrashTypeId = 12;
        const int UnityCrashTypeId = 24;

        [UnityTest]
        public IEnumerator LogMessageReceived_WhenReportUploadGuardServiceReturnsFalse_ShouldNotCallPost()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository
            {
                ShouldPostException = (ex) => false
            };
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var sut = new WebGLReporter(
                clientSettings,
                fakeExceptionClient
            )
            {
                reportUploadGuardService = new FakeFalseReportUploadGuardService()
            };
            yield return sut.LogMessageReceived(logMessage, stackTrace, LogType.Exception);

            Assert.IsEmpty(fakeExceptionClient.Calls);
        }

        [UnityTest]
        public IEnumerator LogMessageReceived_WhenReportUploadGuardServiceReturnsTrue_ShouldCallPostWithStackTraceAndOptions()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository
            {
                ShouldPostException = (ex) => true
            };
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var sut = new WebGLReporter(
                clientSettings,
                fakeExceptionClient
            )
            {
                reportUploadGuardService = new FakeTrueReportUploadGuardService()
            };
            yield return sut.LogMessageReceived(logMessage, stackTrace, LogType.Exception);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.NotNull(fakeExceptionClient.Calls[0].Options);
            Assert.AreEqual(UnityLegacyCrashTypeId, fakeExceptionClient.Calls[0].Options.CrashTypeId);
            StringAssert.AreEqualIgnoringCase($"{logMessage}\n{stackTrace}", fakeExceptionClient.Calls[0].StackTrace);
        }

        [UnityTest]
        public IEnumerator Post_WhenReportUploadGuardServiceReturnsFalse_ShouldNotCallPost()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository
            {
                ShouldPostException = (ex) => false
            };
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var sut = new WebGLReporter(
                clientSettings,
                fakeExceptionClient
            )
            {
                reportUploadGuardService = new FakeFalseReportUploadGuardService()
            };
            yield return sut.Post(exception);

            Assert.IsEmpty(fakeExceptionClient.Calls);
        }

        [UnityTest]
        public IEnumerator Post_WithoutOptions_ShouldCallPostWithExceptionAndDefaultedOptions()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository
            {
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                Key = "key",
                User = "fred",
                ShouldPostException = (ex) => true
            };
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var sut = new WebGLReporter(
                clientSettings,
                fakeExceptionClient
            )
            {
                reportUploadGuardService = new FakeTrueReportUploadGuardService()
            };
            yield return sut.Post(exception);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception.ToString(), fakeExceptionClient.Calls[0].StackTrace);
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
            var clientSettings = new WebGLClientSettingsRepository
            {
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                Key = "key",
                User = "fred",
                ShouldPostException = (ex) => true
            };
            var options = new ReportPostOptions
            {
                Description = "new description",
                Email = "barney@bugsplat.com",
                Key = "new key",
                User = "barney"
            };
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var sut = new WebGLReporter(
                clientSettings,
                fakeExceptionClient
            )
            {
                reportUploadGuardService = new FakeTrueReportUploadGuardService()
            };
            yield return sut.Post(exception, options);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception.ToString(), fakeExceptionClient.Calls[0].StackTrace);
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
            var clientSettings = new WebGLClientSettingsRepository
            {
                ShouldPostException = (ex) => true
            };
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var sut = new WebGLReporter(
                clientSettings,
                fakeExceptionClient
            )
            {
                reportUploadGuardService = new FakeTrueReportUploadGuardService()
            };

            var invoked = false;
            var completed = new Task<bool>(() => invoked = true);
            yield return sut.Post(exception, callback: (_) => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsTrue(invoked);
        }
    }
}
