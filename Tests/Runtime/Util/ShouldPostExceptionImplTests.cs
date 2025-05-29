using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading;

namespace BugSplatUnity.RuntimeTests.Util
{
    class ShouldPostExceptionImplTests
    {
        [Test]
        public void DefaultShouldPostExceptionImpl_ShouldRespectRateLimiting()
        {
            // Reset internal lastPost field to ensure test independence
            var field = typeof(ShouldPostExceptionImpl).GetField("lastPost", BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, new DateTime(0));

            // First call should be allowed
            Assert.IsTrue(ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl());

            // Second immediate call should be blocked
            Assert.IsFalse(ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl());

            // After waiting for 3 seconds a subsequent call should be allowed
            Thread.Sleep(TimeSpan.FromSeconds(3));
            Assert.IsTrue(ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl());
        }
    }
}
