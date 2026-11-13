using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using BugSplatUnity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace BugSplatUnity.EditorTests
{
	public class ProcessRunnerTests
	{
		static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor;

		static ProcessStartInfo Shell(string windowsCommand, string posixCommand) =>
			IsWindows
				? new ProcessStartInfo("cmd.exe", $"/d /c \"{windowsCommand}\"")
				: new ProcessStartInfo("/bin/sh", $"-c \"{posixCommand}\"");

		static ProcessStartInfo LongRunning() =>
			IsWindows
				? new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1")
				: new ProcessStartInfo("/bin/sleep", "60");

		[Test]
		public void Run_ShouldReturnTheExitCodeAndBothStreams()
		{
			var result = ProcessRunner.Run(
				Shell("echo to-stdout& echo to-stderr 1>&2& exit /b 3", "echo to-stdout; echo to-stderr 1>&2; exit 3"),
				TimeSpan.FromSeconds(30));

			Assert.AreEqual(3, result.ExitCode);
			Assert.IsFalse(result.TimedOut);
			Assert.IsFalse(result.Succeeded);
			StringAssert.Contains("to-stdout", result.Output);
			StringAssert.Contains("to-stderr", result.Output);
		}

		[Test]
		public void Run_ShouldNotDeadlockWhenBothStreamsOutgrowThePipe()
		{
			const int lines = 2000;
			const string text = "0123456789012345678901234567890123456789012345678901234567890123456789";

			var result = ProcessRunner.Run(
				Shell(
					$"for /L %i in (1,1,{lines}) do @(echo {text}& echo {text} 1>&2)",
					$"i=0; while [ $i -lt {lines} ]; do echo {text}; echo {text} 1>&2; i=$((i+1)); done"),
				TimeSpan.FromMinutes(2));

			Assert.IsFalse(result.TimedOut);
			Assert.AreEqual(0, result.ExitCode);
			Assert.AreEqual(lines * 2, result.Output.Split('\n').Count(line => line.Trim() == text));
		}

		[Test]
		public void Run_ShouldKillAProcessThatOutlivesTheTimeout()
		{
			var result = ProcessRunner.Run(LongRunning(), TimeSpan.FromSeconds(1));

			Assert.IsTrue(result.TimedOut);
			Assert.IsFalse(result.Succeeded);
			Assert.AreEqual(-1, result.ExitCode);
			Assert.Less(result.Elapsed, TimeSpan.FromSeconds(20));
		}

		[Test]
		public void Run_ShouldStopWhenOnPollCancels()
		{
			var stopwatch = Stopwatch.StartNew();

			var result = ProcessRunner.Run(LongRunning(), TimeSpan.FromMinutes(5), (elapsed, _) => elapsed > TimeSpan.FromMilliseconds(500));

			Assert.IsTrue(result.Cancelled);
			Assert.IsFalse(result.TimedOut);
			Assert.AreEqual(-1, result.ExitCode);
			Assert.Less(stopwatch.Elapsed, TimeSpan.FromSeconds(20));
		}

		[Test]
		public void Run_ShouldPassTheLastOutputLineToOnPoll()
		{
			var seen = new List<string>();

			var result = ProcessRunner.Run(
				Shell("echo first-line& echo progress-line& ping -n 3 127.0.0.1 >nul", "echo first-line; echo progress-line; sleep 2"),
				TimeSpan.FromSeconds(30),
				(_, line) =>
				{
					seen.Add(line);
					return false;
				},
				TimeSpan.FromMilliseconds(50));

			Assert.AreEqual(0, result.ExitCode);
			CollectionAssert.Contains(seen, "progress-line");
		}

		[Test]
		public void Run_ShouldWaitIndefinitelyWhenTheTimeoutIsInfinite()
		{
			var result = ProcessRunner.Run(Shell("exit /b 0", "exit 0"), System.Threading.Timeout.InfiniteTimeSpan);

			Assert.IsTrue(result.Succeeded);
		}

		[Test]
		public void Run_ShouldThrowWhenTheExecutableDoesNotExist()
		{
			Assert.Throws<Win32Exception>(() =>
				ProcessRunner.Run(new ProcessStartInfo("bugsplat-no-such-executable-" + Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(5)));
		}

		[TestCase(5, "0:05")]
		[TestCase(30 * 60, "30:00")]
		[TestCase(3723, "1:02:03")]
		public void FormatDuration_ShouldUseMinutesAndSeconds(int seconds, string expected)
		{
			Assert.AreEqual(expected, ProcessRunner.FormatDuration(TimeSpan.FromSeconds(seconds)));
		}
	}
}
