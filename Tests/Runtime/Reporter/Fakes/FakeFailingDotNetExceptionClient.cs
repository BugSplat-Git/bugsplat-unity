using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    /// <summary>
    /// Records posts like <see cref="FakeDotNetExceptionClient"/> but always faults, so the
    /// reporter takes the branch where it logs a diagnostic about its own failure.
    /// </summary>
    class FakeFailingDotNetExceptionClient : IDotNetStandardExceptionClient
    {
        public List<FakeExceptionClientPostCall> Calls { get; } = new List<FakeExceptionClientPostCall>();

        private readonly Exception _exception;

        public FakeFailingDotNetExceptionClient(Exception exception)
        {
            _exception = exception;
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
            return Task.FromException<HttpResponseMessage>(_exception);
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
            return Task.FromException<HttpResponseMessage>(_exception);
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
            return Task.FromException<HttpResponseMessage>(_exception);
        }
    }
}
