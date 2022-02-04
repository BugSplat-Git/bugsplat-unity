using BugSplatUnity.Runtime.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IReportUploadGuardService
    {
        public bool ShouldPostLogMessage(LogType type);
        public bool ShouldPostException(Exception exception);
    }

    internal class ReportUploadGuardService : IReportUploadGuardService
    {
        private IClientSettingsRepository _clientSettingsRepository;
        public ReportUploadGuardService(IClientSettingsRepository clientSettingsRepository)
        {
            _clientSettingsRepository = clientSettingsRepository;
        }

        public bool ShouldPostException(Exception exception)
        {
            var shouldPostException = _clientSettingsRepository.ShouldPostException(exception);

            if (Application.isEditor)
            {
                shouldPostException &= _clientSettingsRepository.PostExceptionsInEditor;
            }

            return shouldPostException;
        }

        public bool ShouldPostLogMessage(LogType type)
        {
            var shouldPostLogMessage = ShouldPostException(null);
            shouldPostLogMessage &= (type == LogType.Exception);
            return shouldPostLogMessage;
        }
    }
}
