using System.Runtime.InteropServices;

namespace BugSplatUnity.Runtime.Util
{
    internal static class IOSNativeInteropProxy
    {
        [DllImport("__Internal")]
        public static extern bool Start();
    }
}