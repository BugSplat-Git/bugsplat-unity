using System;

namespace Packages.com.bugsplat.unity.Runtime.Client
{
    public class BugSplatClientFactory
    {
        internal static IExceptionClient Create(
            string database,
            string application,
            string version
        )
        {
            

#if !UNITY_WEBGL
            return new BugSplatClient(database, application, version);
#else
            return new BugSplatWebGL(database, application, version);
#endif
        }
    }
}
