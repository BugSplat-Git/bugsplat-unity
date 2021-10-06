using BugSplatUnity.Runtime.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    public class FakeExceptionClient : IExceptionClient<Task<HttpResponseMessage>>
    {
        public List<FakeExceptionClientPostCall> Calls { get; } = new List<FakeExceptionClientPostCall>();

        private readonly HttpResponseMessage _result;

        public FakeExceptionClient(HttpResponseMessage result)
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
    }

    public class FakeExceptionClientPostCall
    {
        public string StackTrace { get; set; }
        public Exception Exception { get; set; }
        public IReportPostOptions Options { get; set; }
    }
}
