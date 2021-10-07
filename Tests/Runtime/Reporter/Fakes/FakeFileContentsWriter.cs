using BugSplatUnity.Runtime.Util;
using System.Collections.Generic;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    class FakeFileContentsWriter : IFileContentsWriter
    {
        public List<FakeFileContentsWriterWriteAllTextCall> Calls { get; } = new List<FakeFileContentsWriterWriteAllTextCall>();

        public void WriteAllText(string path, string contents)
        {
            Calls.Add(
                new FakeFileContentsWriterWriteAllTextCall()
                {
                    Path = path,
                    Contents = contents
                }
            );
        }
    }

    class FakeFileContentsWriterWriteAllTextCall
    {
        public string Path { get; set; }
        public string Contents { get; set; }
    }
}
