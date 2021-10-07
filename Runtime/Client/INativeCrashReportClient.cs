using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.Runtime.Client
{
    internal interface INativeCrashReportClient
    {
        // TODO BG we're going to need to abstract this a bit to satisfy iOS/Android
        // https://github.com/BugSplat-Git/bugsplat-unity/issues/35
        // https://github.com/BugSplat-Git/bugsplat-unity/issues/36
        Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, IReportPostOptions options = null);
    }
}
