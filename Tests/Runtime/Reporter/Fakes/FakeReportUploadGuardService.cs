using BugSplatUnity.Runtime.Reporter;
using System;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    internal class FakeTrueReportUploadGuardService : IReportUploadGuardService
    {
        public int SuppressedCount { get; set; }

        public bool ShouldPostException(Exception exception, out int suppressedCount)
        {
            suppressedCount = SuppressedCount;
            return true;
        }

        public bool ShouldPostLogMessage(LogType type, string logMessage, string stackTrace, out int suppressedCount)
        {
            suppressedCount = SuppressedCount;
            return true;
        }
    }

    internal class FakeFalseReportUploadGuardService : IReportUploadGuardService
    {
        public bool ShouldPostException(Exception exception, out int suppressedCount)
        {
            suppressedCount = 0;
            return false;
        }

        public bool ShouldPostLogMessage(LogType type, string logMessage, string stackTrace, out int suppressedCount)
        {
            suppressedCount = 0;
            return false;
        }
    }
}
