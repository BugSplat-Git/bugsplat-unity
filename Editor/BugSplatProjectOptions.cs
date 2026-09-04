using System.Linq;
using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// The one place the editor answers "which options asset does this project use". Stored as an
	/// EditorBuildSettings config object, which is what Unity's own packages use for a per-project
	/// asset selection: it lives in ProjectSettings, so it is versioned with the project, and it
	/// survives the asset being moved or renamed.
	/// </summary>
	internal static class BugSplatProjectOptions
	{
		/// <summary>
		/// The selected asset, or null. A project with exactly one options asset and no selection
		/// gets that asset selected on the spot - that is every 4.x project on the day it upgrades,
		/// and it should need no clicks. More than one and none selected stays null: picking one
		/// silently is the arbitrary-asset problem this replaces.
		/// </summary>
		internal static BugSplatOptions Get()
		{
			if (EditorBuildSettings.TryGetConfigObject(BugSplatOptions.ConfigObjectKey, out BugSplatOptions selected)
				&& selected != null)
			{
				return selected;
			}

			var candidates = FindAll();
			if (candidates.Length == 1)
			{
				Set(candidates[0]);
				return candidates[0];
			}

			return null;
		}

		internal static void Set(BugSplatOptions options)
		{
			if (options == null)
			{
				EditorBuildSettings.RemoveConfigObject(BugSplatOptions.ConfigObjectKey);
				return;
			}

			EditorBuildSettings.AddConfigObject(BugSplatOptions.ConfigObjectKey, options, true);
		}

		internal static BugSplatOptions[] FindAll()
		{
			return AssetDatabase.FindAssets("t:" + nameof(BugSplatOptions))
				.Select(AssetDatabase.GUIDToAssetPath)
				.Select(AssetDatabase.LoadAssetAtPath<BugSplatOptions>)
				.Where(options => options != null)
				.ToArray();
		}
	}
}
