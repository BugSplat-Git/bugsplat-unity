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
	/// Carries the project's options asset into the player, and refuses to build a player that could
	/// only report nothing.
	///
	/// A player has no EditorBuildSettings, so the selected asset is added to the preloaded assets
	/// for the duration of the build: those load before RuntimeInitializeOnLoadMethod(BeforeSceneLoad)
	/// runs, which is where BugSplat reads it. It is removed again afterwards so the build does not
	/// leave a change behind in ProjectSettings.asset.
	///
	/// With BUGSPLAT_MANUAL_INITIALIZE defined for the target, the project owns initialization: no
	/// check, no failure, though a selected asset is still preloaded in case the code reads it.
	/// </summary>
	internal sealed class BugSplatOptionsPreloader : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		public int callbackOrder => 0;

		private static BugSplatOptions added;

		public void OnPreprocessBuild(BuildReport report)
		{
			var manual = IsManualInitialize(report);
			var options = BugSplatProjectOptions.Get();

			if (options == null)
			{
				if (manual)
				{
					return;
				}

				var count = BugSplatProjectOptions.FindAll().Length;
				throw new BuildFailedException(
					(count > 1
						? $"BugSplat: {count} BugSplatOptions assets exist and none is selected, so the player would report nothing. Choose one in Edit > Project Settings > BugSplat, or with BugSplatUnity.Editor.BugSplatProjectOptions.Set."
						: "BugSplat: no BugSplatOptions asset is selected, so the player would report nothing. " + BugSplatOptions.ConfigureHint)
					+ $" If your code calls BugSplat.Initialize itself, define {BugSplatOptions.ManualInitializeDefine}.");
			}

			if (!manual && options.InitializeAutomatically && string.IsNullOrEmpty(options.Database))
			{
				throw new BuildFailedException(
					$"BugSplat: {AssetDatabase.GetAssetPath(options)} has an empty Database, so the player would report nothing. Set it in Edit > Project Settings > BugSplat, in the asset file, or with BugSplatUnity.Editor.BugSplatSetup.Configure.");
			}

			// Added even when Initialize Automatically is off: BugSplat still reads the asset at
			// startup to learn that it should stay quiet, and without it would warn that nothing is
			// configured.
			var preloaded = PlayerSettings.GetPreloadedAssets();
			if (preloaded.Contains(options))
			{
				return;
			}

			PlayerSettings.SetPreloadedAssets(preloaded.Append(options).ToArray());
			added = options;
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

		// The define is read for the target being built, which is what the player's own #if sees.
		// Without a report - a direct call - fall back to the editor's compile-time value.
		private static bool IsManualInitialize(BuildReport report)
		{
			if (report != null)
			{
				try
				{
					var target = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup);
					return HasManualInitializeDefine(PlayerSettings.GetScriptingDefineSymbols(target));
				}
				catch (ArgumentException)
				{
					// An unknown platform group; nothing to read, so fall through.
				}
			}

#if BUGSPLAT_MANUAL_INITIALIZE
			return true;
#else
			return false;
#endif
		}

		internal static bool HasManualInitializeDefine(string defines)
		{
			return (defines ?? string.Empty)
				.Split(';')
				.Select(define => define.Trim())
				.Contains(BugSplatOptions.ManualInitializeDefine);
		}
	}
}
