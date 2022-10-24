using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using BugSplatUnity.Runtime.Client;
using UnityEditor.iOS.Xcode;

public class BuildPostprocessors
{
	static string _platform;

	/// <summary>
	/// Upload Asset/Plugin symbol files to BugSplat. 
	/// We don't upload Unity symbol files because the build output only contains public symbol information.
	/// BugSplat is configured to use the Unity symbol server which has private symbols containing file, function, and line information.
	/// </summary>
	[PostProcessBuild(1)]
	public static async Task OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	{
		var options = GetBugSplatOptions();

		if (options == null)
		{
			Debug.LogWarning(
				"No BugSplatOptions ScriptableObject found! Skipping build post-process tasks...");
			return;
		}

		if (target == BuildTarget.iOS)
			PostProcessIos(pathToBuiltProject, options);

		await UploadSymbolFiles(target, options);
	}

	private static async Task UploadSymbolFiles(BuildTarget target, BugSplatOptions options)
	{
		switch (target)
		{
			case BuildTarget.StandaloneWindows64:
				_platform = "x86_64";
				break;
			case BuildTarget.StandaloneWindows:
				_platform = "x86";
				break;
			default: return;
		}

		var projectDir = Path.GetDirectoryName(Application.dataPath);
		if (projectDir == null)
		{
			Debug.LogWarning($"Could not find data path directory {Application.dataPath}, skipping symbol uploads...");
			return;
		}

		var pluginsDir = Path.Combine(Path.Combine(projectDir, "Assets", "Plugins"), _platform);

		if (!Directory.Exists(pluginsDir))
		{
			Debug.LogWarning("Plugins directory doesn't exist, skipping symbol uploads...");
			return;
		}

		var database = options.Database;
		var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;
		var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
		var clientId = options.SymbolUploadClientId;
		var clientSecret = options.SymbolUploadClientSecret;

		if (string.IsNullOrEmpty(database))
		{
			Debug.LogWarning("BugSplatOptions Database was not set! Skipping symbol uploads...");
			return;
		}

		if (string.IsNullOrEmpty(clientId))
		{
			Debug.LogWarning("BugSplatOptions ClientID was not set! Skipping symbol uploads...");
			return;
		}

		if (string.IsNullOrEmpty(clientSecret))
		{
			Debug.LogWarning("BugSplatOptions ClientSecret was not set! Skipping symbol uploads...");
			return;
		}

		Debug.Log($"BugSplat Database: {database}");
		Debug.Log($"BugSplat Application: ${application}");
		Debug.Log($"BugSplat Version: ${version}");

		var fileExtensions = new List<string>()
		{
			".dll",
			".pdb"
		};
		var symbolFiles = Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories)
			.Select(file => new FileInfo(file))
			.Where(fileInfo => fileExtensions.Any(ext => ext.Equals(fileInfo.Extension)))
			.ToList();

		foreach (var symbolFile in symbolFiles)
		{
			Debug.Log($"BugSplat found symbol file: {symbolFile.FullName}");
		}

		Debug.Log("About to upload symbol files to BugSplat...");

		try
		{
			using (var symbolUploader = SymbolUploader.CreateOAuth2SymbolUploader(clientId, clientSecret))
			{
				var responseMessages = await symbolUploader.UploadSymbolFiles(
					database,
					application,
					version,
					symbolFiles
				);

				if (responseMessages[0].IsSuccessStatusCode)
					Debug.Log("BugSplat symbol upload completed successfully!");
				else
					Debug.LogError("BugSplat symbol upload failed. " + responseMessages[0]);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError(ex);
		}
	}

	private static BugSplatOptions GetBugSplatOptions()
	{
		var guids = AssetDatabase.FindAssets("t:BugSplatOptions");

		if (guids.Length == 0)
		{
			return null;
		}

		var path = AssetDatabase.GUIDToAssetPath(guids[0]);
		return AssetDatabase.LoadAssetAtPath<BugSplatOptions>(path);
	}

	private static void PostProcessIos(string pathToBuiltProject, BugSplatOptions options)
	{
		Debug.Log("PostProcessIos");

		var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

		var project = new PBXProject();
		project.ReadFromString(File.ReadAllText(projectPath));

#if UNITY_2019_3_OR_NEWER
		var targetGuid = project.GetUnityFrameworkTargetGuid();
#else
		var targetName = PBXProject.GetUnityTargetName();
		var targetGuid = project.TargetGuidByName(targetName);
#endif

		project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");

		ModifyPlist(pathToBuiltProject, options);
		AddBundle(pathToBuiltProject, project, targetGuid);

		File.WriteAllText(projectPath, project.WriteToString());
	}

	private static void ModifyPlist(string projectPath, BugSplatOptions options)
	{
		Debug.Log("ModifyPlist");
		var plistInfoFile = new PlistDocument();

		var infoPlistPath = Path.Combine(projectPath, "Info.plist");
		plistInfoFile.ReadFromString(File.ReadAllText(infoPlistPath));

		const string bugSplatServerURLKey = "BugsplatServerURL";
		plistInfoFile.root.AsDict().SetString(bugSplatServerURLKey, $"https://{options.Database}.bugsplat.com/");

		File.WriteAllText(infoPlistPath, plistInfoFile.WriteToString());
	}

	private static void AddBundle(string pathToBuiltProject, PBXProject project, string targetGuid)
	{
		Debug.Log("AddBundle");
		const string frameworksFolderPath = "Frameworks";
		const string bundleName = "HockeySDKResources.bundle";
		var files = Directory.GetDirectories(Path.Combine(pathToBuiltProject,
			frameworksFolderPath), bundleName, SearchOption.AllDirectories);

		if (!files.Any())
		{
			Debug.LogWarning("Could not find the .bundle file.");
			return;
		}

		var linkedResourcePathAbsolute = files.First();
		var substringIndex = linkedResourcePathAbsolute.IndexOf(frameworksFolderPath, StringComparison.Ordinal);
		var relativePath = linkedResourcePathAbsolute.Substring(substringIndex);

		var addFolderReference = project.AddFolderReference(relativePath, bundleName);
		project.AddFileToBuild(targetGuid, addFolderReference);
	}
}