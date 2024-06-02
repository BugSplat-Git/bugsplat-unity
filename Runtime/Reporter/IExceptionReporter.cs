using System;
using System.Collections;
using UnityEngine;

namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IExceptionReporter
    {
        void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action<ExceptionReporterPostResult> callback = null);
        IEnumerator Post(Exception exception, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null);
    }

    [System.Serializable]
    public class BugSplatResponse
    {
        public string status;
        public string infoUrl;
        public int crashId;
    }

    public class ExceptionReporterPostResult
    {
        public bool Uploaded { get; set; }
        public string Exception { get; set; }
        public string Message { get; set; }
        public BugSplatResponse Response { get; set; }
    }
}
