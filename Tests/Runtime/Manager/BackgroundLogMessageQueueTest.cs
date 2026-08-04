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
