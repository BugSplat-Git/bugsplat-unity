using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void ShouldPostException_WhenPostExceptionsInEditorFalse_WhenInEditor_WarnsOnce()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = false;
            var rugs = new ReportUploadGuardService(clientSettings);

            LogAssert.Expect(LogType.Warning, ReportUploadGuardService.SkippedInEditorMessage);
            rugs.ShouldPostException(new Exception());
            rugs.ShouldPostException(new Exception());
            rugs.ShouldPostLogMessage(LogType.Exception);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ShouldPostException_WhenPostExceptionsInEditorTrue_WhenInEditor_DoesNotWarn()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = true;
            clientSettings.ShouldPostException = (Exception ex) => true;
            var rugs = new ReportUploadGuardService(clientSettings);

            rugs.ShouldPostException(new Exception());

            LogAssert.NoUnexpectedReceived();
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
            clientSettings.PostExceptionsInEditor = true;
            clientSettings.ShouldPostException = (Exception ex) => false;
            var rugs = new ReportUploadGuardService(clientSettings);
            var exception = new Exception();

            Assert.IsFalse(rugs.ShouldPostException(exception));
        }

        [Test]
        public void ShouldPostException_WhenClientSettingsRepositoryShouldPostExceptionTrue_ShouldReturnTrue()
        {
            var clientSettings = new WebGLClientSettingsRepository();
            clientSettings.PostExceptionsInEditor = true;
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
            clientSettings.PostExceptionsInEditor = true;
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
            clientSettings.PostExceptionsInEditor = true;
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
            clientSettings.PostExceptionsInEditor = true;
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

        [Test]
        public void ShouldPostLogMessage_WithTheDefaultGuard_ShouldTellLoggedExceptionsApartAndCountRepeats()
        {
            ShouldPostExceptionImpl.Limiter.Reset();
            try
            {
                var clientSettings = new WebGLClientSettingsRepository { PostExceptionsInEditor = true };
                var rugs = new ReportUploadGuardService(clientSettings);

                Assert.IsTrue(rugs.ShouldPostLogMessage(LogType.Exception, "NullReferenceException: enemy 1", "Game.Enemy.Update ()", out _));
                Assert.IsTrue(rugs.ShouldPostLogMessage(LogType.Exception, "NullReferenceException: player", "Game.Player.Update ()", out _));
                Assert.IsFalse(rugs.ShouldPostLogMessage(LogType.Exception, "NullReferenceException: enemy 2", "Game.Enemy.Update ()", out var suppressed));
                Assert.AreEqual(1, suppressed);
            }
            finally
            {
                ShouldPostExceptionImpl.Limiter.Reset();
            }
        }

        [Test]
        public void ShouldPostLogMessage_WithACustomGuard_ShouldNotReportASuppressedCount()
        {
            var clientSettings = new WebGLClientSettingsRepository
            {
                PostExceptionsInEditor = true,
                ShouldPostException = (Exception ex) => true
            };
            var rugs = new ReportUploadGuardService(clientSettings);

            rugs.ShouldPostLogMessage(LogType.Exception, "NullReferenceException: x", "Game.Enemy.Update ()", out var first);
            rugs.ShouldPostLogMessage(LogType.Exception, "NullReferenceException: x", "Game.Enemy.Update ()", out var second);

            Assert.AreEqual(0, first);
            Assert.AreEqual(0, second);
        }
    }
}

