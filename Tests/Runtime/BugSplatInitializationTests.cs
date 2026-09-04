using System;
using System.Collections;
using System.Net.Http;
using System.Text.RegularExpressions;
using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Manager;
using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#pragma warning disable 618 // BugSplatManager is obsolete; these tests pin its compatibility behavior.

namespace BugSplatUnity.RuntimeTests
{
	/// <summary>
	/// Pins the initialization contract: Initialize sets Instance exactly once, the host it creates
	/// reports, Shutdown undoes both, and the obsolete BugSplatManager adopts an existing instance
	/// instead of building a second one - which is what stops two managers double-reporting.
	/// </summary>
	public class BugSplatInitializationTests
	{
		const float TimeoutSeconds = 10f;

		BugSplatOptions options;
		GameObject managerObject;

		[SetUp]
		public void SetUp()
		{
			// Whatever AutoInitialize or an earlier test left behind.
			BugSplat.Shutdown();

			options = ScriptableObject.CreateInstance<BugSplatOptions>();
			options.Database = "fred";
			// Process-wide event; a host left subscribed would see faulted Tasks from unrelated tests.
			options.CaptureUnobservedTaskExceptions = false;
		}

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;

			if (managerObject != null)
			{
				UnityEngine.Object.DestroyImmediate(managerObject);
				managerObject = null;
			}

			BugSplat.Shutdown();
			UnityEngine.Object.DestroyImmediate(options);
		}

		BugSplatManager CreateManager()
		{
			// Inactive so Awake is deferred until the serialized fields are set.
			managerObject = new GameObject("BugSplatInitializationTests");
			managerObject.SetActive(false);
			var manager = managerObject.AddComponent<BugSplatManager>();
			manager.bugSplatOptions = options;
			manager.dontDestroyManagerOnSceneLoad = false;
			manager.captureUnobservedTaskExceptions = false;
			managerObject.SetActive(true);
			return manager;
		}

		IEnumerator WaitUntil(Func<bool> condition)
		{
			var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
			while (!condition() && Time.realtimeSinceStartup < deadline)
			{
				yield return null;
			}
		}

		[Test]
		public void Initialize_SetsInstance()
		{
			Assert.IsFalse(BugSplat.IsInitialized);

			var bugSplat = BugSplat.Initialize(options);

			Assert.IsTrue(BugSplat.IsInitialized);
			Assert.AreSame(bugSplat, BugSplat.Instance);
		}

		[Test]
		public void Initialize_Twice_WarnsAndReturnsTheExistingInstance()
		{
			var first = BugSplat.Initialize(options);

			LogAssert.Expect(LogType.Warning, new Regex("already initialized"));
			var second = BugSplat.Initialize(options);

			Assert.AreSame(first, second);
			Assert.AreSame(first, BugSplat.Instance);
		}

