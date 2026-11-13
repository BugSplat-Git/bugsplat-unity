using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BugSplatUnity.RuntimeTests.Util
{
    // Most tests drive a private ReportRateLimiter on a fake clock, so they neither sleep nor share
    // state. The few that go through the static default guard reset its shared limiter around
    // every test.
    public class ShouldPostExceptionImplTests
    {
        static readonly TimeSpan Window = ReportRateLimiter.DefaultDuplicateWindow;
        static readonly TimeSpan GlobalWindow = ReportRateLimiter.DefaultGlobalWindow;
        const int GlobalCap = ReportRateLimiter.DefaultMaxReportsPerGlobalWindow;

        TimeSpan now;
        ReportRateLimiter limiter;

        [SetUp]
        public void SetUp()
        {
            now = TimeSpan.FromHours(1);
            limiter = new ReportRateLimiter(() => now);
            ShouldPostExceptionImpl.Limiter.Reset();
        }

        [TearDown]
        public void TearDown() => ShouldPostExceptionImpl.Limiter.Reset();

        bool TryPost(string fingerprint) => limiter.TryPost(fingerprint, out _);

        [Test]
        public void TryPost_FirstReportOfAFingerprint_ShouldPostWithNoSuppressedCount()
        {
            Assert.True(limiter.TryPost("a", out var suppressed));
            Assert.AreEqual(0, suppressed);
        }

        [Test]
        public void TryPost_DistinctFingerprintsInsideTheWindow_ShouldBothPost()
        {
            Assert.True(TryPost("a"));
            Assert.True(TryPost("b"));
        }

        [Test]
        public void TryPost_SameFingerprintInsideTheWindow_ShouldBeSuppressed()
        {
            Assert.True(TryPost("a"));
            now += Window - TimeSpan.FromMilliseconds(1);

            Assert.False(TryPost("a"));
        }

        [Test]
        public void TryPost_SameFingerprintOnceTheWindowHasElapsed_ShouldPost()
        {
            Assert.True(TryPost("a"));
            now += Window;

            Assert.True(TryPost("a"));
        }

        [Test]
        public void TryPost_NextPostAfterSuppression_ShouldCarryTheSuppressedCountOnce()
        {
            TryPost("a");
            Assert.False(limiter.TryPost("a", out var first));
            Assert.False(limiter.TryPost("a", out var second));
            Assert.False(limiter.TryPost("a", out var third));
            Assert.AreEqual(new[] { 1, 2, 3 }, new[] { first, second, third });

            now += Window;
            Assert.True(limiter.TryPost("a", out var carried));
            Assert.AreEqual(3, carried);

            now += Window;
            Assert.True(limiter.TryPost("a", out var afterwards));
            Assert.AreEqual(0, afterwards);
        }

        [Test]
        public void TryPost_SuppressedCount_ShouldBeKeptPerFingerprint()
        {
            TryPost("a");
            TryPost("b");
            TryPost("a");
            TryPost("a");
            TryPost("b");

            now += Window;
            limiter.TryPost("a", out var a);
            limiter.TryPost("b", out var b);

            Assert.AreEqual(2, a);
            Assert.AreEqual(1, b);
        }

        // Measured from the last post, not the last attempt, so an exception thrown every frame
        // still gets one report per window rather than none.
        [Test]
        public void TryPost_WhenSuppressed_ShouldNotExtendTheWindow()
        {
            TryPost("a");
            for (var i = 0; i < 9; i++)
            {
                now += TimeSpan.FromSeconds(1);
                Assert.False(TryPost("a"));
            }

            now += TimeSpan.FromSeconds(1);
            Assert.True(TryPost("a"));
        }

        [Test]
        public void TryPost_MoreDistinctFingerprintsThanTheGlobalCap_ShouldSuppressTheExcess()
        {
            for (var i = 0; i < GlobalCap; i++)
            {
                Assert.True(TryPost($"distinct {i}"), $"report {i}");
            }

            Assert.False(TryPost("one too many"));
        }

        [Test]
        public void TryPost_OnceTheGlobalWindowHasElapsed_ShouldPostAgainWithTheCountItMissed()
        {
            for (var i = 0; i < GlobalCap; i++)
            {
                TryPost($"distinct {i}");
            }
            TryPost("late");
            TryPost("late");

            now += GlobalWindow;

            Assert.True(limiter.TryPost("late", out var suppressed));
            Assert.AreEqual(2, suppressed);
        }

        [Test]
        public void TryPost_GlobalCap_ShouldBeASlidingWindow()
        {
            TryPost("early");
            now += TimeSpan.FromSeconds(30);
            for (var i = 1; i < GlobalCap; i++)
            {
                TryPost($"distinct {i}");
            }
            Assert.False(TryPost("blocked"));

            now += TimeSpan.FromSeconds(30);

            Assert.True(TryPost("one slot freed"));
            Assert.False(TryPost("still full"));
        }

        [Test]
        public void TryPost_MoreFingerprintsThanTheTable_ShouldStayBoundedAndEvictTheLeastRecentlySeen()
        {
            const int max = 8;
            var small = new ReportRateLimiter(() => now, maxReportsPerGlobalWindow: int.MaxValue, maxFingerprints: max);

            for (var i = 0; i < max; i++)
            {
                small.TryPost($"fingerprint {i}", out _);
                now += TimeSpan.FromMilliseconds(1);
            }
            small.TryPost("fingerprint 0", out _);

            for (var i = max; i < max * 4; i++)
            {
                small.TryPost($"fingerprint {i}", out _);
                Assert.LessOrEqual(small.FingerprintCount, max);
            }

            Assert.AreEqual(max, small.FingerprintCount);
        }

        [Test]
        public void TryPost_WhenTheTableIsFull_ShouldKeepTheMostRecentlySeenFingerprint()
        {
            var small = new ReportRateLimiter(() => now, maxReportsPerGlobalWindow: int.MaxValue, maxFingerprints: 2);

            small.TryPost("old", out _);
            now += TimeSpan.FromMilliseconds(1);
            small.TryPost("recent", out _);
            now += TimeSpan.FromMilliseconds(1);
            small.TryPost("old", out _);
            now += TimeSpan.FromMilliseconds(1);
            small.TryPost("new", out _);

            Assert.False(small.TryPost("old", out _), "old was seen most recently, so it should still be tracked");
            Assert.True(small.TryPost("recent", out _), "recent was the least recently seen, so it should have been evicted");
        }

        [Test]
        public void Reset_ShouldForgetEveryFingerprintAndTheGlobalCap()
        {
            for (var i = 0; i < GlobalCap; i++)
            {
                TryPost($"distinct {i}");
            }

            limiter.Reset();

            Assert.AreEqual(0, limiter.FingerprintCount);
            Assert.True(TryPost("distinct 0"));
        }

        [Test]
        public void TryPost_WhenOneFingerprintIsReportedConcurrently_ShouldPostExactlyOnce()
        {
            var allowed = RunConcurrently(16, _ => TryPost("same"));

            Assert.AreEqual(1, allowed);
        }

        [Test]
        public void TryPost_WhenDistinctFingerprintsAreReportedConcurrently_ShouldPostEachUpToTheGlobalCap()
        {
            Assert.AreEqual(GlobalCap / 2, RunConcurrently(GlobalCap / 2, i => TryPost($"distinct {i}")));

            limiter.Reset();

            Assert.AreEqual(GlobalCap, RunConcurrently(GlobalCap * 4, i => TryPost($"distinct {i}")));
        }

        [Test]
        public void TryPost_UnderConcurrentChurn_ShouldKeepTheTableBounded()
        {
            const int max = 16;
            var small = new ReportRateLimiter(() => now, maxFingerprints: max);

            RunConcurrently(8, thread =>
            {
                for (var i = 0; i < 500; i++)
                {
                    small.TryPost($"{thread}:{i}", out _);
                }
                return false;
            });

            Assert.AreEqual(max, small.FingerprintCount);
        }

        static int RunConcurrently(int count, Func<int, bool> body)
        {
            var allowed = 0;
            var start = new ManualResetEventSlim(false);
            var threads = new Thread[count];

            for (var i = 0; i < count; i++)
            {
                var index = i;
                threads[i] = new Thread(() =>
                {
                    start.Wait();
                    if (body(index))
                    {
                        Interlocked.Increment(ref allowed);
                    }
                });
                threads[i].Start();
            }

            start.Set();

            foreach (var thread in threads)
            {
                thread.Join();
            }

            return allowed;
        }

        // The default guard and the Play-mode reset share one process-wide limiter.

        static bool ShouldPost(Exception ex = null) => ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl(ex);

        [Test]
        public void DefaultShouldPostExceptionImpl_DistinctExceptionsInsideTheWindow_ShouldBothPost()
        {
            Assert.True(ShouldPost(Capture(ThrowFromA)));
            Assert.True(ShouldPost(Capture(ThrowFromB)));
        }

        [Test]
        public void DefaultShouldPostExceptionImpl_SameExceptionInsideTheWindow_ShouldBeSuppressed()
        {
            Assert.True(ShouldPost(Capture(ThrowFromA)));
            Assert.False(ShouldPost(Capture(ThrowFromA)));
        }

        [Test]
        public void DefaultShouldPostExceptionImpl_WhenExceptionIsNull_ShouldBeRateLimitedLikeAnyOther()
        {
            Assert.True(ShouldPost(null));
            Assert.False(ShouldPost(null));
        }

        [Test]
        public void DefaultShouldPostExceptionImpl_WhenReachedThroughSeparateDelegates_ShouldShareOneLimiter()
        {
            Func<Exception, bool> firstClient = ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl;
            Func<Exception, bool> secondClient = ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl;
            var exception = Capture(ThrowFromA);

            Assert.True(firstClient(exception));
            Assert.False(secondClient(exception));
        }

        // Enter Play Mode without a domain reload keeps statics, so the SubsystemRegistration hook
        // must clear the limiter or the previous play's last report would block this play's first.
        [Test]
        public void ResetStatics_AfterAPostInsideTheWindow_ShouldAllowTheNextPost()
        {
            var exception = Capture(ThrowFromA);
            ShouldPost(exception);

            var resetStatics = typeof(ShouldPostExceptionImpl).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(resetStatics.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>());
            resetStatics.Invoke(null, null);

            Assert.True(ShouldPost(exception));
        }

        [Test]
        public void IsDefault_ShouldRecognizeTheDefaultGuardButNotACustomOne()
        {
            Assert.True(ShouldPostExceptionImpl.IsDefault(new WebGLClientSettingsRepository().ShouldPostException));
            Assert.True(ShouldPostExceptionImpl.IsDefault(ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl));
            Assert.False(ShouldPostExceptionImpl.IsDefault(ex => true));
            Assert.False(ShouldPostExceptionImpl.IsDefault(null));
        }

        // Fingerprints

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowFromA() => throw new InvalidOperationException("from A");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowFromB() => throw new InvalidOperationException("from B");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowWithMessage(string message) => throw new InvalidOperationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowArgumentWithMessage(string message) => throw new ArgumentException(message);

        static Exception Capture(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                return ex;
            }
            throw new AssertionException("action did not throw");
        }

        static Exception CaptureWithMessage(string message) => Capture(() => ThrowWithMessage(message));

        static Exception CaptureArgumentWithMessage(string message) => Capture(() => ThrowArgumentWithMessage(message));

        [Test]
        public void FromException_SameThrowSiteWithDifferentMessages_ShouldMatch()
        {
            Assert.AreEqual(
                ReportFingerprint.FromException(CaptureWithMessage("Enemy 41 has no target")),
                ReportFingerprint.FromException(CaptureWithMessage("Something else entirely")));
        }

        [Test]
        public void FromException_DifferentThrowSites_ShouldDiffer()
        {
            Assert.AreNotEqual(
                ReportFingerprint.FromException(Capture(ThrowFromA)),
                ReportFingerprint.FromException(Capture(ThrowFromB)));
        }

        [Test]
        public void FromException_DifferentTypesWithTheSameMessage_ShouldDiffer()
        {
            Assert.AreNotEqual(
                ReportFingerprint.FromException(CaptureWithMessage("same")),
                ReportFingerprint.FromException(CaptureArgumentWithMessage("same")));
        }

        // An exception that was never thrown has no stack, so its message is all that tells it
        // apart; numbers in it are masked so a counter or id cannot mint a new fingerprint.
        [Test]
        public void FromException_WithoutAStack_ShouldUseTheMessageWithNumbersMasked()
        {
            Assert.AreEqual(
                ReportFingerprint.FromException(new Exception("Enemy 41 at 0x7ffe12 failed")),
                ReportFingerprint.FromException(new Exception("Enemy 42 at 0x7ffe40 failed")));
            Assert.AreNotEqual(
                ReportFingerprint.FromException(new Exception("Save failed")),
                ReportFingerprint.FromException(new Exception("Load failed")));
        }

        const string UnityStack =
            "Crasher.Enemy.Update () (at Assets/Scripts/Enemy.cs:42)\n" +
            "UnityEngine.Debug:LogException(Exception)\n";

        [Test]
        public void FromLogMessage_SameTypeAndStackWithDifferentMessages_ShouldMatch()
        {
            Assert.AreEqual(
                ReportFingerprint.FromLogMessage("NullReferenceException: Enemy 41 has no target", UnityStack),
                ReportFingerprint.FromLogMessage("NullReferenceException: Enemy 977 has no target", UnityStack));
        }

        [Test]
        public void FromLogMessage_DifferentTypes_ShouldDiffer()
        {
            Assert.AreNotEqual(
                ReportFingerprint.FromLogMessage("NullReferenceException: x", UnityStack),
                ReportFingerprint.FromLogMessage("InvalidOperationException: x", UnityStack));
        }

        [Test]
        public void FromLogMessage_DifferentStacks_ShouldDiffer()
        {
            Assert.AreNotEqual(
                ReportFingerprint.FromLogMessage("NullReferenceException: x", UnityStack),
                ReportFingerprint.FromLogMessage("NullReferenceException: x", "Crasher.Player.Update () (at Assets/Scripts/Player.cs:7)\n"));
        }

        [Test]
        public void FromLogMessage_DependsOnTheMethodNotTheLineOrBuild()
        {
            Assert.AreEqual(
                ReportFingerprint.FromLogMessage("NullReferenceException: x", "Crasher.Enemy.Update () (at Assets/Scripts/Enemy.cs:42)"),
                ReportFingerprint.FromLogMessage("NullReferenceException: x", "Crasher.Enemy.Update ()"));
        }

        [Test]
        public void FromLogMessage_RecursionDepth_ShouldNotChangeTheFingerprint()
        {
            const string frame = "Crasher.Tree.Walk (Node)\n";
            Assert.AreEqual(
                ReportFingerprint.FromLogMessage("StackOverflowException: x", frame + frame + "Crasher.Tree.Start ()"),
                ReportFingerprint.FromLogMessage("StackOverflowException: x", frame + frame + frame + frame + "Crasher.Tree.Start ()"));
        }

        [Test]
        public void FromLogMessage_WithoutAStack_ShouldUseTheMessageWithNumbersMasked()
        {
            Assert.AreEqual(
                ReportFingerprint.FromLogMessage("Exception: retry 3 of 5", ""),
                ReportFingerprint.FromLogMessage("Exception: retry 4 of 5", null));
            Assert.AreNotEqual(
                ReportFingerprint.FromLogMessage("Exception: save failed", ""),
                ReportFingerprint.FromLogMessage("Exception: load failed", ""));
        }
    }
}
