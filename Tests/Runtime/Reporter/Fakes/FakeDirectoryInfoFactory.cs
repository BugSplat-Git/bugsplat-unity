using BugSplatUnity.Runtime.Util;
using System;
using System.IO;

namespace BugSplatUnity.Runtime.Reporter.Fakes
{
    class FakeDirectoryInfoFactory : IDirectoryInfoFactory
    {
        private IDirectoryInfo _directory;

        public FakeDirectoryInfoFactory(IDirectoryInfo directory)
        {
            _directory = directory;
        }

        public IDirectoryInfo CreateDirectoryInfo(string path)
        {
            return _directory;
        }
    }

    class FakeDirectoryInfo : IDirectoryInfo
    {
        public bool Exists { get; set; } = true;
        public string FullName { get; set; } = string.Empty;
        public DateTime LastWriteTime { get; set; } = DateTime.Now;
        public string Name { get; set; } = string.Empty;

        private IDirectoryInfo[] _directories;
        private FileInfo[] _files;

        public FakeDirectoryInfo(
            IDirectoryInfo[] directories = null,
            FileInfo[] files = null
        )
        {
            _directories = directories;
            _files = files;
        }

        public IDirectoryInfo[] GetDirectories()
        {
            return _directories;
        }

        public FileInfo[] GetFiles()
        {
            return _files;
        }
    }
}