		[Test]
		public void Initialize_WithNullOptions_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => BugSplat.Initialize(null));
			Assert.IsFalse(BugSplat.IsInitialized);
		}

		[Test]
		public void Shutdown_ClearsInstance()
		{
			BugSplat.Initialize(options);

			BugSplat.Shutdown();

			Assert.IsFalse(BugSplat.IsInitialized);
			Assert.IsNull(BugSplat.Instance);
		}

		[UnityTest]
		public IEnumerator Initialize_HostReportsAMainThreadExceptionExactlyOnce()
		{
			var reporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
			BugSplat.Initialize(options).exceptionReporter = reporter;
			LogAssert.ignoreFailingMessages = true;

			Debug.LogException(new Exception("BugSplat initialization test: main thread"));

			yield return WaitUntil(() => reporter.Calls.LogMessageReceived.Count >= 1);
			yield return null;
			yield return null;

			Assert.AreEqual(1, reporter.Calls.LogMessageReceived.Count);
		}

		[UnityTest]
		public IEnumerator Shutdown_StopsReporting()
		{
			var reporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
			BugSplat.Initialize(options).exceptionReporter = reporter;
			LogAssert.ignoreFailingMessages = true;

			BugSplat.Shutdown();
			Debug.LogException(new Exception("BugSplat initialization test: after shutdown"));

			yield return null;
			yield return null;
			yield return null;

			Assert.IsEmpty(reporter.Calls.LogMessageReceived);
		}

		[Test]
		public void Manager_WhenNotInitialized_InitializesFromItsOwnOptions()
		{
			var manager = CreateManager();

			Assert.IsTrue(BugSplat.IsInitialized);
			Assert.AreSame(BugSplat.Instance, manager.BugSplat);
		}

		[Test]
		public void Manager_WhenAlreadyInitialized_AdoptsTheInstanceAndWarns()
		{
			var existing = BugSplat.Initialize(options);

			LogAssert.Expect(LogType.Warning, new Regex("no longer needed"));
			var manager = CreateManager();

			Assert.AreSame(existing, BugSplat.Instance);
			Assert.AreSame(existing, manager.BugSplat);
		}

		// The double-reporting half of #174: a second manager used to build a second client with
		// its own host, so every exception posted twice - once per host. Counting calls on one
		// client's reporter cannot see that, because the duplicate goes to the other client; the
		// number of hosts can.
		[UnityTest]
		public IEnumerator Manager_WhenAlreadyInitialized_DoesNotStartASecondHost()
		{
			var reporter = new FakeDotNetStandardExceptionReporter(new HttpResponseMessage());
			BugSplat.Initialize(options).exceptionReporter = reporter;
			LogAssert.ignoreFailingMessages = true;
			CreateManager();

			// Lets any host a previous test shut down finish its deferred Destroy before counting.
			yield return null;

			var hosts = UnityEngine.Object.FindObjectsByType<BugSplatRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			Assert.AreEqual(1, hosts.Length, "a second manager must adopt the host, not start another");

			Debug.LogException(new Exception("BugSplat initialization test: two managers"));

			yield return WaitUntil(() => reporter.Calls.LogMessageReceived.Count >= 1);
			yield return null;
			yield return null;

			Assert.AreEqual(1, reporter.Calls.LogMessageReceived.Count);
		}

		[Test]
		public void Manager_Destroyed_ShutsDownTheInstanceItCreated()
		{
			CreateManager();
			Assert.IsTrue(BugSplat.IsInitialized);

			UnityEngine.Object.DestroyImmediate(managerObject);
			managerObject = null;

			Assert.IsFalse(BugSplat.IsInitialized);
		}

		[Test]
		public void Manager_Destroyed_LeavesAnAdoptedInstanceAlone()
		{
			var existing = BugSplat.Initialize(options);
			LogAssert.Expect(LogType.Warning, new Regex("no longer needed"));
			CreateManager();

			UnityEngine.Object.DestroyImmediate(managerObject);
			managerObject = null;

			Assert.AreSame(existing, BugSplat.Instance);
		}

		[Test]
		public void Options_InitializeAutomatically_DefaultsToTrue()
		{
			Assert.IsTrue(ScriptableObject.CreateInstance<BugSplatOptions>().InitializeAutomatically);
		}

#if UNITY_EDITOR
		// The editor half of the resolution path: AutoInitialize reads the Project Settings
		// selection, which is an EditorBuildSettings config object and has to be a saved asset.
		const string TemporaryAssetPath = "Assets/BugSplatInitializationTests.asset";

		void SelectAsSavedAsset(BugSplatOptions asset)
		{
			UnityEditor.AssetDatabase.CreateAsset(asset, TemporaryAssetPath);
			UnityEditor.EditorBuildSettings.AddConfigObject(BugSplatOptions.ConfigObjectKey, asset, true);
		}

		void ClearSelection()
		{
			UnityEditor.EditorBuildSettings.RemoveConfigObject(BugSplatOptions.ConfigObjectKey);
			UnityEditor.AssetDatabase.DeleteAsset(TemporaryAssetPath);
		}

		[Test]
		public void AutoInitialize_InitializesFromTheSelectedAsset()
		{
			SelectAsSavedAsset(options);
			try
			{
				BugSplat.AutoInitialize();

				Assert.IsTrue(BugSplat.IsInitialized);
			}
			finally
			{
				ClearSelection();
			}
		}

		[Test]
		public void AutoInitialize_WhenInitializeAutomaticallyIsOff_DoesNothingQuietly()
		{
			options.InitializeAutomatically = false;
			SelectAsSavedAsset(options);
			try
			{
				BugSplat.AutoInitialize();

				Assert.IsFalse(BugSplat.IsInitialized);
				LogAssert.NoUnexpectedReceived();
			}
			finally
			{
				ClearSelection();
			}
		}

		[Test]
		public void AutoInitialize_WhenNothingIsSelected_WarnsAndDoesNothing()
		{
			UnityEditor.EditorBuildSettings.RemoveConfigObject(BugSplatOptions.ConfigObjectKey);

			LogAssert.Expect(LogType.Warning, new Regex("not configured"));
			BugSplat.AutoInitialize();

			Assert.IsFalse(BugSplat.IsInitialized);
		}
#endif
	}
}
