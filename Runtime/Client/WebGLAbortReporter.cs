#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BugSplatUnity.Runtime.Util;

namespace BugSplatUnity.Runtime.Client
{
    internal static class WebGLAbortReporter
    {
        [DllImport("__Internal")]
        private static extern void BugSplatWebGL_Install(string configJson);

        internal static void Install(string database, string application, string version, BugSplat bugSplat)
        {
            var config = new Dictionary<string, string>
            {
                { "database", database },
                { "appName", application },
                { "appVersion", version },
                { "appKey", bugSplat.Key },
                { "user", bugSplat.User },
                { "email", bugSplat.Email },
                { "description", bugSplat.Description },
                { "crashTypeId", ((int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity).ToString() },
                { "attributes", AttributesJson.Serialize(bugSplat.Attributes) }
            };

            BugSplatWebGL_Install(AttributesJson.Serialize(config));
        }
    }
}
#endif
