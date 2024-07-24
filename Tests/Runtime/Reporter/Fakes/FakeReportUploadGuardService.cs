using BugSplatUnity.Runtime.Reporter;
using System;
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
