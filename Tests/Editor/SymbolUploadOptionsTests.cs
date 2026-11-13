using System;
using System.Threading;
using BugSplatUnity.Editor;
using BugSplatUnity.Runtime.Client;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.EditorTests
{
	public class SymbolUploadOptionsTests
	{
		BugSplatOptions options;

		[SetUp]
		public void SetUp() => options = ScriptableObject.CreateInstance<BugSplatOptions>();

		[TearDown]
		public void TearDown() => UnityEngine.Object.DestroyImmediate(options);

		[Test]
		public void Defaults_ShouldBoundTheUploadWithoutFailingTheBuild()
		{
			Assert.AreEqual(30, options.SymbolUploadTimeoutMinutes);
			Assert.IsFalse(options.FailBuildOnSymbolUploadError);
			Assert.IsTrue(string.IsNullOrEmpty(options.WindowsLineNumberMappingsPath));
		}

		[Test]
		public void GetSymbolUploadTimeout_ShouldConvertMinutes()
		{
			Assert.AreEqual(TimeSpan.FromMinutes(30), BuildPostprocessors.GetSymbolUploadTimeout(30));
		}

		[TestCase(0)]
		[TestCase(-5)]
		public void GetSymbolUploadTimeout_ShouldWaitIndefinitelyForZeroOrLess(int minutes)
		{
			Assert.AreEqual(Timeout.InfiniteTimeSpan, BuildPostprocessors.GetSymbolUploadTimeout(minutes));
		}

		[Test]
		public void DescribeSymbolUploadFailures_ShouldBeNullWithoutFailures()
		{
			Assert.IsNull(BuildPostprocessors.DescribeSymbolUploadFailures(new string[0], true));
			Assert.IsNull(BuildPostprocessors.DescribeSymbolUploadFailures(null, false));
		}

		[Test]
		public void DescribeSymbolUploadFailures_ShouldNameEachFailureAndTheOption()
		{
			var message = BuildPostprocessors.DescribeSymbolUploadFailures(new[] { "**/*.pdb from C:/Build", "LineNumberMappings.json from C:/Temp" }, false);

			StringAssert.Contains("2 symbol uploads failed", message);
			StringAssert.Contains("**/*.pdb from C:/Build", message);
			StringAssert.Contains("LineNumberMappings.json from C:/Temp", message);
			StringAssert.Contains("turn on Fail Build On Symbol Upload Error", message);
		}

		[Test]
		public void DescribeSymbolUploadFailures_ShouldSayWhenItFailsTheBuild()
		{
			var message = BuildPostprocessors.DescribeSymbolUploadFailures(new[] { "**/*.dSYM from /build" }, true);

			StringAssert.Contains("1 symbol upload failed", message);
			StringAssert.Contains("Failing the build", message);
		}

		[TestCase(BuildTarget.StandaloneWindows64)]
		[TestCase(BuildTarget.StandaloneWindows)]
		public void ShouldWarnCopyPdbFilesOff_ShouldWarnForWindowsWithoutPdbs(BuildTarget target)
		{
			Assert.IsTrue(BuildPostprocessors.ShouldWarnCopyPdbFilesOff(target, options, false));
		}

		[Test]
		public void ShouldWarnCopyPdbFilesOff_ShouldNotWarnWhenItDoesNotApply()
		{
			Assert.IsFalse(BuildPostprocessors.ShouldWarnCopyPdbFilesOff(BuildTarget.StandaloneWindows64, options, true));
			Assert.IsFalse(BuildPostprocessors.ShouldWarnCopyPdbFilesOff(BuildTarget.StandaloneOSX, options, false));
			Assert.IsFalse(BuildPostprocessors.ShouldWarnCopyPdbFilesOff(BuildTarget.StandaloneWindows64, null, false));

			options.UploadDebugSymbolsForWindows = false;
			Assert.IsFalse(BuildPostprocessors.ShouldWarnCopyPdbFilesOff(BuildTarget.StandaloneWindows64, options, false));
		}
	}
}
