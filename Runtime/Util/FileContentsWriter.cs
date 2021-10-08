using System.IO;

namespace BugSplatUnity.Runtime.Util
{
    interface IFileContentsWriter
    {
        void WriteAllText(string path, string contents);
    }

    class FileContentsWriter : IFileContentsWriter
    {
        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }
    }
}
