using BugSplatUnity.Runtime.Settings;
using System;
using UnityEngine;


namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IReportUploadGuardService
    {
        bool ShouldPostLogMessage(LogType type);
        bool ShouldPostException(Exception exception);
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

            return _clientSettingsRepository.ShouldPostException(exception);
        }

        public bool ShouldPostLogMessage(LogType type)
        {
            if (type != LogType.Exception)
            {
                return false;
            }

            return ShouldPostException(null);
        }
    }
}
