using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    class FakeDotNetExceptionClient : IDotNetStandardExceptionClient
    {
        public List<FakeExceptionClientPostCall> Calls { get; } = new List<FakeExceptionClientPostCall>();

        private readonly HttpResponseMessage _result;

        public FakeDotNetExceptionClient(HttpResponseMessage result)
        {
            _result = result;
        }

        public Task<HttpResponseMessage> Post(string stackTrace, IReportPostOptions options = null)
        {
            Calls.Add(
                new FakeExceptionClientPostCall()
                {
                    StackTrace = stackTrace,
                    Options = options
                }
            );
            return Task.FromResult(_result);
        }

        public Task<HttpResponseMessage> Post(Exception ex, IReportPostOptions options = null)
        {
            Calls.Add(
                new FakeExceptionClientPostCall()
                {
                    Exception = ex,
                    Options = options
                }
            );
            return Task.FromResult(_result);
        }

        public Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, IReportPostOptions options = null)
        {
            Calls.Add(
                new FakeExceptionClientPostCall()
                {
                    MinidumpFileInfo = minidumpFileInfo,
                    Options = options
                }
            );
            return Task.FromResult(_result);
        }
    }

    public class FakeWebGLExceptionClient : IWebGlExceptionClient
    {
        public List<FakeExceptionClientPostCall> Calls { get; } = new List<FakeExceptionClientPostCall>();

        public IEnumerator Post(string stackTrace, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            Calls.Add(
                new FakeExceptionClientPostCall()
                {
                    StackTrace = stackTrace,
                    Options = options
                }
            );
            callback?.Invoke(new ExceptionReporterPostResult());
            yield return null;
        }

        public IEnumerator Post(Exception ex, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            Calls.Add(
                new FakeExceptionClientPostCall()
                {
                    Exception = ex,
                    Options = options
                }
            );
            callback?.Invoke(new ExceptionReporterPostResult());
            yield return null;
        }
    }

    public class FakeExceptionClientPostCall
    {
        public Exception Exception { get; set; }
        public FileInfo MinidumpFileInfo { get; set; }
        public IReportPostOptions Options { get; set; }
        public string StackTrace { get; set; }
    }
}
