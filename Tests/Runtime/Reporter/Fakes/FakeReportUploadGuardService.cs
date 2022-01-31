using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    internal class FakeTrueReportUploadGuardService : IReportUploadGuardService
    {
        public bool ShouldPostException(IClientSettingsRepository clientSettingsRepository, Exception exception)
        {
            return true;
        }

        public bool ShouldPostLogMessage(IClientSettingsRepository clientSettingsRepository, Exception exception, LogType type)
        {
            return true;
        }
    }

    internal class FakeFalseReportUploadGuardService : IReportUploadGuardService
    {
        public bool ShouldPostException(IClientSettingsRepository clientSettingsRepository, Exception exception)
        {
            return false;
        }

        public bool ShouldPostLogMessage(IClientSettingsRepository clientSettingsRepository, Exception exception, LogType type)
        {
            return false;
        }
    }
}
