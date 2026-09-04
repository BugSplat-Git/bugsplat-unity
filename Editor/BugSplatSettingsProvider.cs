using System.Linq;
using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Manager;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Edit > Project Settings > BugSplat: the one place a project configures BugSplat. Chooses the
	/// options asset the package initializes from and draws that asset inline, so setup is "install,
	/// open this page, paste your database" - no scene object, no hunting for an asset.
	/// </summary>
	internal static class BugSplatSettingsProvider
	{
		private const string SettingsPath = "Project/BugSplat";

		private static UnityEditor.Editor optionsEditor;

		[SettingsProvider]
		private static SettingsProvider Create()
		{
			return new SettingsProvider(SettingsPath, SettingsScope.Project)
			{
				label = "BugSplat",
				keywords = new[] { "BugSplat", "crash", "exception", "report", "database", "symbols" },
				guiHandler = _ => OnGUI(),
				deactivateHandler = () =>
				{
					if (optionsEditor != null)
					{
						Object.DestroyImmediate(optionsEditor);
					}

					optionsEditor = null;
				}
			};
		}

		[MenuItem("BugSplat/Settings...", false, 0)]
		private static void OpenSettings()
		{
			SettingsService.OpenProjectSettings(SettingsPath);
		}

		private static void OnGUI()
		{
			var options = BugSplatProjectOptions.Get();

			EditorGUILayout.Space();

			using (new EditorGUILayout.HorizontalScope())
			{
				var picked = (BugSplatOptions)EditorGUILayout.ObjectField(
					new GUIContent("Options Asset", "The BugSplat Options asset this project initializes from."),
					options, typeof(BugSplatOptions), false);

				if (picked != options)
				{
					BugSplatProjectOptions.Set(picked);
					options = picked;
				}

				if (GUILayout.Button("Create", GUILayout.Width(70)))
				{
					options = BugSplatSetup.CreateAsset();
					EditorGUIUtility.PingObject(options);
				}
			}

			DrawStatus(options);
			DrawManagerAdvisory();

			if (options == null)
			{
				return;
			}

			EditorGUILayout.Space();
			UnityEditor.Editor.CreateCachedEditor(options, null, ref optionsEditor);
			optionsEditor.OnInspectorGUI();
		}

		private static void DrawStatus(BugSplatOptions options)
		{
#if BUGSPLAT_MANUAL_INITIALIZE
			EditorGUILayout.HelpBox(
				$"{BugSplatOptions.ManualInitializeDefine} is defined for this build target, so BugSplat does not initialize itself and builds are not checked for an options asset. Your code is responsible for calling BugSplat.Initialize.",
				MessageType.Info);
			if (options == null)
			{
				return;
			}
#endif
			if (options == null)
			{
				var count = BugSplatProjectOptions.FindAll().Length;
				EditorGUILayout.HelpBox(
					count > 1
						? $"{count} BugSplat Options assets exist and none is selected. Choose one above. Until then BugSplat is not initialized, nothing is reported, and builds fail."
						: "No BugSplat Options asset is selected. Click Create, or choose an existing asset. Until then BugSplat is not initialized, nothing is reported, and builds fail.",
					MessageType.Error);
				return;
			}

			if (string.IsNullOrEmpty(options.Database))
			{
				EditorGUILayout.HelpBox(
					"Database is empty, so nothing can be reported and builds fail. Enter the name of your BugSplat database below.",
					MessageType.Error);
				return;
			}

			if (!options.InitializeAutomatically)
			{
				EditorGUILayout.HelpBox(
					"Initialize Automatically is off. BugSplat will not start until your code calls BugSplat.Initialize(options).",
					MessageType.Warning);
				return;
			}

			EditorGUILayout.HelpBox(
				$"BugSplat initializes from this asset before the first scene loads and reports to \"{options.Database}\". Nothing needs to be placed in a scene.",
				MessageType.Info);
		}

		private static void DrawManagerAdvisory()
		{
#pragma warning disable 618 // Obsolete by design; this is where a project learns to remove it.
			var managers = Object.FindObjectsByType<BugSplatManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore 618
			if (managers.Length == 0)
			{
				return;
			}

			var names = string.Join(", ", managers.Select(manager => $"\"{manager.gameObject.name}\""));
			EditorGUILayout.HelpBox(
				$"The open scene has a BugSplatManager on {names}. BugSplat no longer needs one - it initializes itself before the first scene loads - so the component can be removed.",
				MessageType.Warning);
		}
	}
}
