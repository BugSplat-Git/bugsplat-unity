using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace BugSplatUnity.RuntimeTests.Util
{
    // SetNullOrEmptyValues is the merge step between the per-report options a caller passes to
    // Post and the client-wide defaults on the settings repository. These tests pin what it does
    // today, including behaviour that is arguably wrong; the comments mark which is which.
    public class ExceptionPostOptionsExtensionsTests
    {
        static readonly FileInfo FirstAttachment = new FileInfo("first.txt");
        static readonly FileInfo SecondAttachment = new FileInfo("second.txt");

        static FakeClientSettingsRepository CreateClientSettings() => new FakeClientSettingsRepository
        {
            Description = "client description",
            Email = "client@bugsplat.com",
            Key = "client key",
            Notes = "client notes",
            User = "client user"
        };

        [Test]
        public void SetNullOrEmptyValues_WhenOptionsValuesAreNull_ShouldCopyClientSettings()
        {
            var options = new ReportPostOptions();
            var clientSettings = CreateClientSettings();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual("client description", options.Description);
            Assert.AreEqual("client@bugsplat.com", options.Email);
            Assert.AreEqual("client key", options.Key);
            Assert.AreEqual("client notes", options.Notes);
            Assert.AreEqual("client user", options.User);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenOptionsValuesAreEmpty_ShouldCopyClientSettings()
        {
            var options = new ReportPostOptions
            {
                Description = string.Empty,
                Email = string.Empty,
                Key = string.Empty,
                Notes = string.Empty,
                User = string.Empty
            };
            var clientSettings = CreateClientSettings();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual("client description", options.Description);
            Assert.AreEqual("client@bugsplat.com", options.Email);
            Assert.AreEqual("client key", options.Key);
            Assert.AreEqual("client notes", options.Notes);
            Assert.AreEqual("client user", options.User);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenOptionsValuesAreSet_ShouldKeepThem()
        {
            var options = new ReportPostOptions
            {
                Description = "options description",
                Email = "options@bugsplat.com",
                Key = "options key",
                Notes = "options notes",
                User = "options user"
            };
            var clientSettings = CreateClientSettings();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual("options description", options.Description);
            Assert.AreEqual("options@bugsplat.com", options.Email);
            Assert.AreEqual("options key", options.Key);
            Assert.AreEqual("options notes", options.Notes);
            Assert.AreEqual("options user", options.User);
        }

        // The guard is string.IsNullOrEmpty, not IsNullOrWhiteSpace, so a whitespace-only value
        // counts as deliberately set and suppresses the client default. The report then carries a
        // field that looks blank in the BugSplat UI. Pinned, not endorsed.
        [Test]
        public void SetNullOrEmptyValues_WhenOptionsValueIsWhitespace_ShouldKeepTheWhitespace()
        {
            var options = new ReportPostOptions { User = "   " };
            var clientSettings = CreateClientSettings();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual("   ", options.User);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenClientSettingsValuesAreNull_ShouldLeaveOptionsUnset()
        {
            var options = new ReportPostOptions();
            var clientSettings = new FakeClientSettingsRepository();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.IsNull(options.Description);
            Assert.IsNull(options.Email);
            Assert.IsNull(options.Key);
            Assert.IsNull(options.Notes);
            Assert.IsNull(options.User);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenClientSettingsHasAttachments_ShouldAddThem()
        {
            var options = new ReportPostOptions();
            var clientSettings = new FakeClientSettingsRepository
            {
                Attachments = new List<FileInfo> { FirstAttachment, SecondAttachment }
            };

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual(2, options.AdditionalAttachments.Count);
            Assert.Contains(FirstAttachment, options.AdditionalAttachments);
            Assert.Contains(SecondAttachment, options.AdditionalAttachments);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenOptionsAlreadyHasAttachments_ShouldAppendNotReplace()
        {
            var options = new ReportPostOptions();
            options.AdditionalAttachments.Add(FirstAttachment);
            var clientSettings = new FakeClientSettingsRepository
            {
                Attachments = new List<FileInfo> { SecondAttachment }
            };

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual(2, options.AdditionalAttachments.Count);
            Assert.AreSame(FirstAttachment, options.AdditionalAttachments[0]);
            Assert.AreSame(SecondAttachment, options.AdditionalAttachments[1]);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenClientSettingsHasNoAttachments_ShouldNotAddAny()
        {
            var options = new ReportPostOptions();
            var clientSettings = new FakeClientSettingsRepository();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.IsEmpty(options.AdditionalAttachments);
        }

        // Nothing dedupes, so applying the same client settings to the same options twice uploads
        // every client attachment twice. Callers only get away with it because the manager builds
        // fresh options per report. Pinned, not endorsed.
        [Test]
        public void SetNullOrEmptyValues_WhenCalledTwice_ShouldDuplicateAttachments()
        {
            var options = new ReportPostOptions();
            var clientSettings = new FakeClientSettingsRepository
            {
                Attachments = new List<FileInfo> { FirstAttachment }
            };

            options.SetNullOrEmptyValues(clientSettings);
            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual(2, options.AdditionalAttachments.Count);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenClientSettingsHasAttributes_ShouldAddThem()
        {
            var options = new ReportPostOptions();
            var clientSettings = new FakeClientSettingsRepository
            {
                Attributes = new Dictionary<string, string> { { "level", "9" } }
            };

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual("9", options.AdditionalAttributes["level"]);
        }

        // TryAdd, not an assignment, so the per-report attribute wins over the client-wide one.
        [Test]
        public void SetNullOrEmptyValues_WhenAttributeKeyAlreadyExists_ShouldKeepTheOptionsValue()
        {
            var options = new ReportPostOptions();
            options.AdditionalAttributes["level"] = "options";
            var clientSettings = new FakeClientSettingsRepository
            {
                Attributes = new Dictionary<string, string> { { "level", "client" } }
            };

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual(1, options.AdditionalAttributes.Count);
            Assert.AreEqual("options", options.AdditionalAttributes["level"]);
        }

        [Test]
        public void SetNullOrEmptyValues_WhenClientSettingsHasNoAttributes_ShouldLeaveOptionsAttributesAlone()
        {
            var options = new ReportPostOptions();
            options.AdditionalAttributes["level"] = "options";
            var clientSettings = new FakeClientSettingsRepository();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual(1, options.AdditionalAttributes.Count);
            Assert.AreEqual("options", options.AdditionalAttributes["level"]);
        }

        // Attributes is only null if a repository implementation forgets to initialize it, and the
        // foreach dereferences it unguarded. Documented so the redesign of this method knows the
        // failure mode is an unhandled NullReferenceException inside the report path, not a
        // report that goes out without attributes. Pinned, not endorsed.
        [Test]
        public void SetNullOrEmptyValues_WhenClientSettingsAttributesIsNull_ShouldThrow()
        {
            var options = new ReportPostOptions();
            var clientSettings = new FakeClientSettingsRepository { Attributes = null };

            Assert.Throws<NullReferenceException>(() => options.SetNullOrEmptyValues(clientSettings));
        }

        [Test]
        public void SetNullOrEmptyValues_ShouldNotTouchCrashTypeIdOrFormDataParams()
        {
            var options = new ReportPostOptions { CrashTypeId = 42 };
            options.AdditionalFormDataParams.Add(new FormDataParam { Name = "param" });
            var clientSettings = CreateClientSettings();

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual(42, options.CrashTypeId);
            Assert.AreEqual(1, options.AdditionalFormDataParams.Count);
            Assert.AreEqual("param", options.AdditionalFormDataParams[0].Name);
        }

        class FakeClientSettingsRepository : IClientSettingsRepository
        {
            public List<FileInfo> Attachments { get; set; } = new List<FileInfo>();
            public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
            public bool CaptureEditorLog { get; set; }
            public bool CapturePlayerLog { get; set; }
            public bool CaptureScreenshots { get; set; }
            public bool PostExceptionsInEditor { get; set; }
            public int LogFileMaxSizeMB { get; set; }
            public Func<Exception, bool> ShouldPostException { get; set; }
            public string Description { get; set; }
            public string Email { get; set; }
            public string Key { get; set; }
            public string Notes { get; set; }
            public string User { get; set; }
        }
    }
}
