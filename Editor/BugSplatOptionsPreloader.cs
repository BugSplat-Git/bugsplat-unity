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
	/// </summary>
	internal sealed class BugSplatOptionsPreloader : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		public int callbackOrder => 0;

		private static BugSplatOptions added;

		public void OnPreprocessBuild(BuildReport report)
		{
			var options = BugSplatProjectOptions.Get();

			if (options == null)
			{
				var count = BugSplatProjectOptions.FindAll().Length;
				throw new BuildFailedException(
					count > 1
						? $"BugSplat: {count} BugSplat Options assets exist and none is selected, so the player would report nothing. Choose one in Edit > Project Settings > BugSplat."
						: "BugSplat: no BugSplat Options asset is selected, so the player would report nothing. Create or select one in Edit > Project Settings > BugSplat. If your code calls BugSplat.Initialize itself, select an asset with Initialize Automatically turned off.");
			}

			if (options.InitializeAutomatically && string.IsNullOrEmpty(options.Database))
			{
				throw new BuildFailedException(
					"BugSplat: the selected BugSplat Options asset has an empty Database, so the player would report nothing. Set it in Edit > Project Settings > BugSplat.");
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
	}
}
