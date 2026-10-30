using System;
using System.IO;
using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Configures BugSplat without the Project Settings page: from an editor script, from the
	/// command line, or from an agent. Does exactly what the page's Create button and asset picker
	/// do, so the two paths cannot drift. See Documentation~/automation.md.
	/// </summary>
	public static class BugSplatSetup
	{
		public const string DefaultAssetPath = "Assets/BugSplat/BugSplatOptions.asset";

		const string DatabaseArgument = "-bugsplatDatabase";
		const string ApplicationArgument = "-bugsplatApplication";
		const string VersionArgument = "-bugsplatVersion";
		const string AssetPathArgument = "-bugsplatAssetPath";

		/// <summary>
		/// Makes the project report to <paramref name="database"/>. Updates the selected options
		/// asset in place when there is one (or a project's single asset), otherwise creates one at
		/// <paramref name="assetPath"/> - <see cref="DefaultAssetPath"/> by default - and selects it.
		/// Safe to run again: a second call with a new database changes the same asset.
		/// </summary>
		/// <param name="database">Required unless the selected asset already has one.</param>
		/// <param name="application">Optional; null leaves the asset's value alone.</param>
		/// <param name="version">Optional; null leaves the asset's value alone.</param>
		/// <param name="assetPath">Where to create the asset if none is selected. Ignored otherwise.</param>
		public static BugSplatOptions Configure(string database, string application = null, string version = null, string assetPath = null)
		{
			var options = BugSplatProjectOptions.Get();
			if (options == null)
			{
				// Checked before anything is created, so a failed call leaves no half-made asset behind.
				if (string.IsNullOrEmpty(database))
				{
					throw new ArgumentException(
						$"BugSplat: a database is required to create an options asset. Pass one with {DatabaseArgument}.");
				}

				options = CreateAsset(assetPath);
			}

			if (!string.IsNullOrEmpty(database))
			{
				options.Database = database;
			}

			if (application != null)
			{
				options.Application = application;
			}

			if (version != null)
			{
				options.Version = version;
			}

			if (string.IsNullOrEmpty(options.Database))
			{
				throw new ArgumentException(
					$"BugSplat: a database is required. Pass one, or set Database on {AssetDatabase.GetAssetPath(options)}.");
			}

			EditorUtility.SetDirty(options);
			AssetDatabase.SaveAssets();
			return options;
		}

		/// <summary>
		/// Creates a new options asset at <paramref name="assetPath"/> (<see cref="DefaultAssetPath"/>
		/// by default, made unique if taken) and selects it as the project's asset.
		/// </summary>
		public static BugSplatOptions CreateAsset(string assetPath = null)
		{
			assetPath = string.IsNullOrEmpty(assetPath) ? DefaultAssetPath : assetPath;

			var directory = Path.GetDirectoryName(assetPath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var uniquePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
			var options = ScriptableObject.CreateInstance<BugSplatOptions>();
			AssetDatabase.CreateAsset(options, uniquePath);
			AssetDatabase.SaveAssets();

			BugSplatProjectOptions.Set(options);
			return options;
		}

		/// <summary>
		/// Command-line entry point for <see cref="Configure"/>:
		/// <c>Unity -batchmode -quit -projectPath . -executeMethod BugSplatUnity.Editor.BugSplatSetup.ConfigureFromCommandLine -bugsplatDatabase my-database [-bugsplatApplication name] [-bugsplatVersion 1.0] [-bugsplatAssetPath Assets/BugSplat/BugSplatOptions.asset]</c>.
		/// In batch mode the process exits 0 on success and 1 on failure.
		/// </summary>
		public static void ConfigureFromCommandLine()
		{
			try
			{
				var args = Environment.GetCommandLineArgs();
				string Argument(string name)
				{
					var index = Array.IndexOf(args, name);
					return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
				}

				var options = Configure(
					Argument(DatabaseArgument),
					Argument(ApplicationArgument),
					Argument(VersionArgument),
					Argument(AssetPathArgument));

				Debug.Log(
					$"BugSplat: configured {AssetDatabase.GetAssetPath(options)} for database \"{options.Database}\" " +
					"and selected it as the project's options asset.");

				if (Application.isBatchMode)
				{
					EditorApplication.Exit(0);
				}
			}
			catch (Exception exception)
			{
				Debug.LogError($"BugSplat: setup failed. {exception.Message}");

				if (Application.isBatchMode)
				{
					EditorApplication.Exit(1);
				}
				else
				{
					throw;
				}
			}
		}
	}
}
