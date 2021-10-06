using BugSplatDotNetStandard;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.Runtime.Client
{
    internal class DotNetStandardClient : INativeCrashReportClient, IExceptionClient<Task<HttpResponseMessage>>
    {
        private readonly BugSplatDotNetStandard.BugSplat _bugsplat;

        public DotNetStandardClient(BugSplatDotNetStandard.BugSplat bugsplat)
        {
            _bugsplat = bugsplat;
        }

        public Task<HttpResponseMessage> Post(string stackTrace, IExceptionPostOptions options = null)
        {
            return _bugsplat.Post(stackTrace, (ExceptionPostOptions)options);
        }

        public Task<HttpResponseMessage> Post(Exception ex, IExceptionPostOptions options = null)
        {
            return _bugsplat.Post(ex, (ExceptionPostOptions)options);
        }

        public Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, MinidumpPostOptions options = null)
        {
            return _bugsplat.Post(minidumpFileInfo, options);
        }
    }
}
