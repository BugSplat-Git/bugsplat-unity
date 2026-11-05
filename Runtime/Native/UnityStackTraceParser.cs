using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BugSplatUnity.Runtime.Native
{
	/// <summary>
	/// A managed exception as BugSplat's structured report wants it: a type, a message and one
	/// frame per line, with the file and line number split out where Unity provided them.
	/// </summary>
	internal sealed class ParsedStackTrace
	{
		public string ExceptionType = "Exception";
		public string Message = string.Empty;
		public readonly List<ReportFrame> Frames = new List<ReportFrame>();
	}

	/// <summary>
	/// Turns the two stack trace shapes Unity produces into structured frames, keeping Unity's own
	/// text for each frame's function so a report reads exactly like the console does.
	///
	/// Shape 1, from Application.logMessageReceived (the message on the first line, then frames):
	///   Crasher.CrashScenarios.ThrowUnhandled () (at Assets/Scripts/CrashScenarios.cs:210)
	///   UnityEngine.Debug:LogException(Exception)
	///
	/// Shape 2, from Exception.ToString():
	///   System.Exception: BugSplat sample
	///     at Crasher.CrashScenarios.ThrowUnhandled () [0x00011] in C:\game\Assets\Scripts\CrashScenarios.cs:210
	///     at Crasher.Menu.Run () [0x00000] in &lt;00000000000000000000000000000000&gt;:0
	/// </summary>
	internal static class UnityStackTraceParser
	{
		// "function [0x...] in file:line", "function (at file:line)", "function in file:line"
		private static readonly Regex FrameWithLocation = new Regex(
			@"^\s*(?:at\s+)?(?<func>.+?)(?:\s+\[0x[0-9A-Fa-f]+\])?\s+(?:\(at\s+|in\s+)(?<file>.+?):(?<line>\d+)\)?\s*$",
			RegexOptions.Compiled);

		private static readonly Regex FrameWithoutLocation = new Regex(
			@"^\s*(?:at\s+)?(?<func>\S.*?)(?:\s+\[0x[0-9A-Fa-f]+\])?\s*$",
			RegexOptions.Compiled);

		// "System.NullReferenceException: Object reference not set" or "Exception: message"
		private static readonly Regex Header = new Regex(
			@"^\s*(?<type>(?:[A-Za-z_][\w.`+]*)?(?:Exception|Error|Fault|Abort))(?::\s*(?<message>.*))?$",
			RegexOptions.Compiled);

		private const int MaxFrames = 128;

		/// <param name="message">The log message, or null when the text carries its own header line.</param>
		/// <param name="stackTrace">Unity's stack trace text, or Exception.ToString().</param>
		public static ParsedStackTrace Parse(string message, string stackTrace)
		{
			var parsed = new ParsedStackTrace();
			var text = stackTrace ?? string.Empty;

			if (!string.IsNullOrEmpty(message))
			{
				ApplyHeader(parsed, message.Split('\n')[0].TrimEnd('\r'));
			}

			var lines = text.Split('\n');
			var sawFrame = false;
			foreach (var raw in lines)
			{
				var line = raw.TrimEnd('\r').TrimEnd();
				if (line.Length == 0) continue;

				var located = FrameWithLocation.Match(line);
				if (located.Success)
				{
					AddFrame(parsed, located.Groups["func"].Value, located.Groups["file"].Value, located.Groups["line"].Value);
					sawFrame = true;
					continue;
				}

				// The first non-frame line before any frame is the exception header (shape 2), or a
				// continuation of the message. Later non-frame lines ("Rethrow as ...", "--- End of
				// inner exception stack trace ---") are kept as frames so nothing Unity printed is lost.
				if (!sawFrame && string.IsNullOrEmpty(message) && ApplyHeader(parsed, line))
				{
					message = parsed.Message;  // header consumed
					continue;
				}

				var plain = FrameWithoutLocation.Match(line);
				if (plain.Success)
				{
					AddFrame(parsed, plain.Groups["func"].Value, string.Empty, "0");
					sawFrame = true;
				}
			}

			return parsed;
		}

		private static bool ApplyHeader(ParsedStackTrace parsed, string line)
		{
			var header = Header.Match(line);
			if (header.Success)
			{
				parsed.ExceptionType = header.Groups["type"].Value;
				parsed.Message = header.Groups["message"].Success ? header.Groups["message"].Value.Trim() : string.Empty;
				return true;
			}

			// No recognizable type: the whole line is the message.
			if (string.IsNullOrEmpty(parsed.Message))
			{
				parsed.Message = line.Trim();
				return true;
			}

			return false;
		}

		private static void AddFrame(ParsedStackTrace parsed, string function, string file, string line)
		{
			if (parsed.Frames.Count >= MaxFrames) return;

			int.TryParse(line, out var lineNumber);
			// IL2CPP release builds print "<00000000000000000000000000000000>:0" when they have no file.
			if (file.StartsWith("<", StringComparison.Ordinal) && file.EndsWith(">", StringComparison.Ordinal))
			{
				file = string.Empty;
				lineNumber = 0;
			}

			parsed.Frames.Add(new ReportFrame { Function = function.Trim(), File = file.Trim(), Line = lineNumber });
		}
	}
}
