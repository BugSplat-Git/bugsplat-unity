using BugSplatDotNetStandard;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    internal class DotNetStandardReporter : INativeCrashReporter, IExceptionReporter
    {
        private readonly BugSplat _bugsplat;

        public DotNetStandardReporter(BugSplat bugsplat)
        {
            _bugsplat = bugsplat;
        }

        public Task<HttpResponseMessage> Post(string stackTrace, ExceptionPostOptions options = null)
        {
            return _bugsplat.Post(stackTrace, options);
        }

        public Task<HttpResponseMessage> Post(Exception ex, ExceptionPostOptions options = null)
        {
            return _bugsplat.Post(ex, options);
        }

        public Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, MinidumpPostOptions options = null)
        {
            return _bugsplat.Post(minidumpFileInfo, options);
        }
    }
}
