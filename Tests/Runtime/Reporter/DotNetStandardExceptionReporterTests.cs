﻿using BugSplatUnity.Runtime.Client;
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
        [Test]
        public void LogMessageReceived_WhenTypeNotException_ShouldNotCallPost()
        {
            var logMessage = "logMessage";
            var stackTrace = "stackTrace";
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => true;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);

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
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);

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
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());

            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);

            sut.LogMessageReceived(logMessage, stackTrace, LogType.Exception);

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.NotNull(fakeExceptionClient.Calls[0].Options);
            Assert.AreEqual(12, fakeExceptionClient.Calls[0].Options.CrashTypeId);
            StringAssert.AreEqualIgnoringCase($"{logMessage}\n{stackTrace}", fakeExceptionClient.Calls[0].StackTrace);
        }

        [UnityTest]
        public IEnumerator Post_WhenShouldPostExceptionFalse_ShouldNotCallPost()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => false;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);

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
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);

            var completed = new Task<bool>(() => true);
            yield return sut.Post(exception, callback: () => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception, fakeExceptionClient.Calls[0].Exception);
            Assert.AreEqual(clientSettings.Description, fakeExceptionClient.Calls[0].Options.Description);
            Assert.AreEqual(clientSettings.Email, fakeExceptionClient.Calls[0].Options.Email);
            Assert.AreEqual(clientSettings.Key, fakeExceptionClient.Calls[0].Options.Key);
            Assert.AreEqual(clientSettings.User, fakeExceptionClient.Calls[0].Options.User);
            Assert.AreEqual(24, fakeExceptionClient.Calls[0].Options.CrashTypeId);
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
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);

            var completed = new Task<bool>(() => true);
            yield return sut.Post(exception, options, () => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsNotEmpty(fakeExceptionClient.Calls);
            Assert.AreEqual(exception, fakeExceptionClient.Calls[0].Exception);
            Assert.AreEqual(options.Description, fakeExceptionClient.Calls[0].Options.Description);
            Assert.AreEqual(options.Email, fakeExceptionClient.Calls[0].Options.Email);
            Assert.AreEqual(options.Key, fakeExceptionClient.Calls[0].Options.Key);
            Assert.AreEqual(options.User, fakeExceptionClient.Calls[0].Options.User);
            Assert.AreEqual(24, fakeExceptionClient.Calls[0].Options.CrashTypeId);
        }

        [UnityTest]
        public IEnumerator Post_WithCallback_ShouldInvokeCallback()
        {
            var exception = new Exception("BugSplat rocks!");
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (ex) => true;
            var fakeExceptionClient = new FakeDotNetExceptionClient(new HttpResponseMessage());
            var sut = new DotNetStandardExceptionReporter(clientSettings, fakeExceptionClient);

            var invoked = false;
            var completed = new Task<bool>(() => invoked = true);
            yield return sut.Post(exception, callback: () => completed.Start());
            yield return completed.AsCoroutine();

            Assert.IsTrue(invoked);
        }
    }
}