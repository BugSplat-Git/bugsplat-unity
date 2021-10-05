using BugSplatUnity.Runtime.Client;
using System;
using System.Collections;
using System.Net.Http;
using UnityEngine;

namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IExceptionReporter
    {
        void LogMessageReceived(string logMessage, string stackTrace, LogType type);
        IEnumerator Post(Exception exception, IExceptionPostOptions options = null, Action<HttpResponseMessage> callback = null);
    }
}
