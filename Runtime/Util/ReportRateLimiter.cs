using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BugSplatUnity.Runtime.Util
{
    internal sealed class ReportRateLimiter
    {
        internal static readonly TimeSpan DefaultDuplicateWindow = TimeSpan.FromSeconds(10);
        internal static readonly TimeSpan DefaultGlobalWindow = TimeSpan.FromSeconds(60);
        internal const int DefaultMaxReportsPerGlobalWindow = 20;
        internal const int DefaultMaxFingerprints = 256;

        private sealed class Entry
        {
            public bool Posted;
            public TimeSpan LastPost;
            public TimeSpan LastSeen;
            public int Suppressed;
        }

        private readonly object gate = new object();
        private readonly Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();
        private readonly Queue<TimeSpan> recentPosts = new Queue<TimeSpan>();
        private readonly Func<TimeSpan> clock;
        private readonly TimeSpan duplicateWindow;
        private readonly TimeSpan globalWindow;
        private readonly int maxReportsPerGlobalWindow;
        private readonly int maxFingerprints;

        public ReportRateLimiter(
            Func<TimeSpan> clock = null,
            TimeSpan? duplicateWindow = null,
            TimeSpan? globalWindow = null,
            int maxReportsPerGlobalWindow = DefaultMaxReportsPerGlobalWindow,
            int maxFingerprints = DefaultMaxFingerprints)
        {
            this.clock = clock ?? MonotonicNow;
            this.duplicateWindow = duplicateWindow ?? DefaultDuplicateWindow;
            this.globalWindow = globalWindow ?? DefaultGlobalWindow;
            this.maxReportsPerGlobalWindow = maxReportsPerGlobalWindow;
            this.maxFingerprints = maxFingerprints;
        }

        internal int FingerprintCount
        {
            get
            {
                lock (gate)
                {
                    return entries.Count;
                }
            }
        }

        public bool TryPost(string fingerprint, out int suppressedCount)
        {
            var key = Hash(fingerprint ?? string.Empty);

            lock (gate)
            {
                var now = clock();

                if (!entries.TryGetValue(key, out var entry))
                {
                    if (entries.Count >= maxFingerprints)
                    {
                        EvictLeastRecentlySeen();
                    }

                    entry = new Entry();
                    entries[key] = entry;
                }

                entry.LastSeen = now;

                while (recentPosts.Count > 0 && now - recentPosts.Peek() >= globalWindow)
                {
                    recentPosts.Dequeue();
                }

                var duplicate = entry.Posted && now - entry.LastPost < duplicateWindow;
                if (duplicate || recentPosts.Count >= maxReportsPerGlobalWindow)
                {
                    if (entry.Suppressed < int.MaxValue)
                    {
                        entry.Suppressed++;
                    }

                    suppressedCount = entry.Suppressed;
                    return false;
                }

                suppressedCount = entry.Suppressed;
                entry.Suppressed = 0;
                entry.Posted = true;
                entry.LastPost = now;
                recentPosts.Enqueue(now);
                return true;
            }
        }

        public void Reset()
        {
            lock (gate)
            {
                entries.Clear();
                recentPosts.Clear();
            }
        }

        private void EvictLeastRecentlySeen()
        {
            var oldestKey = default(ulong);
            Entry oldest = null;

            foreach (var pair in entries)
            {
                if (oldest == null || pair.Value.LastSeen < oldest.LastSeen)
                {
                    oldestKey = pair.Key;
                    oldest = pair.Value;
                }
            }

            if (oldest != null)
            {
                entries.Remove(oldestKey);
            }
        }

        private static TimeSpan MonotonicNow()
        {
            var timestamp = Stopwatch.GetTimestamp();
            return TimeSpan.FromTicks((long)(timestamp * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
        }

        private static ulong Hash(string value)
        {
            var hash = 14695981039346656037UL;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 1099511628211UL;
            }
            return hash;
        }
    }

    internal static class ReportFingerprint
    {
        internal const int MaxFrames = 32;

        private static readonly string[] FrameLocationMarkers = { " [0x", " (at ", " in ", " <0x" };

        public static string FromException(Exception exception)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            return Build(exception.GetType().FullName, exception.Message, exception.StackTrace);
        }

        public static string FromLogMessage(string logMessage, string stackTrace)
        {
            var firstLine = FirstLine(logMessage ?? string.Empty);
            var colon = firstLine.IndexOf(':');
            var hasTypePrefix = colon > 0 && firstLine.IndexOf(' ', 0, colon) < 0;
            var type = hasTypePrefix ? firstLine.Substring(0, colon) : WithoutNumbers(firstLine);
            var message = hasTypePrefix ? firstLine.Substring(colon + 1) : firstLine;

            return Build(type, message, stackTrace);
        }

        private static string Build(string type, string message, string stackTrace)
        {
            var builder = new StringBuilder(type);
            var frames = 0;
            string previous = null;

            foreach (var line in (stackTrace ?? string.Empty).Split('\n'))
            {
                var frame = MethodOf(line);
                if (frame.Length == 0 || frame == previous)
                {
                    continue;
                }

                builder.Append('\n').Append(frame);
                previous = frame;

                if (++frames == MaxFrames)
                {
                    break;
                }
            }

            if (frames == 0)
            {
                builder.Append('\n').Append(WithoutNumbers(message ?? string.Empty));
            }

            return builder.ToString();
        }

        private static string MethodOf(string line)
        {
            var frame = line.Trim();
            if (frame.StartsWith("at ", StringComparison.Ordinal))
            {
                frame = frame.Substring(3);
            }

            foreach (var marker in FrameLocationMarkers)
            {
                var index = frame.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0)
                {
                    frame = frame.Substring(0, index);
                }
            }

            return frame.TrimEnd();
        }

        private static string FirstLine(string value)
        {
            var newline = value.IndexOfAny(new[] { '\r', '\n' });
            return newline < 0 ? value : value.Substring(0, newline);
        }

        private static string WithoutNumbers(string value)
        {
            var builder = new StringBuilder(value.Length);
            var i = 0;

            while (i < value.Length)
            {
                if (!Uri.IsHexDigit(value[i]))
                {
                    builder.Append(value[i++]);
                    continue;
                }

                var start = i;
                var hasDigit = false;
                while (i < value.Length && Uri.IsHexDigit(value[i]))
                {
                    hasDigit |= char.IsDigit(value[i]);
                    i++;
                }

                if (hasDigit)
                {
                    builder.Append('#');
                }
                else
                {
                    builder.Append(value, start, i - start);
                }
            }

            return builder.ToString().Trim();
        }
    }
}
