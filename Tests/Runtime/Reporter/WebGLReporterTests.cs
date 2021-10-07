using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
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
        [Test]
        public void LogMessageReceived_WhenTypeNotException_ShouldNotCallPost()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => true;
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var gameObject = new GameObject();
            var webGLClientSettings = new WebGLClientSettingsRepository();
            var sut = WebGLReporter.Create(
                webGLClientSettings,
                fakeExceptionClient,
                gameObject
            );

            sut.LogMessageReceived(logMessage, stackTrace, LogType.Log);

            Assert.IsEmpty(fakeExceptionClient.Calls);
        }

        [Test]
        public void LogMessageReceived_WhenShouldPostExceptionFalse_ShouldNotCallPost()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => false;
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var gameObject = new GameObject();
            var sut = WebGLReporter.Create(
                clientSettings,
                fakeExceptionClient,
                gameObject
            );

            sut.LogMessageReceived(logMessage, stackTrace, LogType.Exception);

            Assert.IsEmpty(fakeExceptionClient.Calls);
        }

        [Test]
        public void LogMessageReceived_WhenLogTypeExceptionAndShouldPostExceptionTrue_ShouldCallPostWithStackTraceAndOptions()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => true;
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var gameObject = new GameObject();
            var sut = WebGLReporter.Create(
                clientSettings,
                fakeExceptionClient,
                gameObject
            );

            sut.LogMessageReceived(logMessage, stackTrace, LogType.Exception);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.NotNull(fakeExceptionClient.Calls[0].Options);
            Assert.AreEqual(12, fakeExceptionClient.Calls[0].Options.CrashTypeId); // TODO BG unitylegacycrashtypeid
            StringAssert.AreEqualIgnoringCase($"{logMessage}\n{stackTrace}", fakeExceptionClient.Calls[0].StackTrace);
        }

        [UnityTest]
        public IEnumerator Post_WhenShouldPostExceptionFalse_ShouldNotCallPost()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => false;
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var gameObject = new GameObject();
            var sut = WebGLReporter.Create(
                clientSettings,
                fakeExceptionClient,
                gameObject
            );

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
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var gameObject = new GameObject();
            var sut = WebGLReporter.Create(
                clientSettings,
                fakeExceptionClient,
                gameObject
            );

            yield return sut.Post(exception);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception.ToString(), fakeExceptionClient.Calls[0].StackTrace);
            Assert.AreEqual(clientSettings.Description, fakeExceptionClient.Calls[0].Options.Description);
            Assert.AreEqual(clientSettings.Email, fakeExceptionClient.Calls[0].Options.Email);
            Assert.AreEqual(clientSettings.Key, fakeExceptionClient.Calls[0].Options.Key);
            Assert.AreEqual(clientSettings.User, fakeExceptionClient.Calls[0].Options.User);
            Assert.AreEqual(24, fakeExceptionClient.Calls[0].Options.CrashTypeId);  // TODO BG unitylegacycrashtypeid
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
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var gameObject = new GameObject();
            var sut = WebGLReporter.Create(
                clientSettings,
                fakeExceptionClient,
                gameObject
            );

            yield return sut.Post(exception, options);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception.ToString(), fakeExceptionClient.Calls[0].StackTrace);
            Assert.AreEqual(options.Description, fakeExceptionClient.Calls[0].Options.Description);
            Assert.AreEqual(options.Email, fakeExceptionClient.Calls[0].Options.Email);
            Assert.AreEqual(options.Key, fakeExceptionClient.Calls[0].Options.Key);
            Assert.AreEqual(options.User, fakeExceptionClient.Calls[0].Options.User);
            Assert.AreEqual(24, fakeExceptionClient.Calls[0].Options.CrashTypeId);  // TODO BG unitylegacycrashtypeid
        }

        [UnityTest]
        public IEnumerator Post_WithCallback_ShouldInvokeCallback()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => true;
            var fakeExceptionClient = new FakeWebGLExceptionClient();
            var gameObject = new GameObject();
            var sut = WebGLReporter.Create(
                clientSettings,
                fakeExceptionClient,
                gameObject
            );

            var invoked = false;
            var completed = new Task<bool>(() => invoked = true);
            yield return sut.Post(exception, callback: () => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsTrue(invoked);
        }
    }
}
