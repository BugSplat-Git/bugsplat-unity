using BugSplatUnity.Runtime.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    class FakeNativeCrashReportClient : INativeCrashReportClient
    {
        public List<FakeNativeCrashReportClientPostCall> Calls { get; } = new List<FakeNativeCrashReportClientPostCall>();

        private readonly HttpResponseMessage _result;

        public FakeNativeCrashReportClient(HttpResponseMessage result)
        {
            _result = result;
        }

        public Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, IReportPostOptions options = null)
        {
            Calls.Add(
                new FakeNativeCrashReportClientPostCall()
                {
                    MinidumpFileInfo = minidumpFileInfo,
                    Options = options
                }
            );
            return Task.FromResult(_result);
        }
    }

    class FakeNativeCrashReportClientPostCall
    {
        public FileInfo MinidumpFileInfo { get; set; }
        public IReportPostOptions Options { get; set; }
    }
}

