using BugSplatDotNetStandard;
using BugSplatUnity.Runtime.Client;
using BugSplatUnity.RuntimeTests.Client.Fakes;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests.Client
{
    public class DotNetStandardClientTests
    {
        [Test]
        public void Post_WithStackTraceNullOptions_ShouldCreateOptions()
        {
            var bugSplat = new FakeBugSplat("db", "app", "1.0");
            var sut = new DotNetStandardClient(bugSplat);

            sut.Post("stackTrace");

            Assert.IsNotEmpty(bugSplat.ExceptionCalls);
            Assert.NotNull(bugSplat.ExceptionCalls[0].Options);
        }

        [Test]
        public void Post_WithExceptionNullOptions_ShouldCreateOptions()
        {
            var bugSplat = new FakeBugSplat("db", "app", "1.0");
            var sut = new DotNetStandardClient(bugSplat);

            sut.Post(new Exception("oops"));

            Assert.IsNotEmpty(bugSplat.ExceptionCalls);
            Assert.NotNull(bugSplat.ExceptionCalls[0].Options);
        }

        [Test]
        public void Post_WithMinidumpNullOptions_ShouldCreateOptions()
        {
            var bugSplat = new FakeBugSplat("db", "app", "1.0");
            var sut = new DotNetStandardClient(bugSplat);
            var file = new FileInfo("test.dmp");

            sut.Post(file);

            Assert.IsNotEmpty(bugSplat.MinidumpCalls);
            Assert.NotNull(bugSplat.MinidumpCalls[0].Options);
        }
    }
}
