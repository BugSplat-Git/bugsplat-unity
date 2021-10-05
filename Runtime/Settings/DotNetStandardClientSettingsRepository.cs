using BugSplatDotNetStandard;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace BugSplatUnity.Runtime.Settings
{
    internal class DotNetStandardClientSettingsRepository: IClientSettingsRepository
    {

        public List<FileInfo> Attachments
        {
            get
            {
                return _bugsplat.Attachments;
            }
        }

        public bool CaptureEditorLog { get; set; } = false;

        public bool CapturePlayerLog { get; set; } = true;

        public bool CaptureScreenshots { get; set; } = false;

        public Func<Exception, bool> ShouldPostException { get; set; } = ShouldPostExceptionImpl.DefaultShouldPostExceptionImpl;

        public string Description
        {
            get
            {
                return _bugsplat.Description;
            }
            set
            {
                _bugsplat.Description = value;
            }
        }

        public string Email
        {
            get
            {
                return _bugsplat.Email;
            }
            set
            {
                _bugsplat.Email = value;
            }
        }

        public string Key
        {
            get
            {
                return _bugsplat.Key;
            }
            set
            {
                _bugsplat.Key = value;
            }
        }

        public string User
        {
            get
            {
                return _bugsplat.User;
            }
            set
            {
                _bugsplat.User = value;
            }
        }

        protected readonly BugSplatDotNetStandard.BugSplat _bugsplat;

        public DotNetStandardClientSettingsRepository(BugSplatDotNetStandard.BugSplat bugsplat)
        {
            _bugsplat = bugsplat;
        }
    }
}
