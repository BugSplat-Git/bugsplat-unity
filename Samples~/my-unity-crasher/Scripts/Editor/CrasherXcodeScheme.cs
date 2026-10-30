using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Crasher.CrasherEditor
{
	/// <summary>
	/// Turns off "Debug executable" in the exported Xcode project's Run action.
	///
	/// This sample exists to crash, and a crash under a debugger never reaches BugSplat: LLDB claims
	/// the Mach exception ports before PLCrashReporter can, and bugsplat-apple suppresses hang
	/// detection outright while a debugger is attached. Build And Run leaves the debugger on, so
	/// every scenario in the menu appears to do nothing.
	///
	/// This lives in the sample rather than in the BugSplat package on purpose. Disabling a
	/// project's debugger is not a decision a crash reporter should make for its users, but it is the
	/// right default for a project whose entire purpose is to crash.
	/// </summary>
	public static class CrasherXcodeScheme
	{
		// BugSplat's own post-process is order 1; run after it.
		[PostProcessBuild(2)]
		public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
		{
			if (target != BuildTarget.iOS)
			{
				return;
			}

			// Both shared schemes (xcshareddata) and per-user ones (xcuserdata), because Xcode
			// prefers a per-user scheme when it exists and Unity only writes the shared one.
			var schemes = Directory.GetFiles(pathToBuiltProject, "*.xcscheme", SearchOption.AllDirectories);
			var patched = 0;

			foreach (var scheme in schemes)
			{
				if (DisableDebuggerForRunAction(scheme))
				{
					patched++;
				}
			}

			if (patched > 0)
			{
				Debug.Log(
					$"BugSplat sample: turned off \"Debug executable\" in {patched} Xcode scheme(s) so crashes " +
					"reach BugSplat instead of the debugger. Re-enable it in Product > Scheme > Edit Scheme > " +
					"Run > Info if you need to set breakpoints. Only the Run action is changed; Test and " +
					"Profile are left alone.");
			}
		}

		private static bool DisableDebuggerForRunAction(string schemePath)
		{
			try
			{
				var document = new XmlDocument();
				document.Load(schemePath);

				var changed = false;

				// Only LaunchAction. TestAction wants LLDB, and ProfileAction never attaches a debugger.
				foreach (XmlNode node in document.GetElementsByTagName("LaunchAction"))
				{
					if (!(node is XmlElement launchAction))
					{
						continue;
					}

					// An empty debugger identifier with the PosixSpawn launcher is what Xcode itself
					// writes when the checkbox is cleared.
					if (launchAction.GetAttribute("selectedDebuggerIdentifier") == string.Empty &&
						launchAction.GetAttribute("selectedLauncherIdentifier") == "Xcode.IDEFoundation.Launcher.PosixSpawn")
					{
						continue;
					}

					launchAction.SetAttribute("selectedDebuggerIdentifier", string.Empty);
					launchAction.SetAttribute("selectedLauncherIdentifier", "Xcode.IDEFoundation.Launcher.PosixSpawn");
					changed = true;
				}

				if (changed)
				{
					document.Save(schemePath);
				}

				return changed;
			}
			catch (System.Exception ex)
			{
				// A scheme this did not expect is not worth failing a build over.
				Debug.LogWarning($"BugSplat sample: could not update \"{schemePath}\": {ex.Message}");
				return false;
			}
		}
	}
}
