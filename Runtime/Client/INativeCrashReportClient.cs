using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.Runtime.Client
{
    internal interface INativeCrashReportClient
    {
        Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, IReportPostOptions options = null);
    }
}
