using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace BugSplatUnity.RuntimeTests.Util
{
    public class ReportPostOptionsExtensionsTests
    {
        [Test]
        public void SetNullOrEmptyValues_NullAttachments_ShouldNotThrow()
        {
            var options = new ReportPostOptions();
            var clientSettings = new NullAttachmentsClientSettingsRepository();

            Assert.DoesNotThrow(() => options.SetNullOrEmptyValues(clientSettings));
            Assert.IsEmpty(options.AdditionalAttachments);
        }

        [Test]
        public void SetNullOrEmptyValues_EmptyAttachments_ShouldNotAddAttachments()
        {
            var options = new ReportPostOptions();
            var clientSettings = new NullAttachmentsClientSettingsRepository
            {
                Attachments = new List<FileInfo>()
            };

            options.SetNullOrEmptyValues(clientSettings);

            Assert.IsEmpty(options.AdditionalAttachments);
        }

        [Test]
        public void SetNullOrEmptyValues_PopulatedAttachments_ShouldAddAttachments()
        {
            var attachment = new FileInfo("attachment.txt");
            var options = new ReportPostOptions();
            var clientSettings = new NullAttachmentsClientSettingsRepository
            {
                Attachments = new List<FileInfo> { attachment }
            };

            options.SetNullOrEmptyValues(clientSettings);

            Assert.AreEqual(1, options.AdditionalAttachments.Count);
            Assert.AreSame(attachment, options.AdditionalAttachments[0]);
        }

        [Test]
        public void Attachments_WebGLClientSettingsRepository_ShouldBeInitialized()
        {
            Assert.IsNotNull(new WebGLClientSettingsRepository().Attachments);
        }

        private class NullAttachmentsClientSettingsRepository : IClientSettingsRepository
        {
            public List<FileInfo> Attachments { get; set; }
            public IDictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
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
