using BugSplatUnity.Runtime.Manager;
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
	/// what BugSplatManager forwards to this very reporter. What stops a failing report from
	/// becoming a second report is the log type: the pipeline only ever acts on LogType.Exception,
	/// so the diagnostics go out as LogType.Error. These pin that, and that the distinction is
	/// narrow enough to leave the next genuine exception alone.
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
		public IEnumerator Post_WhenUploadFails_ShouldLogTheDiagnosticAtATypeThePipelineIgnores()
		{
			IgnoreReporterErrorLogs();

			var types = new List<LogType>();
			OnLogMessageReceived((message, stackTrace, type) => types.Add(type));

			yield return sut.Post(new Exception("BugSplat test: original"));

			CollectionAssert.IsNotEmpty(types, "the reporter should log a diagnostic when the upload fails");
			CollectionAssert.DoesNotContain(types, LogType.Exception);

			// Asserted against the real filters rather than the reporter's own guard, which the
			// permissive fake in SetUp replaces: these two are what a diagnostic would meet on
			// the way back in, on the main thread and off it respectively.
			var guard = new ReportUploadGuardService(new WebGLClientSettingsRepository());
			foreach (var type in types)
			{
				Assert.IsFalse(guard.ShouldPostLogMessage(type), $"{type} would be posted by the guard service");
				Assert.IsFalse(BackgroundLogMessageQueue.IsReportable(type), $"{type} would be queued from a background thread");
			}
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
