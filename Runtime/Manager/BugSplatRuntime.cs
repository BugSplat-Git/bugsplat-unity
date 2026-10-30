using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BugSplatUnity.Runtime.Manager
{
	/// <summary>
	/// The MonoBehaviour behind <see cref="BugSplat.Instance"/>: subscribes Unity's log callbacks,
	/// queues exceptions raised off the main thread, and posts everything from the main thread.
	/// <see cref="BugSplat.Initialize(BugSplatUnity.Runtime.Client.BugSplatOptions)"/> creates it on
	/// its own DontDestroyOnLoad GameObject; nothing about it is meant to be configured from a scene,
	/// which is why it is hidden from Add Component.
	/// </summary>
	[AddComponentMenu("")]
	internal sealed class BugSplatRuntime : MonoBehaviour
	{
		internal readonly struct Settings
		{
			public readonly bool RegisterLogMessageReceived;
			public readonly bool CaptureExceptionsOnBackgroundThreads;
			public readonly bool CaptureUnobservedTaskExceptions;

			public Settings(
				bool registerLogMessageReceived,
				bool captureExceptionsOnBackgroundThreads,
				bool captureUnobservedTaskExceptions)
			{
				RegisterLogMessageReceived = registerLogMessageReceived;
				CaptureExceptionsOnBackgroundThreads = captureExceptionsOnBackgroundThreads;
				CaptureUnobservedTaskExceptions = captureUnobservedTaskExceptions;
			}
		}

		// Visible in the hierarchy on purpose: a developer wondering whether BugSplat is running can
		// see it under DontDestroyOnLoad, and there is nothing on it to edit.
		const string HostName = "BugSplat";

		private BugSplat bugSplat;
		private BackgroundLogMessageQueue backgroundLogMessages;

		internal BugSplat BugSplat => bugSplat;

		internal static BugSplatRuntime Create(BugSplat bugSplat, Settings settings)
		{
			var host = new GameObject(HostName);
			if (Application.isPlaying)
			{
				// Only meaningful - and only permitted - in play mode.
				DontDestroyOnLoad(host);
			}

			var runtime = host.AddComponent<BugSplatRuntime>();
			runtime.Attach(bugSplat, settings);
			return runtime;
		}

		private void Attach(BugSplat bugSplat, Settings settings)
		{
			this.bugSplat = bugSplat;

			if (!settings.RegisterLogMessageReceived)
			{
				return;
			}

			Application.logMessageReceived += LogMessageReceivedHandler;

			if (settings.CaptureExceptionsOnBackgroundThreads || settings.CaptureUnobservedTaskExceptions)
			{
				// Attach runs on the main thread, so this is the id the threaded handler compares
				// against to tell a background log from one logMessageReceived already delivered.
				backgroundLogMessages = new BackgroundLogMessageQueue(Thread.CurrentThread.ManagedThreadId);
			}

			if (settings.CaptureExceptionsOnBackgroundThreads)
			{
				Application.logMessageReceivedThreaded += LogMessageReceivedThreadedHandler;
			}

			if (settings.CaptureUnobservedTaskExceptions)
			{
				TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
			}
		}

		/// <summary>
		/// Unsubscribes from the static events immediately. OnDestroy does the same, but Destroy is
		/// deferred to end of frame and Shutdown must not leave a window where two hosts are hooked.
		/// Safe to call more than once.
		/// </summary>
		internal void Detach()
		{
			Application.logMessageReceived -= LogMessageReceivedHandler;
			Application.logMessageReceivedThreaded -= LogMessageReceivedThreadedHandler;
			TaskScheduler.UnobservedTaskException -= UnobservedTaskExceptionHandler;
		}

		private void OnDestroy()
		{
			Detach();
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
				StartCoroutine(bugSplat.LogMessageReceived(message.LogMessage, message.StackTrace, message.Type));
			}

			// Checked even when nothing was drained: a burst can overflow and then fully drain, and
			// the warning would otherwise be stranded until the next background exception arrived.
			var dropped = backgroundLogMessages.TakeDroppedCount();
			if (dropped > 0)
			{
				Debug.LogWarning($"BugSplat. Dropped {dropped} off-main-thread exception(s) — they arrived faster than they could be posted. At most {backgroundLogMessages.Capacity} are buffered at a time.");
			}
		}

		private void LogMessageReceivedHandler(string logMessage, string stackTrace, LogType type)
		{
			// Filter before StartCoroutine — the guard downstream skips these anyway, but only
			// after allocating two iterator state machines and a Coroutine per Debug.Log. Same
			// contract as the background path: the guard remains authoritative.
			if (!BackgroundLogMessageQueue.IsReportable(type))
			{
				return;
			}

			StartCoroutine(bugSplat.LogMessageReceived(logMessage, stackTrace, type));
		}

		private void LogMessageReceivedThreadedHandler(string logMessage, string stackTrace, LogType type)
		{
			// Runs on whichever thread logged. Do nothing here beyond queueing — most of the Unity
			// API, StartCoroutine included, is main-thread only.
			backgroundLogMessages?.Enqueue(logMessage, stackTrace, type, Thread.CurrentThread.ManagedThreadId);
		}

		private void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs args)
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
		private static void Enqueue(BackgroundLogMessageQueue queue, Exception exception, int callingThreadId)
		{
			queue.Enqueue(
				$"{exception.GetType()}: {exception.Message}",
				exception.StackTrace ?? string.Empty,
				LogType.Exception,
				callingThreadId);
		}
	}
}
