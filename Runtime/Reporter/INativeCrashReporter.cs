using BugSplatDotNetStandard;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    interface INativeCrashReporter
    {
        IEnumerator PostAllCrashes(MinidumpPostOptions options = null, Action<List<HttpResponseMessage>> callback = null);
        IEnumerator PostCrash(DirectoryInfo crashFolder, MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null);
        IEnumerator PostMostRecentCrash(MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null);
        IEnumerator Post(FileInfo minidump, MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null)
    }
}
