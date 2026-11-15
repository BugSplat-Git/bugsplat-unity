using System;
using UnityEngine;

namespace BugSplatUnity.Runtime.Util
{
    public static class ShouldPostExceptionImpl
    {
        private static DateTime lastPost = new DateTime(0);

        // Enter Play Mode Options without domain reload keeps statics across plays, so a report
        // posted just before leaving Play mode would rate-limit the first one of the next play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lastPost = new DateTime(0);
        }

        public static bool DefaultShouldPostExceptionImpl(Exception ex = null)
        {
            var now = DateTime.Now;

            if (lastPost + TimeSpan.FromSeconds(3) > now)
            {
                Debug.Log("BugSplat info: Report rate-limiting triggered, skipping report...");
                return false;
            }

            lastPost = now;
            return true;
        }
    }
}
