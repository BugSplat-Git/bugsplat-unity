using System;
using System.Linq;
using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Carries the project's options asset into the player, and refuses to ship a release player that
	/// could only report nothing.
	///
	/// A player has no EditorBuildSettings, so the selected asset is added to the preloaded assets
	/// for the duration of the build: those load before RuntimeInitializeOnLoadMethod(BeforeSceneLoad)
	/// runs, which is where BugSplat reads it. It is removed again afterwards so the build does not
	/// leave a change behind in ProjectSettings.asset.
	///
	/// Nothing is checked when BugSplat is off for the build - the BUGSPLAT_DISABLED define, an asset
	/// with Enabled unchecked, or BUGSPLAT_MANUAL_INITIALIZE, where the project owns initialization.
	/// A misconfiguration is a warning on a development build and fatal on a release build: an
	/// unconfigured development build is someone iterating, while a release player that silently
	/// reports nothing is the mistake worth stopping.
	/// </summary>
	internal sealed class BugSplatOptionsPreloader : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		public int callbackOrder => 0;

		private static BugSplatOptions added;

		public void OnPreprocessBuild(BuildReport report)
		{
			// First, so postprocess only ever undoes what this build did. A build that fails after
			// this callback never reaches postprocess, which would otherwise leave a stale value
			// here and remove a preloaded asset the next build did not add.
			added = null;

			if (IsDefined(report, BugSplatOptions.DisabledDefine, DisabledHere))
			{
				// Off for this target: AutoInitialize returns before reading the asset, so there is
				// nothing to validate and nothing worth carrying into the player.
				return;
			}

			var manual = IsDefined(report, BugSplatOptions.ManualInitializeDefine, ManualInitializeHere);
			var options = BugSplatProjectOptions.Get();

			if (options == null)
			{
				if (manual)
				{
					return;
				}

				var count = BugSplatProjectOptions.FindAll().Length;
				RequireConfiguration(
					report,
					count > 1
						? $"BugSplat: {count} BugSplatOptions assets exist and none is selected, so the player would report nothing. Choose one in Edit > Project Settings > BugSplat, or with BugSplatUnity.Editor.BugSplatProjectOptions.Set."
						: "BugSplat: no BugSplatOptions asset is selected, so the player would report nothing. " + BugSplatOptions.ConfigureHint,
					$" To ship without BugSplat, define {BugSplatOptions.DisabledDefine}; if your code calls BugSplat.Initialize itself, define {BugSplatOptions.ManualInitializeDefine}.");
				return;
			}

			if (!options.Enabled)
			{
				// Turned off on the asset. Still preloaded, so the player reads it, finds Enabled
				// unchecked, and stays quiet rather than warning that nothing is configured.
				Preload(options);
				return;
			}

			// Checked even when Initialize Automatically is off: the project still intends to report
			// through this asset, and an empty database would defeat that whenever it does initialize.
			if (!manual && string.IsNullOrEmpty(options.Database))
			{
				RequireConfiguration(
					report,
					$"BugSplat: {AssetDatabase.GetAssetPath(options)} has an empty Database, so the player would report nothing. Set it in Edit > Project Settings > BugSplat, in the asset file, or with BugSplatUnity.Editor.BugSplatSetup.Configure.",
					$" To ship without BugSplat, uncheck Enabled on the asset or define {BugSplatOptions.DisabledDefine}.");
			}

			// A player has no Project Settings to read, so it takes its options from the preloaded
			// assets - which means a second BugSplatOptions there would make the choice depend on
			// load order. That is the arbitrary-asset problem this whole flow exists to prevent, so
			// it is refused here rather than resolved by luck at runtime.
			var conflicting = PlayerSettings.GetPreloadedAssets()
				.OfType<BugSplatOptions>()
				.Where(asset => asset != options)
				.ToArray();

			if (conflicting.Length > 0)
			{
				var paths = string.Join(", ", conflicting.Select(asset => AssetDatabase.GetAssetPath(asset)));
				RequireConfiguration(
					report,
					$"BugSplat: {paths} {(conflicting.Length == 1 ? "is" : "are")} in Player Settings > Preloaded Assets alongside the selected {AssetDatabase.GetAssetPath(options)}, so which one the player initializes from would depend on load order. Remove the others from Preloaded Assets; BugSplat adds the selected asset itself for the duration of the build.",
					string.Empty);
			}

			Preload(options);
		}

		public void OnPostprocessBuild(BuildReport report)
		{
			if (added == null)
			{
				return;
			}

			PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets().Where(asset => asset != added).ToArray());
			added = null;
		}

		private static void Preload(BugSplatOptions options)
		{
			var preloaded = PlayerSettings.GetPreloadedAssets();
			if (preloaded.Contains(options))
			{
				// Already there before this build - someone else's entry, so postprocess must leave
				// it alone. `added` stays null.
				return;
			}

			PlayerSettings.SetPreloadedAssets(preloaded.Append(options).ToArray());
			added = options;
		}

		/// <summary>
		/// Fails a release build and warns a development one. <paramref name="howToTurnOff"/> is
		/// appended to the failure only: someone iterating does not need to be told how to opt out.
		/// </summary>
		private static void RequireConfiguration(BuildReport report, string problem, string howToTurnOff)
		{
			if (IsDevelopmentBuild(report))
			{
				Debug.LogWarning($"{problem} This is a development build, so it is only a warning - a release build fails.");
				return;
			}

			throw new BuildFailedException(problem + howToTurnOff);
		}

		// Without a report - a direct call - treat it as a release build, the stricter reading.
		private static bool IsDevelopmentBuild(BuildReport report)
		{
			return report != null && IsDevelopmentBuild(report.summary.options);
		}

		internal static bool IsDevelopmentBuild(BuildOptions options)
		{
			return options.HasFlag(BuildOptions.Development);
		}

		private const bool ManualInitializeHere =
#if BUGSPLAT_MANUAL_INITIALIZE
			true;
#else
			false;
#endif

		private const bool DisabledHere =
#if BUGSPLAT_DISABLED
			true;
#else
			false;
#endif

		// Read for the target being built, which is what the player's own #if sees. Without a report
		// there is no target to ask, so fall back to the editor assembly's own compile-time value.
		private static bool IsDefined(BuildReport report, string define, bool fallback)
		{
			if (report != null)
			{
				try
				{
					var target = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup);
					return HasDefine(PlayerSettings.GetScriptingDefineSymbols(target), define);
				}
				catch (ArgumentException)
				{
					// An unknown platform group; nothing to read, so fall through.
				}
			}

			return fallback;
		}

		internal static bool HasDefine(string defines, string define)
		{
			return (defines ?? string.Empty)
				.Split(';')
				.Select(symbol => symbol.Trim())
				.Contains(define);
		}
	}
}
