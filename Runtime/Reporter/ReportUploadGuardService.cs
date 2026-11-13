using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using UnityEngine;


namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IReportUploadGuardService
    {
        bool ShouldPostLogMessage(LogType type, string logMessage, string stackTrace, out int suppressedCount);
        bool ShouldPostException(Exception exception, out int suppressedCount);
    }

    internal class ReportUploadGuardService : IReportUploadGuardService
    {
        internal const string SkippedInEditorMessage =
            "BugSplat: not uploading exceptions from play mode because Post Exceptions In Editor is off. " +
            "Check it in Edit > Project Settings > BugSplat to report from the editor; players always report.";

        private IClientSettingsRepository _clientSettingsRepository;
        private bool _skippedInEditorLogged;

        public ReportUploadGuardService(IClientSettingsRepository clientSettingsRepository)
        {
            _clientSettingsRepository = clientSettingsRepository;
        }

        public bool ShouldPostException(Exception exception)
        {
            return ShouldPostException(exception, out _);
        }

        public bool ShouldPostException(Exception exception, out int suppressedCount)
        {
            return ShouldPost(exception, null, null, out suppressedCount);
        }

        public bool ShouldPostLogMessage(LogType type)
        {
            return ShouldPostLogMessage(type, null, null, out _);
        }

        public bool ShouldPostLogMessage(LogType type, string logMessage, string stackTrace, out int suppressedCount)
        {
            if (type != LogType.Exception)
            {
                suppressedCount = 0;
                return false;
            }

            return ShouldPost(null, logMessage, stackTrace, out suppressedCount);
        }

        private bool ShouldPost(Exception exception, string logMessage, string stackTrace, out int suppressedCount)
        {
            suppressedCount = 0;

            if (Application.isEditor && !_clientSettingsRepository.PostExceptionsInEditor)
            {
                // Once per client, which is once per play: otherwise the first exception a new
                // integration throws in play mode vanishes without a word.
                if (!_skippedInEditorLogged)
                {
                    _skippedInEditorLogged = true;
                    Debug.LogWarning(SkippedInEditorMessage);
                }

                return false;
            }

            var shouldPostException = _clientSettingsRepository.ShouldPostException;

            // A logged exception reaches a custom guard as null, but the default guard can still
            // tell it apart from others by the logged type and stack trace.
            if (ShouldPostExceptionImpl.IsDefault(shouldPostException))
            {
                var fingerprint = exception != null
                    ? ReportFingerprint.FromException(exception)
                    : ReportFingerprint.FromLogMessage(logMessage, stackTrace);

                return ShouldPostExceptionImpl.ShouldPost(fingerprint, out suppressedCount);
            }

            return shouldPostException(exception);
        }
    }
}
