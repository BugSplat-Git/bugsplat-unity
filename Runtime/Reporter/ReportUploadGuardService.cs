using BugSplatUnity.Runtime.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IReportUploadGuardService
    {
        public bool ShouldPostLogMessage(IClientSettingsRepository clientSettingsRepository, Exception exception, LogType type);
        public bool ShouldPostException(IClientSettingsRepository clientSettingsRepository, Exception exception);
    }

    internal class ReportUploadGuardService : IReportUploadGuardService
    {
        public bool ShouldPostException(IClientSettingsRepository clientSettingsRepository, Exception exception)
        {
            var shouldPostException = clientSettingsRepository.ShouldPostException(exception);

            if (Application.isEditor)
            {
                shouldPostException &= clientSettingsRepository.PostExceptionsInEditor;
            }

            return shouldPostException;
        }

        public bool ShouldPostLogMessage(IClientSettingsRepository clientSettingsRepository, Exception exception, LogType type)
        {
            var shouldPostLogMessage = ShouldPostException(clientSettingsRepository, exception);
            shouldPostLogMessage &= (type == LogType.Exception);
            return shouldPostLogMessage;
        }
    }
}
