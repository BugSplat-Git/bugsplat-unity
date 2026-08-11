using System;
using System.Collections;
using System.Net.Http;
using System.Threading;
using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Manager;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Manager
{
	/// <summary>
	/// Play-mode tests for the manager's log-event wiring. The queue's policy is covered by
	/// BackgroundLogMessageQueueTest; what these pin is the part unit tests can't — Unity's own
	/// event contract (logMessageReceivedThreaded fires on the logging thread, both events fire
	/// for main-thread logs) and the exactly-once delivery built on top of it.
	///
	/// The real exception reporter is swapped for a fake right after Awake, so nothing here ever
	/// attempts a network post and the real reporter's rate limiter can't make delivery counts
	/// flaky.
	/// </summary>
	public class BugSplatManagerTest
	{
		// Generous so a loaded CI runner can't miss the window.
		const float TimeoutSeconds = 10f;

		GameObject gameObject;
		BugSplatManager manager;
		FakeDotNetStandardExceptionReporter reporter;

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;
			if (gameObject != null)
			{
				// DestroyImmediate so OnDestroy unsubscribes the static Application events
				// before the next test subscribes its own manager.
				UnityEngine.Object.DestroyImmediate(gameObject);
				gameObject = null;
			}
		}

		void CreateManager(bool registerLogMessageReceived = true, bool captureExceptionsOnBackgroundThreads = true)
		{
			var options = ScriptableObject.CreateInstance<BugSplatOptions>();
			options.Database = "fred";

			// Inactive so Awake is deferred until the serialized fields are set.
			gameObject = new GameObject("BugSplatManagerTest");
			gameObject.SetActive(false);
			manager = gameObject.AddComponent<BugSplatManager>();
			manager.bugSplatOptions = options;
			manager.dontDestroyManagerOnSceneLoad = false;
			manager.registerLogMessageReceived = registerLogMessageReceived;
			manager.captureExceptionsOnBackgroundThreads = captureExceptionsOnBackgroundThreads;
			gameObject.SetActive(true);

			reporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
			manager.BugSplat.exceptionReporter = reporter;
		}

		static Thread RunOnBackgroundThread(Action action)
		{
			var thread = new Thread(() => action())
			{
				IsBackground = true,
				Name = "BugSplatManagerTestThread"
			};
			thread.Start();
			return thread;
		}

		IEnumerator WaitUntil(Func<bool> condition)
		{
			var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
			while (!condition() && Time.realtimeSinceStartup < deadline)
			{
				yield return null;
			}
		}

		[UnityTest]
		public IEnumerator BackgroundThreadException_ReportsExactlyOnce()
		{
			CreateManager();
			LogAssert.ignoreFailingMessages = true;

			var thread = RunOnBackgroundThread(
				() => Debug.LogException(new Exception("BugSplat manager test: background thread")));

			yield return WaitUntil(() => reporter.Calls.LogMessageReceived.Count >= 1);
			thread.Join();

			// Extra frames so a duplicate delivery — the threaded handler and the main-thread
			// handler both posting — would have surfaced before the count is pinned.
			yield return null;
			yield return null;
			yield return null;

			Assert.AreEqual(1, reporter.Calls.LogMessageReceived.Count);
			var call = reporter.Calls.LogMessageReceived[0];
			StringAssert.Contains("BugSplat manager test: background thread", call.LogMessage);
			Assert.AreEqual(LogType.Exception, call.Type);
		}

		[UnityTest]
		public IEnumerator MainThreadException_ReportsExactlyOnce()
		{
			CreateManager();
			LogAssert.ignoreFailingMessages = true;

			// Both events fire for a main-thread log; the queue's thread-id rejection is what
			// keeps this from reporting twice.
			Debug.LogException(new Exception("BugSplat manager test: main thread"));

			yield return WaitUntil(() => reporter.Calls.LogMessageReceived.Count >= 1);
			yield return null;
			yield return null;
			yield return null;

			Assert.AreEqual(1, reporter.Calls.LogMessageReceived.Count);
			Assert.AreEqual(LogType.Exception, reporter.Calls.LogMessageReceived[0].Type);
		}

		[UnityTest]
		public IEnumerator MainThreadNonException_NeverReachesTheReporter()
		{
			CreateManager();
			LogAssert.ignoreFailingMessages = true;

			Debug.Log("BugSplat manager test: log");
			Debug.LogWarning("BugSplat manager test: warning");
			Debug.LogError("BugSplat manager test: error");

			yield return null;
			yield return null;
			yield return null;

			Assert.IsEmpty(reporter.Calls.LogMessageReceived);
		}

		[UnityTest]
		public IEnumerator BackgroundThreadException_WhenCaptureDisabled_DoesNotReport()
		{
			CreateManager(captureExceptionsOnBackgroundThreads: false);
			LogAssert.ignoreFailingMessages = true;

			var thread = RunOnBackgroundThread(
				() => Debug.LogException(new Exception("BugSplat manager test: capture disabled")));
			thread.Join();

			yield return null;
			yield return null;
			yield return null;

			Assert.IsEmpty(reporter.Calls.LogMessageReceived);
		}

		[UnityTest]
		public IEnumerator MainThreadException_WhenRegisterDisabled_DoesNotReport()
		{
			CreateManager(registerLogMessageReceived: false);
			LogAssert.ignoreFailingMessages = true;

			Debug.LogException(new Exception("BugSplat manager test: register disabled"));

			yield return null;
			yield return null;
			yield return null;

			Assert.IsEmpty(reporter.Calls.LogMessageReceived);
		}

		[UnityTest]
		public IEnumerator BackgroundThreadException_AfterDestroy_DoesNotReport()
		{
			CreateManager();
			LogAssert.ignoreFailingMessages = true;

			UnityEngine.Object.Destroy(gameObject);
			gameObject = null;
			// OnDestroy — and the static event unsubscription — runs at end of frame.
			yield return null;

			var thread = RunOnBackgroundThread(
				() => Debug.LogException(new Exception("BugSplat manager test: after destroy")));
			thread.Join();

			yield return null;
			yield return null;
			yield return null;

			Assert.IsEmpty(reporter.Calls.LogMessageReceived);
		}

		[UnityTest]
		public IEnumerator BackgroundThreadFlood_DrainsOneQueuePerFrameAndWarnsOnceAboutDrops()
		{
			CreateManager();
			LogAssert.ignoreFailingMessages = true;

			var dropWarnings = 0;
			Application.LogCallback countDropWarnings = (message, stackTrace, type) =>
			{
				if (type == LogType.Warning && message.Contains("Dropped"))
				{
					dropWarnings++;
				}
			};
			Application.logMessageReceived += countDropWarnings;
			try
			{
				// Three queues' worth, fully delivered before the next frame: Join returns before
				// any yield, and the threaded callback runs synchronously on the logging thread.
				// The queue holds exactly Capacity and drops the rest.
				var floodCount = BackgroundLogMessageQueue.DefaultCapacity * 3;
				var thread = RunOnBackgroundThread(() =>
				{
					for (var i = 0; i < floodCount; i++)
					{
						Debug.LogException(new Exception("BugSplat manager test: flood"));
					}
				});
				thread.Join();

				yield return WaitUntil(
					() => reporter.Calls.LogMessageReceived.Count >= BackgroundLogMessageQueue.DefaultCapacity);
				yield return null;

				Assert.AreEqual(BackgroundLogMessageQueue.DefaultCapacity, reporter.Calls.LogMessageReceived.Count);
				Assert.AreEqual(1, dropWarnings);
			}
			finally
			{
				Application.logMessageReceived -= countDropWarnings;
			}
		}
	}
}
