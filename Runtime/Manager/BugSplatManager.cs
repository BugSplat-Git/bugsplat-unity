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
		internal BugSplatOptions bugSplatOptions;

		[SerializeField]
		[Tooltip("Should the BugSplatManager be destroyed when a new scene is loaded?")]
		internal bool dontDestroyManagerOnSceneLoad = true;

		[SerializeField]
		[Tooltip("Register BugSplat to capture LogType.Exceptions on initialization.")]
		internal bool registerLogMessageReceived = true;

		[SerializeField]
		[Tooltip("Also capture unhandled exceptions thrown on background threads. Unity only raises logMessageReceived for main-thread logs, so without this those exceptions are written to the log but never reported. Requires Register Log Message Received.")]
		internal bool captureExceptionsOnBackgroundThreads = true;

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
			if (backgroundLogMessages == null)
			{
				return;
			}

			// Bounded rather than "drain until empty". Background threads enqueue concurrently, so
			// an unbounded loop only exits when the producers happen to lose the race — a thread
			// failing in a tight loop could refill the queue as fast as this drains it and never
			// hand control back to the player loop. One frame drains at most one queue's worth;
			// whatever is left waits for the next frame.
			for (var drained = 0;
				drained < backgroundLogMessages.Capacity && backgroundLogMessages.TryDequeue(out var message);
				drained++)
			{
				StartCoroutine(bugsplatRef.BugSplat.LogMessageReceived(message.LogMessage, message.StackTrace, message.Type));
			}

			// Checked even when nothing was drained: a burst can overflow and then fully drain, and
			// the warning would otherwise be stranded until the next background exception arrived.
			var dropped = backgroundLogMessages.TakeDroppedCount();
			if (dropped > 0)
			{
				Debug.LogWarning($"BugSplat. Dropped {dropped} background thread exception(s) — they arrived faster than they could be posted. At most {backgroundLogMessages.Capacity} are buffered at a time.");
			}
		}

		void LogMessageReceivedHandler(string logMessage, string stackTrace, LogType type)
		{
			// Filter before StartCoroutine — the guard downstream skips these anyway, but only
			// after allocating two iterator state machines and a Coroutine per Debug.Log. Same
			// contract as the background path: the guard remains authoritative.
			if (!BackgroundLogMessageQueue.IsReportable(type))
			{
				return;
			}

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
