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
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = false;
            clientSettings.ShouldPostException = (Exception ex) => true;
            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();

            Assert.IsFalse(rugs.ShouldPostException(exception));
        }

        [Test]
        public void ShouldPostException_WhenPostExceptionsInEditorTrue_WhenInEditor_ShouldReturnTrue()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = true;
            clientSettings.ShouldPostException = (Exception ex) => true;
            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();

            Assert.IsTrue(rugs.ShouldPostException(exception));
        }

        [Test]
        public void ShouldPostException_WhenClientSettingsRepositoryShouldPostExceptionFalse_ShouldReturnFalse()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (Exception ex) => false;
            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();

            Assert.IsFalse(rugs.ShouldPostException(exception));
        }

        [Test]
        public void ShouldPostException_WhenClientSettingsRepositoryShouldPostExceptionTrue_ShouldReturnTrue()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (Exception ex) => true;
            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();

            Assert.IsTrue(rugs.ShouldPostException(exception));
        }

        [Test]
        public void ShouldPostException_WhenPostExceptionsInEditorFalse_WhenInEditor_ShouldNotCallClientSettingsRepositoryShouldPostException()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = false;

            var called = false;
            clientSettings.ShouldPostException = (Exception ex) =>
            {
                called = true;
                return false;
            };

            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();

            rugs.ShouldPostException(exception);
            Assert.IsFalse(called);
        }

        [Test]
        public void ShouldPostLogMessage_WhenLogTypeNotException_ShouldReturnFalse()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (Exception ex) => true;
            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();
            var logType = LogType.Error;

            Assert.IsFalse(rugs.ShouldPostLogMessage(logType));
        }

        [Test]
        public void ShouldPostLogMessage_WhenLogTypeException_ShouldReturnTrue()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.ShouldPostException = (Exception ex) => true;
            var rugs = new ReportUploadGuardService(clientSettings);         
            var exception = new Exception();
            var logType = LogType.Exception;

            Assert.IsTrue(rugs.ShouldPostLogMessage(logType));
        }

        [Test]
        public void ShouldPostLogMessage_WhenLogTypeNotException_ShouldNotCallClientSettingsRepositoryShouldPostException()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();
            var logType = LogType.Error;


            var called = false;
            clientSettings.ShouldPostException = (Exception ex) =>
            {
                called = true;
                return false;
            };

            rugs.ShouldPostLogMessage(logType);
            Assert.IsFalse(called);
        }

        [Test]
        public void ShouldPostLogMessage_WhenPostExceptionsInEditorTrue_WhenLogTypeException_ShouldCallClientSettingsRepositoryShouldPostException()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = true;

            var called = false;
            clientSettings.ShouldPostException = (Exception ex) =>
            {
                called = true;
                return false;
            };

            var rugs = new ReportUploadGuardService(clientSettings);
            var logType = LogType.Exception;

            rugs.ShouldPostLogMessage(logType);
            Assert.IsTrue(called);
        }
    }
}

