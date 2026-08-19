using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Reporter
{
	/// <summary>
	/// The reporter's own diagnostics travel back through Application.logMessageReceived, which is
	/// what BugSplatManager forwards to this very reporter. These pin that a failing report can't
	/// turn into a second report, and that the guard is narrow enough to leave the next genuine
	/// exception alone.
	/// </summary>
	public class DotNetStandardExceptionReporterReentrancyTests
	{
		DotNetStandardExceptionReporter sut;
		FakeFailingDotNetExceptionClient exceptionClient;
		Application.LogCallback logCallback;

		[SetUp]
		public void SetUp()
		{
			exceptionClient = new FakeFailingDotNetExceptionClient(new Exception("BugSplat test: upload failed"));
			sut = new DotNetStandardExceptionReporter(new WebGLClientSettingsRepository(), exceptionClient)
			{
				reportUploadGuardService = new FakeTrueReportUploadGuardService()
			};
		}

		[TearDown]
		public void TearDown()
		{
			if (logCallback != null)
			{
				Application.logMessageReceived -= logCallback;
				logCallback = null;
			}
		}

		// The reporter logs an error when the upload fails, and an unexpected error fails the
		// test. This must run inside the test body: SetUp executes outside the test's log
		// scope, so the flag set there doesn't reach the scope that judges the logs. The flag
		// dies with the test's scope, so no TearDown reset is needed.
		static void IgnoreReporterErrorLogs()
		{
			LogAssert.ignoreFailingMessages = true;
		}

		void OnLogMessageReceived(Application.LogCallback callback)
		{
			logCallback = callback;
			Application.logMessageReceived += callback;
		}

		[UnityTest]
		public IEnumerator Post_WhenUploadFails_ShouldNotRaiseTheDiagnosticAsAnException()
		{
			IgnoreReporterErrorLogs();

			var types = new List<LogType>();
			OnLogMessageReceived((message, stackTrace, type) => types.Add(type));

			yield return sut.Post(new Exception("BugSplat test: original"));

			CollectionAssert.Contains(types, LogType.Error);
			CollectionAssert.DoesNotContain(types, LogType.Exception);
		}

		[UnityTest]
		public IEnumerator Post_WhenUploadFails_ShouldNotReportTheDiagnosticItLogs()
		{
			IgnoreReporterErrorLogs();

			ExceptionReporterPostResult reentrantResult = null;
			var reentrantStarted = false;

			OnLogMessageReceived((message, stackTrace, type) =>
			{
				if (type != LogType.Error)
				{
					return;
				}

				var reentrant = sut.LogMessageReceived(
					message,
					stackTrace,
					LogType.Exception,
					result => reentrantResult = result);
				reentrantStarted = reentrant.MoveNext();
			});

			yield return sut.Post(new Exception("BugSplat test: original"));

			Assert.IsFalse(reentrantStarted, "the re-entrant report should have completed without posting");
			Assert.IsNotNull(reentrantResult, "the re-entrant report should have been skipped, not started");
			Assert.IsFalse(reentrantResult.Uploaded);
			Assert.AreEqual(1, exceptionClient.Calls.Count);
		}

		[UnityTest]
		public IEnumerator Post_WhenUploadFails_ShouldStillReportTheNextGenuineException()
		{
			IgnoreReporterErrorLogs();

			yield return sut.Post(new Exception("BugSplat test: original"));

			yield return sut.LogMessageReceived("BugSplat test: next", "stackTrace", LogType.Exception);

			Assert.AreEqual(2, exceptionClient.Calls.Count);
		}
	}
}
