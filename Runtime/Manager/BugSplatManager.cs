using System;
using System.Threading;
using System.Threading.Tasks;
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

		[SerializeField]
		[Tooltip("Also capture exceptions from Tasks that faulted and were never awaited. These never reach Unity's log at all, so they are otherwise invisible. They surface only after a garbage collection notices the Task, so they are reported late and are not guaranteed to be reported before the process exits. Requires Register Log Message Received.")]
		internal bool captureUnobservedTaskExceptions = true;

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

				if (captureExceptionsOnBackgroundThreads || captureUnobservedTaskExceptions)
				{
					// Awake runs on the main thread, so this is the id the threaded handler compares
					// against to tell a background log from one logMessageReceived already delivered.
					backgroundLogMessages = new BackgroundLogMessageQueue(Thread.CurrentThread.ManagedThreadId);
				}

				if (captureExceptionsOnBackgroundThreads)
				{
					Application.logMessageReceivedThreaded += LogMessageReceivedThreadedHandler;
				}

				if (captureUnobservedTaskExceptions)
				{
					TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
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
			TaskScheduler.UnobservedTaskException -= UnobservedTaskExceptionHandler;
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
				Debug.LogWarning($"BugSplat. Dropped {dropped} off-main-thread exception(s) — they arrived faster than they could be posted. At most {backgroundLogMessages.Capacity} are buffered at a time.");
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

		void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs args)
		{
			// SetObserved is deliberately not called. Marking the exception observed would suppress
			// whatever the application does with it next — including the process-terminating
			// behavior a project can opt into — and reporting a crash must not change whether that
			// crash happens.
			EnqueueUnobservedTaskException(backgroundLogMessages, args?.Exception, Thread.CurrentThread.ManagedThreadId);
		}

		/// <summary>
		/// Queues one report per inner exception of the flattened AggregateException. Runs on the
		/// finalizer thread, so it only writes to the queue; <see cref="Update"/> posts from the
		/// main thread. Reporting each inner exception separately rather than the wrapper keeps
		/// distinct failures in distinct dashboard buckets.
		/// </summary>
		internal static void EnqueueUnobservedTaskException(BackgroundLogMessageQueue queue, AggregateException exception, int callingThreadId)
		{
			if (queue == null || exception == null)
			{
				return;
			}

			var inners = exception.Flatten().InnerExceptions;
			if (inners.Count == 0)
			{
				Enqueue(queue, exception, callingThreadId);
				return;
			}

			foreach (var inner in inners)
			{
				Enqueue(queue, inner, callingThreadId);
			}
		}

		// Matches the shape Unity's log callback delivers for an exception, so both capture paths
		// produce identically formatted reports.
		static void Enqueue(BackgroundLogMessageQueue queue, Exception exception, int callingThreadId)
		{
			queue.Enqueue(
				$"{exception.GetType()}: {exception.Message}",
				exception.StackTrace ?? string.Empty,
				LogType.Exception,
				callingThreadId);
		}
	}
}
