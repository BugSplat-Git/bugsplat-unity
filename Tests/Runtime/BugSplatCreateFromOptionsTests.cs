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
			options.CapturePlayerLog = true;
			options.CaptureScreenshots = true;
			options.LogFileMaxSizeMB = 42;
			options.PostExceptionsInEditor = false;

			var sut = BugSplat.CreateFromOptions(options);

			Assert.AreEqual("description", sut.Description, nameof(options.Description));
			Assert.AreEqual("fred@bugsplat.com", sut.Email, nameof(options.Email));
			Assert.AreEqual("key", sut.Key, nameof(options.Key));
			Assert.AreEqual("notes", sut.Notes, nameof(options.Notes));
			Assert.AreEqual("fred", sut.User, nameof(options.User));
			Assert.True(sut.CaptureEditorLog, nameof(options.CaptureEditorLog));
			Assert.True(sut.CapturePlayerLog, nameof(options.CapturePlayerLog));
			Assert.True(sut.CaptureScreenshots, nameof(options.CaptureScreenshots));
			Assert.AreEqual(42, sut.LogFileMaxSizeMB, nameof(options.LogFileMaxSizeMB));
			Assert.False(sut.PostExceptionsInEditor, nameof(options.PostExceptionsInEditor));
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
		public void CreateFromOptions_WhenPersistentDataFileAttachmentPathsIsNull_ShouldNotThrow()
		{
			options.PersistentDataFileAttachmentPaths = null;

			Assert.DoesNotThrow(() => BugSplat.CreateFromOptions(options));
		}

		[Test]
		public void CreateFromOptions_WhenAttachmentDoesNotExist_ShouldSkipIt()
		{
			options.PersistentDataFileAttachmentPaths = new System.Collections.Generic.List<string>
			{
				"does-not-exist.txt"
			};

			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("does not exist"));

			var sut = BugSplat.CreateFromOptions(options);

			Assert.IsEmpty(sut.Attachments);
		}
	}
}
