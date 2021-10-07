using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace BugSplatUnity.Runtime.Reporter
{

    interface INativeCrashReporter
    {
        IEnumerator PostAllCrashes(IReportPostOptions options = null, Action<List<HttpResponseMessage>> callback = null);
        IEnumerator PostCrash(IDirectoryInfo crashFolder, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null);
        IEnumerator PostMostRecentCrash(IReportPostOptions options = null, Action<HttpResponseMessage> callback = null);
        IEnumerator Post(FileInfo minidump, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null);
    }
}
