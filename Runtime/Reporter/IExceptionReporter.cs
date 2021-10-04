using BugSplatDotNetStandard; // TODO BG can we use this type directly in WebGL?
using System;
using System.Collections;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    internal interface IExceptionReporter
    {
        Task LogMessageReceived(string logMessage, string stackTrace, LogType type);
        IEnumerator Post(Exception exception, ExceptionPostOptions options = null, Action<HttpResponseMessage> callback = null);
    }
}
