using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace BugSplatUnity.Runtime.Manager
{
	internal readonly struct BackgroundLogMessage
	{
		public readonly string LogMessage;
		public readonly string StackTrace;
		public readonly LogType Type;

		public BackgroundLogMessage(string logMessage, string stackTrace, LogType type)
		{
			LogMessage = logMessage;
			StackTrace = stackTrace;
			Type = type;
		}
	}

	/// <summary>
	/// Buffers log messages raised on background threads until the main thread can post them.
	///
	/// Unity delivers logs through two events: <c>Application.logMessageReceived</c>, which fires
	/// only for main-thread logs, and <c>Application.logMessageReceivedThreaded</c>, which fires for
	/// every log on whichever thread produced it. Reporting needs the threaded event to see
	/// background-thread exceptions at all, but posting from that event directly is not possible —
	/// reports are sent from a coroutine, and coroutines can only be started on the main thread.
	///
	/// So the threaded handler enqueues here and the main thread drains it. Because the threaded
	/// event also fires for main-thread logs that <c>logMessageReceived</c> already delivered,
	/// anything raised on the main thread is rejected — that rejection is what keeps main-thread
	/// exceptions from being reported twice.
	/// </summary>
	internal sealed class BackgroundLogMessageQueue
	{
		/// <summary>
		/// Upper bound on buffered messages. A background thread can log far faster than frames
		/// elapse, so a runaway loop would otherwise grow this queue without limit and turn every
		/// entry into an upload.
		/// </summary>
		internal const int DefaultCapacity = 64;

		private readonly ConcurrentQueue<BackgroundLogMessage> pending = new ConcurrentQueue<BackgroundLogMessage>();
		private readonly int mainThreadId;
		private readonly int capacity;
		private int pendingCount;
		private int droppedCount;

		public BackgroundLogMessageQueue(int mainThreadId, int capacity = DefaultCapacity)
		{
			if (capacity < 1)
			{
				throw new ArgumentException($"BugSplat error: capacity must be at least 1, was {capacity}");
			}

			this.mainThreadId = mainThreadId;
			this.capacity = capacity;
		}

		public bool IsEmpty => pending.IsEmpty;

		/// <summary>
		/// Upper bound on buffered messages for this instance. Also bounds how much the main
		/// thread drains per frame, so one frame can never do more work than the queue can hold.
		/// </summary>
		public int Capacity => capacity;

		/// <summary>
		/// Mirrors <c>ReportUploadGuardService.ShouldPostLogMessage</c> so ordinary background
		/// logging doesn't consume the queue's capacity. The guard still runs downstream and
		/// remains authoritative — this is a filter, not the policy.
		/// </summary>
		internal static bool IsReportable(LogType type) => type == LogType.Exception;

		/// <summary>
		/// Queues a message if it came from a background thread, is reportable, and there is room.
		/// Returns whether it was queued. Safe to call from any thread.
		/// </summary>
		/// <remarks>
		/// Capacity accounting is conservative rather than exact. <see cref="TryDequeue"/> decrements
		/// the count after the item is already out of the queue, so an enqueue racing that window can
		/// still see the pre-dequeue count and drop even though a slot has just opened. The error only
		/// runs one way — the queue never holds more than <see cref="Capacity"/> — and it can only
		/// occur while already saturated, a state that is by definition already dropping and already
		/// reported through <see cref="TakeDroppedCount"/>. A lock would close the window, but this
		/// runs on arbitrary threads from inside Unity's log handler while an exception is unwinding;
		/// blocking there is the worse risk.
		/// </remarks>
		public bool Enqueue(string logMessage, string stackTrace, LogType type, int callingThreadId)
		{
			if (callingThreadId == mainThreadId)
			{
				return false;
			}

			if (!IsReportable(type))
			{
				return false;
			}

			if (Interlocked.Increment(ref pendingCount) > capacity)
			{
				Interlocked.Decrement(ref pendingCount);
				Interlocked.Increment(ref droppedCount);
				return false;
			}

			pending.Enqueue(new BackgroundLogMessage(logMessage, stackTrace, type));
			return true;
		}

		public bool TryDequeue(out BackgroundLogMessage message)
		{
			if (!pending.TryDequeue(out message))
			{
				return false;
			}

			Interlocked.Decrement(ref pendingCount);
			return true;
		}

		/// <summary>
		/// Returns how many messages have been dropped since the last call and resets the counter,
		/// so the caller can report an overflow once rather than once per lost message.
		/// </summary>
		public int TakeDroppedCount() => Interlocked.Exchange(ref droppedCount, 0);
	}
}
