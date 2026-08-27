using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using System.Collections.Generic;
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
		List<string> tempCopyDirectories;

		[SetUp]
		public void SetUp()
		{
			sut = new DotNetStandardExceptionReporter(
				new WebGLClientSettingsRepository(),
				new FakeDotNetExceptionClient(new HttpResponseMessage()));

			workingDirectory = Path.Combine(Path.GetTempPath(), "bugsplat-log-tail-tests", Path.GetRandomFileName());
			Directory.CreateDirectory(workingDirectory);
			tempCopyDirectories = new List<string>();
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(workingDirectory))
			{
				Directory.Delete(workingDirectory, true);
			}

			foreach (var tempCopyDirectory in tempCopyDirectories)
			{
				if (Directory.Exists(tempCopyDirectory))
				{
					Directory.Delete(tempCopyDirectory, true);
				}
			}
		}

		// CopyLogTailToTempFile writes each copy to its own generated directory
		// that nothing cleans up in tests, so every non-null result must be
		// tracked for TearDown.
		FileInfo TrackTempCopy(FileInfo tempCopy)
		{
			if (tempCopy != null)
			{
				tempCopyDirectories.Add(tempCopy.DirectoryName);
			}
			return tempCopy;
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

			var result = TrackTempCopy(sut.CopyLogTailToTempFile(log, 1));

			Assert.NotNull(result);
			Assert.AreEqual(contents.Length, result.Length);
			CollectionAssert.AreEqual(contents, File.ReadAllBytes(result.FullName));
		}

		[Test]
		public void CopyLogTailToTempFile_WhenFileExceedsMax_ShouldCopyOnlyTheTail()
		{
			var contents = Repeating(2 * OneMegabyte + 4096);
			var log = WriteLog("Player.log", contents);

			var result = TrackTempCopy(sut.CopyLogTailToTempFile(log, 1));

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

			var result = TrackTempCopy(sut.CopyLogTailToTempFile(log, 1));

			Assert.AreEqual("Editor.log", result.Name);
		}

		[Test]
		public void CopyLogTailToTempFile_ShouldNotModifyTheSourceFile()
		{
			var contents = Repeating(2 * OneMegabyte);
			var log = WriteLog("Player.log", contents);

			TrackTempCopy(sut.CopyLogTailToTempFile(log, 1));

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

		[Test]
		public void AddLogTailAttachment_WhenFileExists_ShouldAddTempFileToAttachmentsAndTempFiles()
		{
			var contents = Encoding.UTF8.GetBytes("a small log file");
			var log = WriteLog("Player.log", contents);
			var options = new ReportPostOptions();
			var tempFiles = new List<FileInfo>();

			sut.AddLogTailAttachment(log, options, tempFiles);
			foreach (var tempFile in tempFiles)
			{
				TrackTempCopy(tempFile);
			}

			Assert.AreEqual(1, options.AdditionalAttachments.Count);
			Assert.AreEqual(1, tempFiles.Count);
			Assert.AreSame(options.AdditionalAttachments[0], tempFiles[0]);
			Assert.AreEqual("Player.log", options.AdditionalAttachments[0].Name);
			CollectionAssert.AreEqual(contents, File.ReadAllBytes(options.AdditionalAttachments[0].FullName));
		}

		[Test]
		public void AddLogTailAttachment_WhenFileDoesNotExist_ShouldNotAddNullToAttachmentsOrTempFiles()
		{
			var missing = new FileInfo(Path.Combine(workingDirectory, "missing.log"));
			var options = new ReportPostOptions();
			var tempFiles = new List<FileInfo>();

			LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("does not exist"));

			sut.AddLogTailAttachment(missing, options, tempFiles);

			Assert.IsEmpty(options.AdditionalAttachments);
			Assert.IsEmpty(tempFiles);
		}

		[Test]
		public void AddLogTailAttachment_WhenFileInfoIsNull_ShouldNotAddNullToAttachmentsOrTempFiles()
		{
			var options = new ReportPostOptions();
			var tempFiles = new List<FileInfo>();

			LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("does not exist"));

			sut.AddLogTailAttachment(null, options, tempFiles);

			Assert.IsEmpty(options.AdditionalAttachments);
			Assert.IsEmpty(tempFiles);
		}
	}
}
