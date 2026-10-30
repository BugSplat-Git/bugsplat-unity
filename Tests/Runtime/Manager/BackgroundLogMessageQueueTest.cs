using System;
using System.Threading;
using BugSplatUnity.Runtime.Manager;
using NUnit.Framework;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Manager
{
	public class BackgroundLogMessageQueueTest
	{
		const int MainThreadId = 1;
		const int BackgroundThreadId = 2;

		static BackgroundLogMessageQueue CreateQueue(int capacity = BackgroundLogMessageQueue.DefaultCapacity)
			=> new BackgroundLogMessageQueue(MainThreadId, capacity);

		// A capacity that can't hold a single message would silently drop everything and stall
		// the manager's drain loop, so it fails fast instead.
		[TestCase(0)]
		[TestCase(-1)]
		public void Constructor_WhenCapacityIsNotPositive_ShouldThrow(int capacity)
		{
			Assert.Throws<ArgumentException>(() => CreateQueue(capacity));
		}

		[Test]
		public void Enqueue_WhenCalledOnBackgroundThread_ShouldQueueMessage()
		{
			var queue = CreateQueue();

			var queued = queue.Enqueue("message", "stack", LogType.Exception, BackgroundThreadId);

			Assert.True(queued);
			Assert.False(queue.IsEmpty);
		}

		// Unity raises logMessageReceivedThreaded for main-thread logs too, and those are already
		// delivered by logMessageReceived. Rejecting them here is what prevents double reporting.
		[Test]
		public void Enqueue_WhenCalledOnMainThread_ShouldNotQueueMessage()
		{
			var queue = CreateQueue();

			var queued = queue.Enqueue("message", "stack", LogType.Exception, MainThreadId);

			Assert.False(queued);
			Assert.True(queue.IsEmpty);
		}

		[TestCase(LogType.Log)]
		[TestCase(LogType.Warning)]
		[TestCase(LogType.Error)]
		[TestCase(LogType.Assert)]
		public void Enqueue_WhenTypeIsNotException_ShouldNotQueueMessage(LogType type)
		{
			var queue = CreateQueue();

			var queued = queue.Enqueue("message", "stack", type, BackgroundThreadId);

			Assert.False(queued);
			Assert.True(queue.IsEmpty);
		}

		[Test]
		public void TryDequeue_WhenMessageWasQueued_ShouldReturnSameValues()
		{
			var queue = CreateQueue();
			queue.Enqueue("message", "stack", LogType.Exception, BackgroundThreadId);

			var dequeued = queue.TryDequeue(out var message);

			Assert.True(dequeued);
			Assert.AreEqual("message", message.LogMessage);
			Assert.AreEqual("stack", message.StackTrace);
			Assert.AreEqual(LogType.Exception, message.Type);
			Assert.True(queue.IsEmpty);
		}

		[Test]
		public void TryDequeue_WhenQueueIsEmpty_ShouldReturnFalse()
		{
			var queue = CreateQueue();

			Assert.False(queue.TryDequeue(out _));
		}

		[Test]
		public void Enqueue_WhenCapacityIsExceeded_ShouldDropMessage()
		{
			var queue = CreateQueue(capacity: 2);

			Assert.True(queue.Enqueue("first", "stack", LogType.Exception, BackgroundThreadId));
			Assert.True(queue.Enqueue("second", "stack", LogType.Exception, BackgroundThreadId));
			Assert.False(queue.Enqueue("third", "stack", LogType.Exception, BackgroundThreadId));

			Assert.AreEqual(1, queue.TakeDroppedCount());
		}

		// Draining frees capacity, so a burst that overflows one frame doesn't wedge the queue.
		[Test]
		public void Enqueue_AfterDequeueFreesCapacity_ShouldQueueAgain()
		{
			var queue = CreateQueue(capacity: 1);
			queue.Enqueue("first", "stack", LogType.Exception, BackgroundThreadId);
			Assert.False(queue.Enqueue("dropped", "stack", LogType.Exception, BackgroundThreadId));

			queue.TryDequeue(out _);

			Assert.True(queue.Enqueue("second", "stack", LogType.Exception, BackgroundThreadId));
		}

		[Test]
		public void TakeDroppedCount_WhenCalledTwice_ShouldResetAfterFirstCall()
		{
			var queue = CreateQueue(capacity: 1);
			queue.Enqueue("first", "stack", LogType.Exception, BackgroundThreadId);
			queue.Enqueue("dropped", "stack", LogType.Exception, BackgroundThreadId);

			Assert.AreEqual(1, queue.TakeDroppedCount());
			Assert.AreEqual(0, queue.TakeDroppedCount());
		}

		[Test]
		public void TakeDroppedCount_WhenNothingWasDropped_ShouldBeZero()
		{
			var queue = CreateQueue();
			queue.Enqueue("message", "stack", LogType.Exception, BackgroundThreadId);

			Assert.AreEqual(0, queue.TakeDroppedCount());
		}

		[Test]
		public void Capacity_ShouldReportTheConfiguredBound()
		{
			Assert.AreEqual(BackgroundLogMessageQueue.DefaultCapacity, CreateQueue().Capacity);
			Assert.AreEqual(4, CreateQueue(capacity: 4).Capacity);
		}

		// The sequential tests above only exercise one thread at a time, which is not the state this
		// queue exists for. Capacity accounting is deliberately lock-free and therefore approximate
		// at the saturation boundary: a dequeue frees a slot slightly before it decrements the count,
		// so a racing enqueue can drop when a slot was technically free. That imprecision is allowed.
		// What must hold is that nothing is invented or silently lost — every message is either
		// queued or counted as dropped, and every queued message comes back out exactly once.
		[Test]
		public void Enqueue_WhenDrainedConcurrently_ShouldAccountForEveryMessage()
		{
			const int messageCount = 5000;
			var queue = CreateQueue(capacity: 8);
			var queued = 0;
			var dequeued = 0;

			var producer = new Thread(() =>
			{
				for (var i = 0; i < messageCount; i++)
				{
					if (queue.Enqueue("message", "stack", LogType.Exception, BackgroundThreadId))
					{
						queued++;
					}
				}
			});

			producer.Start();

			// Mirrors the manager's bounded per-frame drain running against a live producer.
			while (producer.IsAlive)
			{
				for (var drained = 0; drained < queue.Capacity && queue.TryDequeue(out _); drained++)
				{
					dequeued++;
				}
			}

			producer.Join();

			while (queue.TryDequeue(out _))
			{
				dequeued++;
			}

			// Join is the barrier that makes the producer's writes visible here.
			Assert.AreEqual(messageCount, queued + queue.TakeDroppedCount(), "every message is either queued or counted as dropped");
			Assert.AreEqual(queued, dequeued, "every queued message is dequeued exactly once");
			Assert.True(queue.IsEmpty);
		}

		// Unobserved Task exceptions arrive on the finalizer thread as an AggregateException and
		// share this queue with the threaded log callback. The event itself can't be raised on
		// demand, so these cover everything up to that boundary.

		[Test]
		public void EnqueueUnobservedTaskException_ShouldQueueOneMessagePerInnerException()
		{
			var queue = CreateQueue();
			var aggregate = new AggregateException(
				new InvalidOperationException("first"),
				new ArgumentException("second"));

			BugSplatRuntime.EnqueueUnobservedTaskException(queue, aggregate, BackgroundThreadId);

			Assert.True(queue.TryDequeue(out var first));
			Assert.AreEqual($"{typeof(InvalidOperationException)}: first", first.LogMessage);
			Assert.AreEqual(LogType.Exception, first.Type);

			Assert.True(queue.TryDequeue(out var second));
			Assert.AreEqual($"{typeof(ArgumentException)}: second", second.LogMessage);
			Assert.True(queue.IsEmpty);
		}

		// A Task awaiting other Tasks faults with aggregates inside aggregates. Reporting the
		// wrapper would bucket unrelated failures together, so Flatten runs first.
		[Test]
		public void EnqueueUnobservedTaskException_WhenNested_ShouldQueueTheLeafExceptions()
		{
			var queue = CreateQueue();
			var aggregate = new AggregateException(
				new AggregateException(new InvalidOperationException("leaf")));

			BugSplatRuntime.EnqueueUnobservedTaskException(queue, aggregate, BackgroundThreadId);

			Assert.True(queue.TryDequeue(out var message));
			Assert.AreEqual($"{typeof(InvalidOperationException)}: leaf", message.LogMessage);
			Assert.True(queue.IsEmpty);
		}

		// An exception that never propagated has no stack trace. Unity's log callback delivers an
		// empty string in that case, not null, and the reporter concatenates it either way.
		[Test]
		public void EnqueueUnobservedTaskException_WhenStackTraceIsNull_ShouldQueueEmptyStackTrace()
		{
			var queue = CreateQueue();

			BugSplatRuntime.EnqueueUnobservedTaskException(
				queue, new AggregateException(new Exception("never thrown")), BackgroundThreadId);

			Assert.True(queue.TryDequeue(out var message));
			Assert.AreEqual(string.Empty, message.StackTrace);
		}

		[Test]
		public void EnqueueUnobservedTaskException_WhenAggregateIsEmpty_ShouldQueueTheAggregate()
		{
			var queue = CreateQueue();

			BugSplatRuntime.EnqueueUnobservedTaskException(queue, new AggregateException(), BackgroundThreadId);

			Assert.True(queue.TryDequeue(out var message));
			StringAssert.Contains(typeof(AggregateException).ToString(), message.LogMessage);
		}

		[Test]
		public void EnqueueUnobservedTaskException_WhenQueueOrExceptionIsNull_ShouldNotThrow()
		{
			Assert.DoesNotThrow(() =>
				BugSplatRuntime.EnqueueUnobservedTaskException(null, new AggregateException(), BackgroundThreadId));
			Assert.DoesNotThrow(() =>
				BugSplatRuntime.EnqueueUnobservedTaskException(CreateQueue(), null, BackgroundThreadId));
		}

		[Test]
		public void IsReportable_ShouldOnlyAllowExceptions()
		{
			Assert.True(BackgroundLogMessageQueue.IsReportable(LogType.Exception));
			Assert.False(BackgroundLogMessageQueue.IsReportable(LogType.Error));
			Assert.False(BackgroundLogMessageQueue.IsReportable(LogType.Assert));
			Assert.False(BackgroundLogMessageQueue.IsReportable(LogType.Warning));
			Assert.False(BackgroundLogMessageQueue.IsReportable(LogType.Log));
		}
	}
}
