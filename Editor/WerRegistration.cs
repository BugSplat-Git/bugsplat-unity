#if UNITY_EDITOR_WIN
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Registers BugSplatWer.dll as a Windows Error Reporting runtime exception helper for a
	/// locally built player.
	///
	/// Fail-fast terminations — stack buffer overrun (0xC0000409), heap corruption (0xC0000374),
	/// and __fastfail — bypass every in-process exception handler, so BugSplat's crash handler
	/// never sees them. Windows only hands them to BugSplatWer.dll if the DLL's full path is
	/// named by a value under RuntimeExceptionHelperModules, which lives in HKLM and therefore
	/// requires administrator rights to write.
	///
	/// This is a developer convenience for testing. Shipping games must have their installer
	/// write the value at install time and remove it on uninstall.
	/// </summary>
	internal static class WerRegistration
	{
		const string WerKeyPath = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules";
		const string WerDllName = "BugSplatWer.dll";
		const int ErrorCancelled = 1223;

		// A 64-bit player is handled by the 64-bit WerFault; a 32-bit player by the SysWOW64 one,
		// whose HKLM\SOFTWARE reads are redirected to Wow6432Node.
		const string View64 = "/reg:64";
		const string View32 = "/reg:32";

		[MenuItem("BugSplat/Windows/Register WER Handler...", false, 100)]
		static void Register() => SetRegistration(true);

		[MenuItem("BugSplat/Windows/Unregister WER Handler...", false, 101)]
		static void Unregister() => SetRegistration(false);

		[MenuItem("BugSplat/Windows/Check WER Handler Registration...", false, 120)]
		static void Check()
		{
			var werDll = PromptForWerDll();
			if (werDll == null) return;

			var report =
				$"BugSplatWer.dll:\n{werDll}\n\n" +
				$"Registered (64-bit view): {(IsRegistered(werDll, View64) ? "yes" : "no")}\n" +
				$"Registered (32-bit view): {(IsRegistered(werDll, View32) ? "yes" : "no")}\n\n" +
				"A 64-bit player is handled by the 64-bit view; a 32-bit player is handled by the 32-bit view.";

			Debug.Log($"BugSplat. WER registration check.\n{report}");
			EditorUtility.DisplayDialog("BugSplat — WER Registration", report, "OK");
		}

		static void SetRegistration(bool register)
		{
			var werDll = PromptForWerDll();
			if (werDll == null) return;

			// Write both registry views: a 32-bit player is handled by the SysWOW64 WerFault,
			// whose HKLM\SOFTWARE reads are redirected to Wow6432Node. Writing both is cheaper
			// than deciding which one this build needs.
			// The value data is ignored — WER and the SDK only check that the value exists — so
			// use 0 to match the entries Windows registers for its own helper modules.
			var command = register
				? $"add \"HKLM\\{WerKeyPath}\" /v \"{werDll}\" /t REG_DWORD /d 0 /f"
				: $"delete \"HKLM\\{WerKeyPath}\" /v \"{werDll}\" /f";

			var startInfo = new ProcessStartInfo("cmd.exe", $"/c reg {command} /reg:64 & reg {command} /reg:32")
			{
				UseShellExecute = true, // required for Verb = runas
				Verb = "runas",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			try
			{
				using (var process = Process.Start(startInfo))
				{
					process.WaitForExit();
				}
			}
			catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
			{
				Debug.Log("BugSplat. WER registration cancelled — administrator approval was declined. Nothing changed.");
				EditorUtility.DisplayDialog(
					"BugSplat — WER Registration",
					"Administrator approval was declined, so nothing changed.",
					"OK");
				return;
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat. Failed to update WER registration: {ex.Message}");
				return;
			}

			// UseShellExecute forbids output redirection, so read the value back instead of
			// trusting reg.exe's exit code.
			var registered64 = IsRegistered(werDll, View64);
			var registered32 = IsRegistered(werDll, View32);
			var succeeded = register ? registered64 && registered32 : !registered64 && !registered32;

			var summary = register
				? $"Registered BugSplatWer.dll for Windows Error Reporting:\n{werDll}\n\nFail-fast crashes from this build will now be reported."
				: $"Unregistered BugSplatWer.dll from Windows Error Reporting:\n{werDll}\n\nFail-fast crashes from this build will no longer be reported.";

			if (succeeded)
			{
				Debug.Log($"BugSplat. {summary}");
				EditorUtility.DisplayDialog("BugSplat — WER Registration", summary, "OK");
				return;
			}

			var failure =
				$"WER registration did not take effect for:\n{werDll}\n\n" +
				$"64-bit view: {(registered64 ? "present" : "absent")}\n" +
				$"32-bit view: {(registered32 ? "present" : "absent")}\n\n" +
				"Some endpoint-protection products monitor RuntimeExceptionHelperModules because it " +
				"is a known persistence location. Check your security software if this keeps failing.";

			Debug.LogError($"BugSplat. {failure}");
			EditorUtility.DisplayDialog("BugSplat — WER Registration", failure, "OK");
		}

		/// <summary>
		/// Asks for the built player executable and returns the full path to the BugSplatWer.dll
		/// beside it, or null if the user cancelled or the DLL is missing.
		/// </summary>
		static string PromptForWerDll()
		{
			var lastBuild = EditorUserBuildSettings.GetBuildLocation(EditorUserBuildSettings.activeBuildTarget);
			var startDirectory = string.IsNullOrEmpty(lastBuild) ? "" : Path.GetDirectoryName(lastBuild);

			var exePath = EditorUtility.OpenFilePanel("Select your built Windows player", startDirectory, "exe");
			if (string.IsNullOrEmpty(exePath)) return null;

			var werDll = NormalizeWerDllPath(exePath);

			if (!File.Exists(werDll))
			{
				var message =
					$"{WerDllName} was not found next to {Path.GetFileName(exePath)}.\n\n" +
					"Enable UseNativeCrashReportingForWindows on your BugSplatOptions asset and rebuild — " +
					"the post-build step copies it next to the executable.";

				Debug.LogError($"BugSplat. {message}");
				EditorUtility.DisplayDialog("BugSplat — WER Registration", message, "OK");
				return null;
			}

			return werDll;
		}

		/// <summary>
		/// Returns the full, backslash-separated path to BugSplatWer.dll beside the given executable.
		/// The separator matters: the SDK builds the path it looks up with GetModuleFileNameW, which
		/// yields backslashes, so a value named with forward slashes (as EditorUtility.OpenFilePanel
		/// returns) never matches and WER stays silently disarmed.
		/// </summary>
		internal static string NormalizeWerDllPath(string exePath)
		{
			var buildDir = Path.GetDirectoryName(Path.GetFullPath(exePath));
			return Path.GetFullPath(Path.Combine(buildDir, WerDllName)).Replace('/', '\\');
		}

		/// <summary>
		/// Reads back whether the value exists in the given registry view. Uses reg.exe rather than
		/// Microsoft.Win32.Registry, which is not part of netstandard2.1 and so may not resolve
		/// depending on the project's API compatibility level. Reads need no elevation.
		/// </summary>
		static bool IsRegistered(string werDll, string registryView)
		{
			var startInfo = new ProcessStartInfo("reg.exe", BuildQueryArguments(werDll, registryView))
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			try
			{
				using (var process = Process.Start(startInfo))
				{
					process.StandardOutput.ReadToEnd();
					process.StandardError.ReadToEnd();
					process.WaitForExit();
					return process.ExitCode == 0;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"BugSplat. Could not read WER registration ({registryView}): {ex.Message}");
				return false;
			}
		}

		internal static string BuildQueryArguments(string werDll, string registryView) =>
			$"query \"HKLM\\{WerKeyPath}\" /v \"{werDll}\" {registryView}";
	}
}
#endif
