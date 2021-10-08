using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    class FakeDotNetStandardExceptionReporter : IExceptionReporter
    {
        public FakeDotNetStandardExceptionReporterCalls Calls { get; } = new FakeDotNetStandardExceptionReporterCalls();

        private readonly HttpResponseMessage _result;

        public FakeDotNetStandardExceptionReporter(HttpResponseMessage result)
        {
            _result = result;
        }

        public void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action callback = null)
        {
            Calls.LogMessageReceived.Add(
                new FakeDotNetStandardExceptionReporterLogMessageReceivedCall()
                {
                    LogMessage = logMessage,
                    StackTrace = stackTrace,
                    Type = type
                }
            );
            callback?.Invoke();
        }

        public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action callback = null)
        {
            Calls.Post.Add(
                new FakeDotNetStandardExceptionReporterPostCall()
                {
                    Exception = exception,
                    Options = options
                }
            );
            yield return null;
            callback?.Invoke();
        }

        public IEnumerator Post(FileInfo minidump, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            Calls.Post.Add(
                new FakeDotNetStandardExceptionReporterPostCall()
                { 
                    Minidump = minidump,
                    Options = options
                }
            );
            yield return null;
            callback?.Invoke(_result);
        }
    }

    class FakeDotNetStandardExceptionReporterCalls
    {
        public List<FakeDotNetStandardExceptionReporterLogMessageReceivedCall> LogMessageReceived { get; } = new List<FakeDotNetStandardExceptionReporterLogMessageReceivedCall>();
        public List<FakeDotNetStandardExceptionReporterPostCall> Post { get; } = new List<FakeDotNetStandardExceptionReporterPostCall>();
    }

    class FakeDotNetStandardExceptionReporterLogMessageReceivedCall
    {
        public string LogMessage { get; set; }
        public string StackTrace { get; set; }
        public LogType Type { get; set; }
    }

    class FakeDotNetStandardExceptionReporterPostCall
    {
        public FileInfo Minidump { get; set; }
        public Exception Exception { get; set; }
        public IReportPostOptions Options { get; set; }
    }
}
