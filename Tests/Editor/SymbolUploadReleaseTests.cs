using System;
using System.IO;
using BugSplatUnity.Editor;
using NUnit.Framework;

namespace BugSplatUnity.EditorTests
{
	public class SymbolUploadReleaseTests
	{
		static readonly string[] Variants =
		{
			SymbolUploadRelease.Windows,
			SymbolUploadRelease.MacOS,
			SymbolUploadRelease.MacOSIntel,
			SymbolUploadRelease.Linux
		};

		[TestCaseSource(nameof(Variants))]
		public void GetSha256_ShouldReturnALowercaseSha256ForEveryVariant(string variant)
		{
			StringAssert.IsMatch("^[0-9a-f]{64}$", SymbolUploadRelease.GetSha256(variant));
		}

		[TestCaseSource(nameof(Variants))]
		public void GetDownloadUrl_ShouldNameThePinnedRelease(string variant)
		{
			Assert.AreEqual(
				$"https://github.com/BugSplat-Git/symbol-upload/releases/download/{SymbolUploadRelease.Version}/{variant}",
				SymbolUploadRelease.GetDownloadUrl(variant));
		}

		[Test]
		public void GetSha256_ShouldThrowForAnUnknownVariant()
		{
			Assert.Throws<ArgumentException>(() => SymbolUploadRelease.GetSha256("symbol-upload-plan9"));
		}

		[Test]
		public void IsVerified_ShouldBeFalseForAMissingFile()
		{
			var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			Assert.IsFalse(SymbolUploadRelease.IsVerified(path, SymbolUploadRelease.Windows));
		}

		[Test]
		public void IsVerified_ShouldBeFalseForAFileThatIsNotThePinnedRelease()
		{
			var path = Path.GetTempFileName();
			try
			{
				File.WriteAllText(path, "not symbol-upload");
				Assert.IsFalse(SymbolUploadRelease.IsVerified(path, SymbolUploadRelease.Windows));
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void ComputeSha256_ShouldMatchAKnownDigest()
		{
			var path = Path.GetTempFileName();
			try
			{
				File.WriteAllText(path, "abc");
				Assert.AreEqual(
					"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
					SymbolUploadRelease.ComputeSha256(path));
			}
			finally
			{
				File.Delete(path);
			}
		}
	}
}
