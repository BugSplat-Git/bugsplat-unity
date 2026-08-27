using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading;

namespace BugSplatUnity.RuntimeTests.Util
{
    // The rate limiter's entire state is one private static DateTime shared by every caller in the
    // process, so these tests would otherwise depend on each other and on wall-clock sleeps. Both
    // problems are handled the same way: the field is reset to its initial value before and after
    // every test, and tests that need an aged window write the field instead of sleeping. That
    // keeps the fixture order-independent and leaves no residue for the rest of the suite.
    //
    // These tests pin current behaviour, including behaviour that is arguably wrong. Issue #170
    // redesigns this limiter; the comments below mark what should be allowed to change.
    public class ShouldPostExceptionImplTests
    {
        static readonly FieldInfo LastPostField = typeof(ShouldPostExceptionImpl)
            .GetField("lastPost", BindingFlags.NonPublic | BindingFlags.Static);

        // The field's declared initial value: no report has been posted yet.
        static readonly DateTime NeverPosted = new DateTime(0);

        static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

        static void SetLastPost(DateTime value) => LastPostField.SetValue(null, value);

        static DateTime GetLastPost() => (DateTime)LastPostField.GetValue(null);

        static bool ShouldPost(Exception ex = null) => ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl(ex);

        [OneTimeSetUp]
        public void RequireTheFieldTheseTestsDriveThrough()
        {
            Assert.NotNull(LastPostField, "ShouldPostExceptionImpl.lastPost is gone; these tests need to be rewritten against whatever replaced it");
        }

        [SetUp]
        public void ResetRateLimiterBeforeTest() => SetLastPost(NeverPosted);

        [TearDown]
        public void ResetRateLimiterAfterTest() => SetLastPost(NeverPosted);

        [Test]
        public void DefaultShouldPostExceptionImpl_WhenNothingHasBeenPostedYet_ShouldAllowPost()
        {
            Assert.True(ShouldPost(new Exception("first")));
        }

        [Test]
        public void DefaultShouldPostExceptionImpl_WhenCalledAgainInsideTheWindow_ShouldBlockPost()
        {
            ShouldPost(new Exception("first"));

            Assert.False(ShouldPost(new Exception("second")));
        }

        // The limiter is time-based only: there is no per-signature dedup, so the second call is
        // blocked even though it reports a completely different fault. Worse, the report is
        // dropped rather than deferred, so a distinct exception that happens to land within three
        // seconds of an unrelated one is never sent at all. Pinned, not endorsed - see #170.
        [Test]
        public void DefaultShouldPostExceptionImpl_WhenSecondExceptionIsUnrelated_ShouldStillBlockPost()
        {
            ShouldPost(new InvalidOperationException("first"));

            Assert.False(ShouldPost(new NullReferenceException("unrelated")));
        }

        // The exception argument is never read, so a null exception is not a special case.
        [Test]
        public void DefaultShouldPostExceptionImpl_WhenExceptionIsNull_ShouldFollowTheSameWindow()
        {
            Assert.True(ShouldPost(null));
            Assert.False(ShouldPost(null));
        }

        [Test]
        public void DefaultShouldPostExceptionImpl_WhenWindowHasElapsed_ShouldAllowPost()
        {
            SetLastPost(DateTime.Now - Window - TimeSpan.FromSeconds(1));

            Assert.True(ShouldPost(new Exception()));
        }

        [Test]
        public void DefaultShouldPostExceptionImpl_WhenStillInsideTheWindow_ShouldBlockPost()
        {
            SetLastPost(DateTime.Now - Window + TimeSpan.FromSeconds(1));

            Assert.False(ShouldPost(new Exception()));
        }

        // A blocked attempt is not recorded, so a steady stream of exceptions does not hold the
        // window open forever - one report gets through every three seconds.
        [Test]
        public void DefaultShouldPostExceptionImpl_WhenPostIsBlocked_ShouldNotExtendTheWindow()
        {
            var lastPost = DateTime.Now - TimeSpan.FromSeconds(1);
            SetLastPost(lastPost);

            Assert.False(ShouldPost(new Exception()));
            Assert.AreEqual(lastPost, GetLastPost());
        }

        // The window is measured against DateTime.Now, so the limiter follows local wall-clock
        // time rather than a monotonic clock. A backwards jump - DST, an NTP correction, a user
        // changing the clock - leaves a timestamp in the future and suppresses every report until
        // real time catches up. Pinned, not endorsed.
        [Test]
        public void DefaultShouldPostExceptionImpl_WhenClockJumpsBackwards_ShouldBlockPostUntilTimeCatchesUp()
        {
            SetLastPost(DateTime.Now + TimeSpan.FromHours(1));

            Assert.False(ShouldPost(new Exception()));
        }

        // Every IClientSettingsRepository defaults ShouldPostException to this one static method,
        // so separate BugSplat clients in the same process do not get separate budgets - one
        // client's report silences the other's. Pinned, not endorsed.
        [Test]
        public void DefaultShouldPostExceptionImpl_WhenReachedThroughSeparateDelegates_ShouldShareOneWindow()
        {
            Func<Exception, bool> firstClient = ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl;
            Func<Exception, bool> secondClient = ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl;

            Assert.True(firstClient(new Exception()));
            Assert.False(secondClient(new Exception()));
        }

        // Exceptions are reported from background threads too, and the read-then-write of the
        // shared timestamp is not synchronized, so a simultaneous burst can admit more than one
        // report. How many is a race, so this asserts only what must hold: the burst is not
        // silently swallowed, it cannot exceed the number of callers, and the window is closed
        // afterwards. If #170 adds synchronization, the exact count becomes assertable.
        [Test]
        public void DefaultShouldPostExceptionImpl_WhenCalledConcurrently_ShouldAllowAtLeastOnePostAndThenClose()
        {
            const int callerCount = 8;
            var allowed = 0;
            var start = new ManualResetEventSlim(false);
            var callers = new Thread[callerCount];

            for (var i = 0; i < callerCount; i++)
            {
                callers[i] = new Thread(() =>
                {
                    start.Wait();
                    if (ShouldPost(new Exception()))
                    {
                        Interlocked.Increment(ref allowed);
                    }
                });
                callers[i].Start();
            }

            start.Set();

            foreach (var caller in callers)
            {
                caller.Join();
            }

            Assert.GreaterOrEqual(allowed, 1);
            Assert.LessOrEqual(allowed, callerCount);
            Assert.False(ShouldPost(new Exception()));
        }
    }
}
