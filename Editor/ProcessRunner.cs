using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace BugSplatUnity.Editor
{
	internal sealed class ProcessRunResult
	{
		/// <summary>The process's exit code, or -1 when it was stopped by a timeout or cancellation.</summary>
		public int ExitCode = -1;
		public bool TimedOut;
		public bool Cancelled;
		/// <summary>Standard output and standard error, interleaved in the order they arrived.</summary>
		public string Output = string.Empty;
		public TimeSpan Elapsed;

		public bool Succeeded => !TimedOut && !Cancelled && ExitCode == 0;
	}

	/// <summary>
	/// Runs a child process with both output streams read asynchronously, so a chatty process can never
	/// fill a pipe and deadlock against a caller waiting for it to exit, and with a bound on how long the
	/// caller waits. A process still running at the timeout, or when <c>onPoll</c> asks to cancel, is killed.
	/// </summary>
	internal static class ProcessRunner
	{
		internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(250);
		static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);
		static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(10);

		/// <param name="timeout">Zero, negative, or <see cref="Timeout.InfiniteTimeSpan"/> waits indefinitely.</param>
		/// <param name="onPoll">Called on the calling thread while the process runs, with the elapsed time and the last
		/// non-empty output line. Return true to cancel.</param>
		/// <exception cref="Win32Exception">The process could not be started.</exception>
		internal static ProcessRunResult Run(
			ProcessStartInfo startInfo,
			TimeSpan timeout,
			Func<TimeSpan, string, bool> onPoll = null,
			TimeSpan? pollInterval = null)
		{
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			startInfo.CreateNoWindow = true;

			var gate = new object();
			var output = new StringBuilder();
			string lastLine = null;
			var stdoutClosed = new ManualResetEventSlim();
			var stderrClosed = new ManualResetEventSlim();

			DataReceivedEventHandler Collect(ManualResetEventSlim closed) => (_, e) =>
			{
				if (e.Data == null)
				{
					closed.Set();
					return;
				}

				lock (gate)
				{
					output.AppendLine(e.Data);
					if (!string.IsNullOrWhiteSpace(e.Data))
						lastLine = e.Data.Trim();
				}
			};

			var result = new ProcessRunResult();
			var interval = (int)Math.Max(1, (pollInterval ?? DefaultPollInterval).TotalMilliseconds);
			var bounded = timeout > TimeSpan.Zero;

			using (var process = new Process { StartInfo = startInfo })
			{
				process.OutputDataReceived += Collect(stdoutClosed);
				process.ErrorDataReceived += Collect(stderrClosed);

				process.Start();
				var stopwatch = Stopwatch.StartNew();
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				while (!process.WaitForExit(interval))
				{
					if (bounded && stopwatch.Elapsed >= timeout)
					{
						result.TimedOut = true;
						break;
					}

					string line;
					lock (gate)
						line = lastLine;

					if (onPoll != null && onPoll(stopwatch.Elapsed, line))
					{
						result.Cancelled = true;
						break;
					}
				}

				if (result.TimedOut || result.Cancelled)
				{
					Kill(process);
				}
				else
				{
					// A grandchild that inherited the pipes can hold them open after the process exits.
					stdoutClosed.Wait(OutputDrainTimeout);
					stderrClosed.Wait(OutputDrainTimeout);
					result.ExitCode = process.ExitCode;
				}

				result.Elapsed = stopwatch.Elapsed;

				try
				{
					process.CancelOutputRead();
					process.CancelErrorRead();
				}
				catch (InvalidOperationException)
				{
				}
			}

			lock (gate)
				result.Output = output.ToString();

			return result;
		}

		static void Kill(Process process)
		{
			try
			{
				if (!process.HasExited)
					process.Kill();
			}
			catch (Exception e) when (e is InvalidOperationException || e is Win32Exception || e is NotSupportedException)
			{
			}

			try
			{
				process.WaitForExit((int)KillTimeout.TotalMilliseconds);
			}
			catch (SystemException)
			{
			}
		}

		/// <summary>Formats a duration as m:ss, or h:mm:ss from an hour up.</summary>
		internal static string FormatDuration(TimeSpan duration) =>
			duration.TotalHours >= 1
				? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
				: $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
	}
}
