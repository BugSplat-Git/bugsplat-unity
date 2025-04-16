using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using BugSplatUnity.Runtime.Client;
using Debug = UnityEngine.Debug;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

#if UNITY_EDITOR_WIN
using UnityEditor.WindowsStandalone;
#endif

public class BuildPostprocessors
{
	static string _platform;

	const string SymUploaderWindows = "symbol-upload-windows.exe";
	const string SymUploaderMacOS = "symbol-upload-macos";
	const string SymUploaderLinux = "symbol-upload-linux";

	internal static string GetSymUploaderName() =>
		Application.platform switch
		{
			RuntimePlatform.WindowsEditor => SymUploaderWindows,
			RuntimePlatform.OSXEditor => SymUploaderMacOS,
			RuntimePlatform.LinuxEditor => SymUploaderLinux,
			_ => throw new InvalidOperationException($"BugSplat. Failed to obtain symbol uploader for {Application.platform}")
		};

	/// <summary>
	/// Upload Asset/Plugin symbol files to BugSplat. 
	/// We don't upload Unity symbol files because the build output only contains public symbol information.
	/// BugSplat is configured to use the Unity symbol server which has private symbols containing file, function, and line information.
	/// </summary>
	[PostProcessBuild(1)]
	public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	{
		var options = GetBugSplatOptions();

		if (options == null)
		{
			Debug.LogWarning("No BugSplatOptions ScriptableObject found! Skipping build post-process tasks...");
			return;
		}

#if UNITY_IOS
		if (target == BuildTarget.iOS)
			PostProcessIos(pathToBuiltProject, options);
#elif UNITY_ANDROID
		if (target == BuildTarget.Android)
			UploadSymbolsAndroid(pathToBuiltProject, options);
#elif UNITY_EDITOR_WIN
		if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
			UploadSymbolFilesWin(pathToBuiltProject, options);
#endif
	}

#if UNITY_EDITOR_WIN
	private static void UploadSymbolFilesWin(string pathToBuiltProject, BugSplatOptions options)
	{
		if (!UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles)
		{
			Debug.LogWarning("BugSplat. Skipping symbols uploading since \"Copy PDB files\" is disabled in BuildSettings->Windows.");
			return;
		}

		UploadSymbols(Path.GetDirectoryName(pathToBuiltProject), "**/*.{pdb,dll,exe}", options, uploadExitCode =>
		{
			if (uploadExitCode != 0)
			{
				Debug.LogError("BugSplat. Could not upload symbols.");
				return;
			}

			Debug.Log("BugSplat. Symbols uploading completed.");
		});
	}
#endif

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

#if UNITY_IOS
	private static void PostProcessIos(string pathToBuiltProject, BugSplatOptions options)
	{
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
		project.AddBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");

		project.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

		var mainTargetGuid = project.GetUnityMainTargetGuid();
		project.AddBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
		project.SetBuildProperty(mainTargetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

		ModifyPlist(pathToBuiltProject, options);
		AddBundle(pathToBuiltProject, project, targetGuid);
		HandleUploadSymbols(mainTargetGuid, project, options.UploadDebugSymbolsForIos);

		File.WriteAllText(projectPath, project.WriteToString());
	}

	private static void ModifyPlist(string projectPath, BugSplatOptions options)
	{
		var plistInfoFile = new PlistDocument();

		var infoPlistPath = Path.Combine(projectPath, "Info.plist");
		plistInfoFile.ReadFromString(File.ReadAllText(infoPlistPath));

		const string bugSplatServerURLKey = "BugsplatServerURL";
		plistInfoFile.root.AsDict().SetString(bugSplatServerURLKey, $"https://{options.Database}.bugsplat.com/");

		File.WriteAllText(infoPlistPath, plistInfoFile.WriteToString());
	}

	private static void AddBundle(string pathToBuiltProject, PBXProject project, string targetGuid)
	{
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

	private static void HandleUploadSymbols(string targetGuid, PBXProject project, bool upload)
	{
		const string shellPath = "/bin/sh";
		const int index = 999;
		const string name = "Upload dSYM files to BugSplat";
		const string shellScript =
			"if [ ! -f \"${HOME}/.bugsplat.conf\" ]\nthen\n    echo \"Missing bugsplat config file: ~/.bugsplat.conf\"\n    exit\nfi\n\nsource \"${HOME}/.bugsplat.conf\"\n\nif [ -z \"${BUGSPLAT_USER}\" ]\nthen\n    echo \"BUGSPLAT_USER must be set in ~/.bugsplat.conf\"\n    exit\nfi\n\nif [ -z \"${BUGSPLAT_PASS}\" ]\nthen\n    echo \"BUGSPLAT_PASS must be set in ~/.bugsplat.conf\"\n    exit\nfi\n\necho \"Product dir: ${BUILT_PRODUCTS_DIR}\"\n\nWORK_DIR=\"$PWD\"\nAPP=$(find $BUILT_PRODUCTS_DIR -name *.app -type d -maxdepth 1 -print | head -n1)\n\necho \"App: ${APP}\"\n\nFILE=\"${WORK_DIR}/Archive.zip\"\n\ncd $BUILT_PRODUCTS_DIR\nzip -r \"${FILE}\" ./*\ncd -\n\n# Change Info.plist path\nAPP_MARKETING_VERSION=$(/usr/libexec/PlistBuddy -c \"Print CFBundleShortVersionString\" \"${APP}/Info.plist\")\nAPP_BUNDLE_VERSION=$(/usr/libexec/PlistBuddy -c \"Print CFBundleVersion\" \"${APP}/Info.plist\")\n\nif [ -z \"${APP_MARKETING_VERSION}\" ]\nthen\n\\techo \"CFBundleShortVersionString not found in app Info.plist\"\n    exit\nfi\n\necho \"App marketing version: ${APP_MARKETING_VERSION}\"\necho \"App bundle version: ${APP_BUNDLE_VERSION}\"\n\nAPP_VERSION=\"${APP_MARKETING_VERSION}\"\n\nif [ -n \"${APP_BUNDLE_VERSION}\" ]\nthen\n    APP_VERSION=\"${APP_VERSION} (${APP_BUNDLE_VERSION})\"\nfi\n\n# Changed CFBundleName to CFBundleExecutable and Info.plist path\nPRODUCT_NAME=$(/usr/libexec/PlistBuddy -c \"Print CFBundleExecutable\" \"${APP}/Info.plist\")\n\nBUGSPLAT_SERVER_URL=$(/usr/libexec/PlistBuddy -c \"Print BugsplatServerURL\" \"${APP}/Info.plist\")\nBUGSPLAT_SERVER_URL=${BUGSPLAT_SERVER_URL%/}\n\nUPLOAD_URL=\"${BUGSPLAT_SERVER_URL}/post/plCrashReporter/symbol/\"\n\necho \"App version: ${APP_VERSION}\"\n\nUUID_CMD_OUT=$(xcrun dwarfdump --uuid \"${APP}/${PRODUCT_NAME}\")\nUUID_CMD_OUT=$([[ \"${UUID_CMD_OUT}\" =~ ^(UUID: )([0-9a-zA-Z\\-]+) ]] && echo ${BASH_REMATCH[2]})\necho \"UUID found: ${UUID_CMD_OUT}\"\n\necho \"Signing into bugsplat and storing session cookie for use in upload\"\n\nCOOKIEPATH=\"/tmp/bugsplat-cookie.txt\"\nLOGIN_URL=\"${BUGSPLAT_SERVER_URL}/browse/login.php\"\necho \"Login URL: ${LOGIN_URL}\"\nrm \"${COOKIEPATH}\"\ncurl -b \"${COOKIEPATH}\" -c \"${COOKIEPATH}\" --data-urlencode \"currusername=${BUGSPLAT_USER}\" --data-urlencode \"currpasswd=${BUGSPLAT_PASS}\" \"${LOGIN_URL}\"\n\necho \"Uploading ${FILE} to ${UPLOAD_URL}\"\n\ncurl -i -b \"${COOKIEPATH}\" -c \"${COOKIEPATH}\" -F filedata=@\"${FILE}\" -F appName=\"${PRODUCT_NAME}\" -F appVer=\"${APP_VERSION}\" -F buildId=\"${UUID_CMD_OUT}\" $UPLOAD_URL";

		if (string.IsNullOrEmpty(project.GetShellScriptBuildPhaseForTarget(targetGuid, name, shellPath, shellScript)) && upload)
			project.InsertShellScriptBuildPhase(index, targetGuid, name, shellPath, shellScript);
	}
#endif

#if UNITY_ANDROID
	private static void UploadSymbolsAndroid(string pathToBuiltProject, BugSplatOptions options)
	{
		if (!options.UploadDebugSymbolsForAndroid)
		{
			return;
		}

		if (EditorUserBuildSettings.exportAsGoogleAndroidProject)
		{
			Debug.LogWarning("BugSplat. Skipping symbols uploading since \"Export Project\" is enabled in BuildSettings->Android.");
			return;
		}

		if (UnityEditor.Android.UserBuildSettings.DebugSymbols.level == Unity.Android.Types.DebugSymbolLevel.None)
		{
			Debug.LogWarning("BugSplat. Skipping symbols uploading since \"Debug Symbols\" is set to None in BuildSettings->Android.");
			return;
		}

		Debug.Log("BugSplat. Starting symbol upload.");

		var buildDir = Path.GetDirectoryName(pathToBuiltProject);
		if (buildDir == null)
		{
			Debug.LogError("BugSplat. Could not find build directory. Will not upload Android debug symbols.");
			return;
		}

		var pattern = "*.symbols.zip";

		var hasFoundFile = false;
		foreach (var file in Directory.GetFiles(buildDir, pattern))
		{
			hasFoundFile = true;
			ProcessSymbolsArchive(file, options);
		}

		if (!hasFoundFile)
		{
			Debug.LogError("BugSplat. Could not find generated symbols archive.");
		}
	}

	private static void ProcessSymbolsArchive(string filePath, BugSplatOptions options)
	{
		string symbolsUnzipPath = Path.Combine(Path.GetDirectoryName(filePath), "symbols");

		try
		{
			System.IO.Compression.ZipFile.ExtractToDirectory(filePath, symbolsUnzipPath, true);
		}
		catch (Exception e)
		{
			Debug.LogError(e);
			return;
		}

		if(!Directory.Exists(symbolsUnzipPath))
		{
			Debug.LogError("BugSplat. Could not unzip generated symbols archive.");
			return;
		}

		UploadSymbols(symbolsUnzipPath, "**/*.so", options, uploadExitCode =>
		{
			if (uploadExitCode != 0)
			{
				Debug.LogError("BugSplat. Could not upload symbols.");
			}

			// Clean up generated debug symbols
			Directory.Delete(symbolsUnzipPath, true);

			Debug.Log("BugSplat. Symbols uploading completed.");
		});
	}
#endif

	private static void UploadSymbols(string artifactsDirPath, string globPattern, BugSplatOptions options, Action<int> onCompleted)
	{
		if (string.IsNullOrEmpty(options.SymbolUploadClientId))
		{
			Debug.LogWarning("BugSplat. SymbolUploadClientId is not set in BugSplatOptions. Skipping symbol uploads...");
			onCompleted(0);
			return;
		}

		if (string.IsNullOrEmpty(options.SymbolUploadClientSecret))
		{
			Debug.LogWarning("BugSplat. SymbolUploadClientSecret is not set in BugSplatOptions. Skipping symbol uploads");
			onCompleted(0);
			return;
		}

		var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
		var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;

		var symUploadProcessInfo = new ProcessStartInfo
		{
			FileName = Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.unity", "Editor", GetSymUploaderName())),
			UseShellExecute = false,
			RedirectStandardOutput = true,
			Arguments = $"--database {options.Database} --application \"{application}\" --clientId {options.SymbolUploadClientId} --clientSecret {options.SymbolUploadClientSecret} " +
				$"--version \"{version}\" --files \"{globPattern}\" --directory \"{artifactsDirPath}\""
		};
		
		if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
			symUploadProcessInfo.Arguments += " --dumpSyms";
		};
		
		var uploadSymProcess = Process.Start(symUploadProcessInfo);
		if (uploadSymProcess == null)
		{
			onCompleted(-1);
			return;
		}

		Debug.Log(uploadSymProcess.StandardOutput.ReadToEnd());

		uploadSymProcess.WaitForExit();

		onCompleted(uploadSymProcess.ExitCode);
	}
}