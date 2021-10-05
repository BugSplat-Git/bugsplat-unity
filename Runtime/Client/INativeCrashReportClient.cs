using BugSplatDotNetStandard;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.Runtime.Client
{
    internal interface INativeCrashReportClient
    {
        // TODO BG we're going to need to abstract this a bit to satisfy iOS/Android
        Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, MinidumpPostOptions options = null);
    }
}
