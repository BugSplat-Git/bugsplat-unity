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
        public bool ShouldPostException(Exception exception)
        {
            return true;
        }

        public bool ShouldPostLogMessage(LogType type)
        {
            return true;
        }
    }

    internal class FakeFalseReportUploadGuardService : IReportUploadGuardService
    {
        public bool ShouldPostException(Exception exception)
        {
            return false;
        }

        public bool ShouldPostLogMessage(LogType type)
        {
            return false;
        }
    }
}
