using BugSplatUnity.Runtime.Client;
using System;
using System.Collections;
using UnityEngine;

namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IExceptionReporter
    {
        void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action callback = null);
        IEnumerator Post(Exception exception, IReportPostOptions options = null, Action callback = null);
    }
}
