using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;
using NUnit.Framework;

namespace BugSplatUnity.RuntimeTests.Client
{
    public class WebAssemblyCrashReporterTests
    {
        [Test]
        public void ConfigJson_ShouldCarryTheCurrentReportFields()
        {
            var settings = new WebGLClientSettingsRepository
            {
                Description = "description",
                Email = "fred@bugsplat.com",
                Key = "key",
                Notes = "notes",
                User = "fred"
            };
            var sut = new WebAssemblyCrashReporter("database", "application", "1.0", settings);

            Assert.AreEqual(
                "{\"database\":\"database\",\"application\":\"application\",\"version\":\"1.0\",\"key\":\"key\",\"user\":\"fred\"," +
                "\"email\":\"fred@bugsplat.com\",\"description\":\"description\",\"notes\":\"notes\"}",
                sut.ConfigJson());

            settings.User = "barney";

            StringAssert.Contains("\"user\":\"barney\"", sut.ConfigJson());
        }

        [Test]
        public void EnableAndDisable_ShouldSetEnabled()
        {
            var sut = new WebAssemblyCrashReporter("database", "application", "1.0", new WebGLClientSettingsRepository());

            Assert.False(sut.Enabled);
            sut.Enable();
            Assert.True(sut.Enabled);
            sut.Disable();
            Assert.False(sut.Enabled);
        }
    }
}
