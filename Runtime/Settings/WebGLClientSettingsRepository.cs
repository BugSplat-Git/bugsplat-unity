using BugSplatUnity.Runtime.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace BugSplatUnity.Runtime.Settings
{
    internal class WebGLClientSettingsRepository : IClientSettingsRepository
    {
        // TODO BG what do we do about attachments here...
        // https://github.com/BugSplat-Git/bugsplat-unity/issues/37
        public List<FileInfo> Attachments { get; } = new List<FileInfo>();
        public bool CaptureEditorLog { get; set; } = false;
        public bool CapturePlayerLog { get; set; } = false;
        public bool CaptureScreenshots { get; set; } = false;
        public Func<Exception, bool> ShouldPostException { get; set; } = ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl;
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string User { get; set; }
    }
}
