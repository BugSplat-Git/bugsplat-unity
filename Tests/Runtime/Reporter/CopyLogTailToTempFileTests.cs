using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using System.IO;
using System.Net.Http;
using System.Text;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Reporter
{
	public class CopyLogTailToTempFileTests
	{
		const int OneMegabyte = 1024 * 1024;

		DotNetStandardExceptionReporter sut;
		string workingDirectory;

		[SetUp]
		public void SetUp()
		{
			sut = new DotNetStandardExceptionReporter(
				new WebGLClientSettingsRepository(),
				new FakeDotNetExceptionClient(new HttpResponseMessage()));

			workingDirectory = Path.Combine(Path.GetTempPath(), "bugsplat-log-tail-tests", Path.GetRandomFileName());
			Directory.CreateDirectory(workingDirectory);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(workingDirectory))
			{
				Directory.Delete(workingDirectory, true);
			}
		}

		FileInfo WriteLog(string name, byte[] contents)
		{
			var path = Path.Combine(workingDirectory, name);
			File.WriteAllBytes(path, contents);
			return new FileInfo(path);
		}

		static byte[] Repeating(int length)
		{
			var bytes = new byte[length];
			for (var i = 0; i < length; i++)
			{
				// Position-dependent so a copy taken from the wrong offset is detectable.
				bytes[i] = (byte)(i % 251);
			}
			return bytes;
		}

		[Test]
		public void CopyLogTailToTempFile_WhenFileIsSmallerThanMax_ShouldCopyAllOfIt()
		{
			var contents = Encoding.UTF8.GetBytes("a small log file");
			var log = WriteLog("Player.log", contents);

			var result = sut.CopyLogTailToTempFile(log, 1);

			Assert.NotNull(result);
			Assert.AreEqual(contents.Length, result.Length);
			CollectionAssert.AreEqual(contents, File.ReadAllBytes(result.FullName));
		}

		[Test]
		public void CopyLogTailToTempFile_WhenFileExceedsMax_ShouldCopyOnlyTheTail()
		{
			var contents = Repeating(2 * OneMegabyte + 4096);
			var log = WriteLog("Player.log", contents);

			var result = sut.CopyLogTailToTempFile(log, 1);

			Assert.NotNull(result);
			Assert.AreEqual(OneMegabyte, result.Length, "should be truncated to exactly the max size");

			var expectedTail = new byte[OneMegabyte];
			System.Array.Copy(contents, contents.Length - OneMegabyte, expectedTail, 0, OneMegabyte);
			CollectionAssert.AreEqual(expectedTail, File.ReadAllBytes(result.FullName), "should be the end of the file, not the start");
		}

		[Test]
		public void CopyLogTailToTempFile_ShouldKeepTheOriginalFileName()
		{
			var log = WriteLog("Editor.log", Encoding.UTF8.GetBytes("contents"));

			var result = sut.CopyLogTailToTempFile(log, 1);

			Assert.AreEqual("Editor.log", result.Name);
		}

		[Test]
		public void CopyLogTailToTempFile_ShouldNotModifyTheSourceFile()
		{
			var contents = Repeating(2 * OneMegabyte);
			var log = WriteLog("Player.log", contents);

			sut.CopyLogTailToTempFile(log, 1);

			Assert.AreEqual(contents.Length, new FileInfo(log.FullName).Length);
		}

		[Test]
		public void CopyLogTailToTempFile_WhenFileDoesNotExist_ShouldReturnNull()
		{
			var missing = new FileInfo(Path.Combine(workingDirectory, "missing.log"));

			LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("does not exist"));

			Assert.Null(sut.CopyLogTailToTempFile(missing, 1));
		}

		[Test]
		public void CopyLogTailToTempFile_WhenFileInfoIsNull_ShouldReturnNull()
		{
			LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("does not exist"));

			Assert.Null(sut.CopyLogTailToTempFile(null, 1));
		}
	}
}
