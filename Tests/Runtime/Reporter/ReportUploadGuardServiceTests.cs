using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using NUnit.Framework;
using System;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Reporter
{
    public class ReportUploadGuardServiceTests
    {
        [Test]
        public void ShouldPostException_WhenPostExceptionsInEditorFalse_WhenInEditor_ShouldReturnFalse()
        {
            var rugs = new ReportUploadGuardService();
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = false;
            clientSettings.ShouldPostException = (Exception ex) => true;
            var exception = new Exception();

            Assert.IsFalse(rugs.ShouldPostException(clientSettings, exception));
        }

        [Test]
        public void ShouldPostException_WhenPostExceptionsInEditorTrue_WhenInEditor_ShouldReturnTrue()
        {
            var rugs = new ReportUploadGuardService();
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = true;
            clientSettings.ShouldPostException = (Exception ex) => true;
            var exception = new Exception();

            Assert.IsTrue(rugs.ShouldPostException(clientSettings, exception));
        }

        [Test]
        public void ShouldPostException_WhenClientSettingsRepositoryShouldPostExceptionFalse_ShouldReturnFalse()
        {
            var rugs = new ReportUploadGuardService();
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (Exception ex) => false;
            var exception = new Exception();

            Assert.IsFalse(rugs.ShouldPostException(clientSettings, exception));
        }

        [Test]
        public void ShouldPostException_WhenClientSettingsRepositoryShouldPostExceptionTrue_ShouldReturnTrue()
        {
            var rugs = new ReportUploadGuardService();
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (Exception ex) => true;
            var exception = new Exception();

            Assert.IsTrue(rugs.ShouldPostException(clientSettings, exception));
        }

        [Test]
        public void ShouldPostLogMessage_WhenLogTypeNotException_ShouldReturnFalse()
        {
            var rugs = new ReportUploadGuardService();
            var clientSettings = new WebGLClientSettingsRepository();
            var exception = new Exception();
            clientSettings.ShouldPostException = (Exception ex) => true;
            var logType = LogType.Error;

            Assert.IsFalse(rugs.ShouldPostLogMessage(clientSettings, exception, logType));
        }

        [Test]
        public void ShouldPostLogMessage_WhenLogTypeException_ShouldReturnTrue()
        {
            var rugs = new ReportUploadGuardService();
            var clientSettings = new WebGLClientSettingsRepository();
            var exception = new Exception();
            clientSettings.ShouldPostException = (Exception ex) => true;
            var logType = LogType.Exception;

            Assert.IsTrue(rugs.ShouldPostLogMessage(clientSettings, exception, logType));
        }
    }
}

