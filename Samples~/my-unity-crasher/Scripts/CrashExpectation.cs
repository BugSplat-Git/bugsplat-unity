using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Crasher
{
	/// <summary>
	/// Records what a crash was supposed to look like, from inside the process, just
	/// before it happens.
	///
	/// This exists because no debugger can tell us afterwards. A Unity Mono crash dump
	/// carries the jitted code ranges but no method names, so the managed half of the
	/// stack is unrecoverable post-mortem. The program, on the other hand, knows exactly
	/// which methods it called. That makes this file better ground truth than any tool's
	/// output, and it settles inlining too, because the runtime reports the frames that
	/// actually exist rather than the ones that were written.
	///
	/// The file lands beside the player log in <see cref="Application.persistentDataPath"/>
	/// so a harness can collect it with the dump.
	/// </summary>
	public static class CrashExpectation
	{
		public const string FileName = "bugsplat-expected-stack.txt";

		public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

		/// <summary>
		/// Writes the managed stack as it stands at the call site, plus enough context to
		/// tell one build's expectations from another's.
		///
		/// Call it as close to the crash as possible. Called from the menu it records the
		/// dispatch point, which is one or two frames above a scenario that crashes
		/// synchronously and nowhere near one that crashes on another thread or a frame
		/// later — those scenarios should call it themselves at the crash site.
		/// </summary>
		public static void Record(string scenarioName)
		{
			try
			{
				var text = new StringBuilder();
				text.AppendLine($"# scenario: {scenarioName}");
				text.AppendLine($"# backend: {ScriptingBackend()}");
				text.AppendLine($"# unity: {Application.unityVersion}");
				text.AppendLine($"# product: {Application.productName} {Application.version}");
				text.AppendLine($"# platform: {Application.platform}");
				text.AppendLine($"# debugger-attached: {Debug.isDebugBuild}");
				text.AppendLine($"# utc: {DateTime.UtcNow:O}");
				text.AppendLine($"# thread: {Environment.CurrentManagedThreadId}");
				text.AppendLine();

				// fNeedFileInfo: file and line come from the .pdb or .mdb beside the
				// assembly, so they are present in a build that shipped debug symbols and
				// absent otherwise. The frame names do not depend on them.
				var stack = new StackTrace(1, true);
				for (int i = 0; i < stack.FrameCount; i++)
				{
					var frame = stack.GetFrame(i);
					var method = frame?.GetMethod();
					if (method == null)
					{
						continue;
					}

					var type = method.DeclaringType != null ? method.DeclaringType.FullName : "<global>";
					var file = frame.GetFileName();
					var line = frame.GetFileLineNumber();
					var where = string.IsNullOrEmpty(file) ? string.Empty : $"  [{file}:{line}]";
					text.AppendLine($"{i,4}  {type}.{method.Name}{where}");
				}

				File.WriteAllText(Path, text.ToString());
				Debug.Log($"[BugSplat] expected stack written to {Path}");
			}
			catch (Exception ex)
			{
				// Never let recording an expectation change what the scenario does: the
				// crash under test is the point, this file is only evidence about it.
				Debug.LogWarning($"[BugSplat] could not write the expected stack: {ex.Message}");
			}
		}

		static string ScriptingBackend()
		{
#if ENABLE_IL2CPP
			return "IL2CPP";
#elif ENABLE_MONO
			return "Mono";
#else
			return "unknown";
#endif
		}
	}
}
