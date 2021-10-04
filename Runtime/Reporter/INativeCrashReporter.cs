using BugSplatDotNetStandard;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    internal interface INativeCrashReporter
    {
        // TODO BG we're going to need to abstract this a bit to satisfy iOS/Android
        Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, MinidumpPostOptions options = null);
    }
}
