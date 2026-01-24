using BugSplatUnity;
using NUnit.Framework;

namespace BugSplatUnity.RuntimeTests.Manager
{
    public class BugSplatInstanceTests
    {
        [Test]
        public void Constructor_ShouldSetInstance()
        {
            BugSplat.Instance = null;
            var bugsplat = new BugSplat("database", "application", "version", false, false);
            Assert.AreEqual(bugsplat, BugSplat.Instance);
        }
    }
}
