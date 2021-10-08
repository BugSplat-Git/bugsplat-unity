using System;
using System.IO;
using System.Linq;

namespace BugSplatUnity.Runtime.Util
{
    internal interface IDirectoryInfoFactory
    {
        public IDirectoryInfo CreateDirectoryInfo(string path);
    }

    internal interface IDirectoryInfo
    {
        public IDirectoryInfo[] GetDirectories();
        public FileInfo[] GetFiles();
        public bool Exists { get; }
        public string FullName { get; }
        public DateTime LastWriteTime { get; }
        public string Name { get; }
    }

    class WrappedDirectoryInfo : IDirectoryInfo
    {
        public bool Exists
        {
            get
            {
                return _directory.Exists;
            }
        }

        public string FullName
        {
            get
            {
                return _directory.FullName;
            }
        }

        public DateTime LastWriteTime
        {
            get
            {
                return _directory.LastWriteTime;
            }
        }

        public string Name
        {
            get
            {
                return _directory.Name;
            }
        }

        private readonly DirectoryInfo _directory;

        public WrappedDirectoryInfo(DirectoryInfo directory)
        {
            _directory = directory;
        }

        public IDirectoryInfo[] GetDirectories()
        {
            return _directory.GetDirectories()
                .Select(dir => new WrappedDirectoryInfo(dir))
                .ToArray();
        }

        public FileInfo[] GetFiles()
        {
            return _directory.GetFiles();
        }
    }

    class DirectoryInfoFactory : IDirectoryInfoFactory
    {
        public IDirectoryInfo CreateDirectoryInfo(string path)
        {
            return new WrappedDirectoryInfo(new DirectoryInfo(path));
        }
    }
}
