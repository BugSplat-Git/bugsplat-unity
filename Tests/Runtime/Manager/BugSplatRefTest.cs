using NUnit.Framework;
using System;

namespace BugSplatUnity.RuntimeTests.Manager
{
    public class BugSplatRefTest
    {
        [Test]
        public void Constructor_WhenBugSplatArgIsNull_ShouldThrowArgumentException()
        {

            try
            {
                var bugsplatRef = new BugSplatRef(null);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.True(ex is ArgumentException);
                StringAssert.AreEqualIgnoringCase("BugSplat error: BugSplat instance is null! BugSplatRef will not be initialized.", ex.Message);
            }
        }

        [Test]
        public void Constructor_WhenBugSplatArgIsNotNull_ShouldNotThrowException()
        {
            try
            {
                var bugsplatRef = new BugSplatRef(new BugSplat("database", "application", "version", false, false));
            }
            catch
            {
                Assert.Fail();
            }
        }

        [Test]
        public void Constructor_WhenBugSplatArgIsNotNull_BugSplatPropertyShouldBeNonNull()
        {
            var bugsplatRef = new BugSplatRef(new BugSplat("database", "application", "version", false, false));
            Assert.NotNull(bugsplatRef.BugSplat);
        }
    }
}

