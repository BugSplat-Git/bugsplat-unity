using System;
using UnityEngine;

namespace BugSplatUnity.Runtime.Util
{
    public static class ShouldPostExceptionImpl
    {
        internal const string SuppressedReportsAttribute = "SuppressedDuplicateReports";

        internal static readonly ReportRateLimiter Limiter = new ReportRateLimiter();

        private static readonly Func<Exception, bool> DefaultDelegate = DefaultShouldPostExceptionImpl;

        // Enter Play Mode Options without domain reload keeps statics across plays, so a report
        // posted just before leaving Play mode would rate-limit the first one of the next play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Limiter.Reset();
        }

        /// <summary>
        /// The default <see cref="BugSplat.ShouldPostException"/> guard. Posts each distinct exception
        /// (same type and call stack, ignoring the message) at most once every 10 seconds, and no more
        /// than 20 reports in any 60 seconds across all exceptions. Thread-safe; accepts null.
        /// </summary>
        public static bool DefaultShouldPostExceptionImpl(Exception ex = null)
        {
            return ShouldPost(ReportFingerprint.FromException(ex), out _);
        }

        internal static bool IsDefault(Func<Exception, bool> shouldPostException)
        {
            return shouldPostException != null && shouldPostException.Equals(DefaultDelegate);
        }

        internal static bool ShouldPost(string fingerprint, out int suppressedCount)
        {
            if (Limiter.TryPost(fingerprint, out suppressedCount))
            {
                return true;
            }

            if (suppressedCount == 1)
            {
                Debug.Log("BugSplat info: Report rate-limiting triggered, skipping report. Repeats are counted and sent with the next report of this exception.");
            }

            return false;
        }
    }
}
