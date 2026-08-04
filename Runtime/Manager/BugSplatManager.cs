using System;
using System.Threading;
using BugSplatUnity.Runtime.Client;
using UnityEngine;

namespace BugSplatUnity.Runtime.Manager
{
	public sealed class BugSplatManager : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("BugSplat configuration SerializedObject to instantiate BugSplat with.")]
		private BugSplatOptions bugSplatOptions;

		[SerializeField]
		[Tooltip("Should the BugSplatManager be destroyed when a new scene is loaded?")]
		private bool dontDestroyManagerOnSceneLoad = true;

		[SerializeField]
		[Tooltip("Register BugSplat to capture LogType.Exceptions on initialization.")]
		private bool registerLogMessageReceived = true;

		[SerializeField]
		[Tooltip("Also capture unhandled exceptions thrown on background threads. Unity only raises logMessageReceived for main-thread logs, so without this those exceptions are written to the log but never reported. Requires Register Log Message Received.")]
		private bool captureExceptionsOnBackgroundThreads = true;

		private BugSplatRef bugsplatRef;
		private BackgroundLogMessageQueue backgroundLogMessages;

		public BugSplat BugSplat => bugsplatRef.BugSplat;

		private void Awake()
		{
			if (bugSplatOptions == null)
			{
				throw new ArgumentException("BugSplat error: BugSplatOptions is null! BugSplat will not be initialized.");
			}

			var bugsplat = BugSplat.CreateFromOptions(bugSplatOptions);
			bugsplatRef = new BugSplatRef(bugsplat);

			if (registerLogMessageReceived)
			{
				Application.logMessageReceived += LogMessageReceivedHandler;

				if (captureExceptionsOnBackgroundThreads)
				{
					// Awake runs on the main thread, so this is the id the threaded handler compares
					// against to tell a background log from one logMessageReceived already delivered.
					backgroundLogMessages = new BackgroundLogMessageQueue(Thread.CurrentThread.ManagedThreadId);
					Application.logMessageReceivedThreaded += LogMessageReceivedThreadedHandler;
				}
			}

			if (dontDestroyManagerOnSceneLoad)
			{
				DontDestroyOnLoad(this);
			}
		}

		private void OnDestroy()
		{
			Application.logMessageReceived -= LogMessageReceivedHandler;
			Application.logMessageReceivedThreaded -= LogMessageReceivedThreadedHandler;
		}

		private void Update()
		{
			if (backgroundLogMessages == null || backgroundLogMessages.IsEmpty)
			{
				return;
			}

			while (backgroundLogMessages.TryDequeue(out var message))
			{
				StartCoroutine(bugsplatRef.BugSplat.LogMessageReceived(message.LogMessage, message.StackTrace, message.Type));
			}

			var dropped = backgroundLogMessages.TakeDroppedCount();
			if (dropped > 0)
			{
				Debug.LogWarning($"BugSplat. Dropped {dropped} background thread exception(s) — they arrived faster than they could be posted. At most {BackgroundLogMessageQueue.DefaultCapacity} are buffered at a time.");
			}
		}

		void LogMessageReceivedHandler(string logMessage, string stackTrace, LogType type)
		{
			StartCoroutine(bugsplatRef.BugSplat.LogMessageReceived(logMessage, stackTrace, type));
		}

		void LogMessageReceivedThreadedHandler(string logMessage, string stackTrace, LogType type)
		{
			// Runs on whichever thread logged. Do nothing here beyond queueing — most of the Unity
			// API, StartCoroutine included, is main-thread only.
			backgroundLogMessages?.Enqueue(logMessage, stackTrace, type, Thread.CurrentThread.ManagedThreadId);
		}
	}
}
