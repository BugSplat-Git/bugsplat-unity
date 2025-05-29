using BugSplatDotNetStandard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests.Client.Fakes
{
    class FakeBugSplat : BugSplat
    {
        public List<ExceptionPostCall> ExceptionCalls { get; } = new List<ExceptionPostCall>();
        public List<MinidumpPostCall> MinidumpCalls { get; } = new List<MinidumpPostCall>();

        public FakeBugSplat(string database, string application, string version)
            : base(database, application, version)
        {
        }

        public override Task<HttpResponseMessage> Post(string stackTrace, ExceptionPostOptions options)
        {
            ExceptionCalls.Add(new ExceptionPostCall { StackTrace = stackTrace, Options = options });
            return Task.FromResult(new HttpResponseMessage());
        }

        public override Task<HttpResponseMessage> Post(Exception ex, ExceptionPostOptions options)
        {
            ExceptionCalls.Add(new ExceptionPostCall { Exception = ex, Options = options });
            return Task.FromResult(new HttpResponseMessage());
        }

        public override Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, MinidumpPostOptions options)
        {
            MinidumpCalls.Add(new MinidumpPostCall { MinidumpFileInfo = minidumpFileInfo, Options = options });
            return Task.FromResult(new HttpResponseMessage());
        }
    }

    class ExceptionPostCall
    {
        public string StackTrace { get; set; }
        public Exception Exception { get; set; }
        public ExceptionPostOptions Options { get; set; }
    }

    class MinidumpPostCall
    {
        public FileInfo MinidumpFileInfo { get; set; }
        public MinidumpPostOptions Options { get; set; }
    }
}
