using BugSplatUnity.Runtime.Client;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests
{
	public class BugSplatCreateFromOptionsTests
	{
		BugSplatOptions options;

		[SetUp]
		public void SetUp()
		{
			options = ScriptableObject.CreateInstance<BugSplatOptions>();
			options.Database = "database";
			options.Application = "application";
			options.Version = "version";
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(options);
		}

		// Every value is deliberately set away from its default. Asserting a field that already
		// holds the value it would have had anyway passes whether or not the mapping exists.
		[Test]
		public void CreateFromOptions_ShouldCopyEveryConfiguredValue()
		{
			options.Description = "description";
			options.Email = "fred@bugsplat.com";
			options.Key = "key";
			options.Notes = "notes";
			options.User = "fred";
			options.CaptureEditorLog = true;
			options.CapturePlayerLog = false;
			options.CaptureScreenshots = true;
			options.LogFileMaxSizeMB = 42;
			options.PostExceptionsInEditor = true;
#if UNITY_IOS
			options.IosAutoSubmitCrashReport = false;
			options.IosAutoSubmitFatalHangReport = false;
#elif UNITY_STANDALONE_OSX
			options.MacAutoSubmitCrashReport = true;
			options.MacAutoSubmitFatalHangReport = false;
#endif

			var sut = BugSplat.CreateFromOptions(options);

			Assert.AreEqual("description", sut.Description, nameof(options.Description));
			Assert.AreEqual("fred@bugsplat.com", sut.Email, nameof(options.Email));
			Assert.AreEqual("key", sut.Key, nameof(options.Key));
			Assert.AreEqual("notes", sut.Notes, nameof(options.Notes));
			Assert.AreEqual("fred", sut.User, nameof(options.User));
			Assert.True(sut.CaptureEditorLog, nameof(options.CaptureEditorLog));
			Assert.False(sut.CapturePlayerLog, nameof(options.CapturePlayerLog));
			Assert.True(sut.CaptureScreenshots, nameof(options.CaptureScreenshots));
			Assert.AreEqual(42, sut.LogFileMaxSizeMB, nameof(options.LogFileMaxSizeMB));
			Assert.True(sut.PostExceptionsInEditor, nameof(options.PostExceptionsInEditor));
			// CreateFromOptions picks the pair for the active build target, so the assertions
			// follow it. On a non-Apple target there is no mapping to check.
#if UNITY_IOS
			Assert.False(sut.AutoSubmitCrashReportSetting, nameof(options.IosAutoSubmitCrashReport));
			Assert.False(sut.AutoSubmitFatalHangReportSetting, nameof(options.IosAutoSubmitFatalHangReport));
#elif UNITY_STANDALONE_OSX
			Assert.True(sut.AutoSubmitCrashReportSetting, nameof(options.MacAutoSubmitCrashReport));
			Assert.False(sut.AutoSubmitFatalHangReportSetting, nameof(options.MacAutoSubmitFatalHangReport));
#else
			// Non-Apple targets are asserted rather than skipped: CreateFromOptions deliberately
			// passes null so bugsplat-apple's own defaults survive, and that is worth pinning -
			// it is also the only branch CI exercises, since no CI target is iOS or macOS.
			Assert.IsNull(sut.AutoSubmitCrashReportSetting, nameof(sut.AutoSubmitCrashReportSetting));
			Assert.IsNull(sut.AutoSubmitFatalHangReportSetting, nameof(sut.AutoSubmitFatalHangReportSetting));
#endif
		}

		[Test]
		public void CreateFromOptions_WhenPostExceptionsInEditorNotSet_ShouldNotPostExceptionsInEditor()
		{
			var sut = BugSplat.CreateFromOptions(options);

			Assert.False(sut.PostExceptionsInEditor, nameof(options.PostExceptionsInEditor));
		}

		[Test]
		public void NewBugSplat_ShouldNotPostExceptionsInEditor()
		{
			var sut = new BugSplat("database", "application", "version", false, false);

			Assert.False(sut.PostExceptionsInEditor, nameof(BugSplat.PostExceptionsInEditor));
		}

		// CapturePlayerLog defaults to true in the client settings, so false is the direction that
		// proves the option reached them. Whether the native reporter honors it cannot be asserted
		// here — the native branches are compiled out in the editor.
		[Test]
		public void CreateFromOptions_WhenCapturePlayerLogIsFalse_ShouldNotCapturePlayerLog()
		{
			options.CapturePlayerLog = false;

			var sut = BugSplat.CreateFromOptions(options);

			Assert.False(sut.CapturePlayerLog);
		}

		// The constructor throws on an empty application, so completing at all is what proves the
		// fallback ran.
		[Test]
		public void CreateFromOptions_WhenApplicationIsEmpty_ShouldFallBackToProductName()
		{
			Assume.That(Application.productName, Is.Not.Empty);
			options.Application = string.Empty;

			Assert.DoesNotThrow(() => BugSplat.CreateFromOptions(options));
		}

		[Test]
		public void CreateFromOptions_WhenVersionIsEmpty_ShouldFallBackToApplicationVersion()
		{
			Assume.That(Application.version, Is.Not.Empty);
			options.Version = string.Empty;

			Assert.DoesNotThrow(() => BugSplat.CreateFromOptions(options));
		}

		[Test]
		public void CreateFromOptions_ShouldCopyAttributes()
		{
			options.Attributes = new System.Collections.Generic.List<BugSplatAttribute>
			{
				new BugSplatAttribute { Name = "level", Value = "boss" },
				new BugSplatAttribute { Name = "difficulty", Value = "hard" }
			};

			var sut = BugSplat.CreateFromOptions(options);

			Assert.AreEqual(2, sut.Attributes.Count);
			Assert.AreEqual("boss", sut.Attributes["level"]);
			Assert.AreEqual("hard", sut.Attributes["difficulty"]);
		}

		[Test]
		public void CreateFromOptions_WhenAttributeNameIsEmpty_ShouldSkipIt()
		{
			options.Attributes = new System.Collections.Generic.List<BugSplatAttribute>
			{
				new BugSplatAttribute { Name = "", Value = "orphaned" },
				new BugSplatAttribute { Name = "kept", Value = "value" }
			};

			var sut = BugSplat.CreateFromOptions(options);

			Assert.AreEqual(1, sut.Attributes.Count);
			Assert.AreEqual("value", sut.Attributes["kept"]);
		}

		[Test]
		public void CreateFromOptions_WhenAttributeValueIsNull_ShouldUseEmptyString()
		{
			options.Attributes = new System.Collections.Generic.List<BugSplatAttribute>
			{
				new BugSplatAttribute { Name = "name", Value = null }
			};

			var sut = BugSplat.CreateFromOptions(options);

			Assert.AreEqual(string.Empty, sut.Attributes["name"]);
		}

		[Test]
		public void CreateFromOptions_WhenAttributesIsNull_ShouldNotThrow()
		{
			options.Attributes = null;

			Assert.DoesNotThrow(() => BugSplat.CreateFromOptions(options));
		}

		[Test]
		public void CreateFromOptions_WhenPersistentDataFileAttachmentPathsIsNull_ShouldNotThrow()
		{
			options.PersistentDataFileAttachmentPaths = null;

			Assert.DoesNotThrow(() => BugSplat.CreateFromOptions(options));
		}

		// The warning has to quote the entry as it was written as well as the path it resolved to.
		// Reporting only the resolved path names a directory the reader has never typed, which makes
		// the cause harder to find rather than easier.
		[Test]
		public void CreateFromOptions_WhenAttachmentDoesNotExist_ShouldSkipItAndQuoteTheEntryAsWritten()
		{
			options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
			{
				"does-not-exist.txt"
			};

			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
				System.Text.RegularExpressions.Regex.Escape("\"does-not-exist.txt\" does not exist at")));

			var sut = BugSplat.CreateFromOptions(options);

			Assert.IsEmpty(sut.Attachments);
		}

		// Entries are relative to Application.persistentDataPath. An absolute path is machine-specific,
		// so accepting it would produce an options asset that works only on the machine that authored it.
		// The file below really exists, so the only reason to skip it is that it is rooted.
		[Test]
		public void CreateFromOptions_WhenAttachmentPathIsAbsolute_ShouldSkipItAndExplainWhy()
		{
			var absolutePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
			System.IO.File.WriteAllText(absolutePath, "attach me");

			try
			{
				options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
				{
					absolutePath
				};

				LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
					System.Text.RegularExpressions.Regex.Escape($"\"{absolutePath}\"") + ".*persistentDataPath"));

				var sut = BugSplat.CreateFromOptions(options);

				Assert.IsEmpty(sut.Attachments);
			}
			finally
			{
				System.IO.File.Delete(absolutePath);
			}
		}

		[Test]
		public void CreateFromOptions_WhenAttachmentIsRelativeAndExists_ShouldAttachIt()
		{
			var fileName = System.IO.Path.GetRandomFileName();
			var fullPath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
			System.IO.Directory.CreateDirectory(Application.persistentDataPath);
			System.IO.File.WriteAllText(fullPath, "attach me");

			try
			{
				options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
				{
					fileName
				};

				var sut = BugSplat.CreateFromOptions(options);

				Assert.AreEqual(1, sut.Attachments.Count);
				Assert.AreEqual(new System.IO.FileInfo(fullPath).FullName, sut.Attachments[0].FullName);
			}
			finally
			{
				System.IO.File.Delete(fullPath);
			}
		}

		// Clicking + on the Inspector list leaves an empty row behind, and code can hand us a null.
		// Neither is an attempt to attach anything, and neither should throw.
		[Test]
		public void CreateFromOptions_WhenAttachmentPathIsBlank_ShouldSkipIt()
		{
			options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
			{
				null,
				string.Empty,
				"   "
			};

			BugSplat sut = null;

			Assert.DoesNotThrow(() => sut = BugSplat.CreateFromOptions(options));
			Assert.IsEmpty(sut.Attachments);
		}

		// A default that differs by construction path is two privacy postures for one setting.
		[Test]
		public void CapturePlayerLog_ShouldDefaultToEnabledWhenCreatedFromOptions()
		{
			var fromOptions = BugSplat.CreateFromOptions(options);

			Assert.True(options.CapturePlayerLog, "BugSplatOptions field default");
			Assert.True(fromOptions.CapturePlayerLog, "client created from options");
		}

		// The native reporter is compiled out in the editor, so AttachNativeLogFile itself is a no-op
		// here. NativePersistentDataAttachmentPaths records what CreateFromOptions resolved and handed
		// to it, which is the part of the wiring a PlayMode test can actually observe.
		[Test]
		public void CreateFromOptions_WhenAttachmentIsRelativeAndExists_ShouldOfferItToTheNativeReporter()
		{
			var fileName = System.IO.Path.GetRandomFileName();
			var fullPath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
			System.IO.Directory.CreateDirectory(Application.persistentDataPath);
			System.IO.File.WriteAllText(fullPath, "attach me");

			try
			{
				options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
				{
					fileName
				};

				var sut = BugSplat.CreateFromOptions(options);

				Assert.AreEqual(1, sut.NativePersistentDataAttachmentPaths.Count);
				Assert.AreEqual(new System.IO.FileInfo(fullPath).FullName, sut.NativePersistentDataAttachmentPaths[0]);
			}
			finally
			{
				System.IO.File.Delete(fullPath);
			}
		}

		[Test]
		public void CreateFromOptions_WhenAttachmentPathIsAbsolute_ShouldNotOfferItToTheNativeReporter()
		{
			var absolutePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
			System.IO.File.WriteAllText(absolutePath, "attach me");

			try
			{
				options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
				{
					absolutePath
				};

				LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
					System.Text.RegularExpressions.Regex.Escape($"\"{absolutePath}\"") + ".*persistentDataPath"));

				var sut = BugSplat.CreateFromOptions(options);

				Assert.IsEmpty(sut.NativePersistentDataAttachmentPaths);
			}
			finally
			{
				System.IO.File.Delete(absolutePath);
			}
		}

		[Test]
		public void CreateFromOptions_WhenAttachmentDoesNotExist_ShouldNotOfferItToTheNativeReporter()
		{
			options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
			{
				"does-not-exist.txt"
			};

			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
				@"does-not-exist\.txt"));

			var sut = BugSplat.CreateFromOptions(options);

			Assert.IsEmpty(sut.NativePersistentDataAttachmentPaths);
		}

#if !UNITY_WEBGL
		// WebGL is excluded: it has no Player.log, so its repository defaults to off.
		[Test]
		public void CapturePlayerLog_ShouldDefaultToEnabledWhenConstructedInCode()
		{
			var fromCode = new BugSplat("database", "application", "version", false, false);

			Assert.True(fromCode.CapturePlayerLog, "client created in code");
		}
#endif
	}
}
