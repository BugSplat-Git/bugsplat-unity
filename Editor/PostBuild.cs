using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Net;


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
		if (target == BuildTarget.StandaloneOSX)
			PostProcessMac(pathToBuiltProject, options);
	}

#if UNITY_EDITOR_WIN
	private static void UploadSymbolFilesWin(string pathToBuiltProject, BugSplatOptions options)
	{
		if (!UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles)
		{
			Debug.LogWarning("BugSplat. Skipping symbols uploading since \"Copy PDB files\" is disabled in BuildSettings->Windows.");
			return;
		}

		UploadSymbols(Path.GetDirectoryName(pathToBuiltProject), "**/{*.pdb,*.dll,*.exe,LineNumberMappings.json}", options, uploadExitCode =>
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

	private static void PostProcessMac(string pathToBuiltProject, BugSplatOptions options)
	{
		if (!options.UploadDebugSymbolsForMac)
			return;

		// Skip symbol upload for Xcode project exports — dSYMs don't exist yet
		if (Directory.GetFiles(pathToBuiltProject, "*.xcodeproj", SearchOption.TopDirectoryOnly).Length > 0
			|| Directory.GetDirectories(pathToBuiltProject, "*.xcodeproj", SearchOption.TopDirectoryOnly).Length > 0)
		{
			Debug.Log("BugSplat: Xcode project export detected, skipping symbol upload. Symbols will be available after building in Xcode.");
			return;
		}

		var buildDir = Path.GetDirectoryName(pathToBuiltProject);
		if (buildDir == null)
		{
			Debug.LogError("BugSplat. Could not find build directory. Will not upload macOS debug symbols.");
			return;
		}

		// Copy LineNumberMappings.json for IL2CPP symbolication
		var mappingSearchPaths = new[]
		{
			Path.Combine("Library", "Bee", "artifacts", "MacStandalonePlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "MacStandalonePlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "MacPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "MacPlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
		};

		var mappingFound = false;
		foreach (var searchPath in mappingSearchPaths)
		{
			var fullPath = Path.GetFullPath(searchPath);
			if (File.Exists(fullPath))
			{
				var dest = Path.Combine(buildDir, "LineNumberMappings.json");
				File.Copy(fullPath, dest, true);
				Debug.Log($"BugSplat: Copied LineNumberMappings.json to build directory ({new FileInfo(fullPath).Length / 1024}KB)");
				mappingFound = true;
				break;
			}
		}

		if (!mappingFound)
		{
			Debug.LogWarning("BugSplat: LineNumberMappings.json not found. IL2CPP C# symbolication will not be available for macOS. Ensure Scripting Backend is set to IL2CPP.");
		}

		UploadSymbols(buildDir, "**/{*.dSYM,LineNumberMappings.json}", options, uploadExitCode =>
		{
			if (uploadExitCode != 0)
			{
				Debug.LogError("BugSplat. Could not upload macOS symbols.");
				return;
			}

			Debug.Log("BugSplat. macOS symbols uploading completed.");
		});
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

#if UNITY_IOS
	private static void PostProcessIos(string pathToBuiltProject, BugSplatOptions options)
	{
		var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

		var project = new PBXProject();
		project.ReadFromString(File.ReadAllText(projectPath));

		var targetGuid = project.GetUnityFrameworkTargetGuid();

		project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
		project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-lz");
		project.AddBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");

		project.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

		var mainTargetGuid = project.GetUnityMainTargetGuid();
		project.AddBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
		project.SetBuildProperty(mainTargetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

		HandleUploadSymbols(mainTargetGuid, project, options);

		File.WriteAllText(projectPath, project.WriteToString());

		CopyLineNumberMappings(pathToBuiltProject);

		if (options.UseNativeCrashReportingForIos)
			DisableUnityCrashReporter(pathToBuiltProject);
	}

	private static void CopyLineNumberMappings(string pathToBuiltProject)
	{
		var searchPaths = new[]
		{
			Path.Combine("Library", "Bee", "artifacts", "iOS", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "iOSPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
		};

		foreach (var searchPath in searchPaths)
		{
			var fullPath = Path.GetFullPath(searchPath);
			if (File.Exists(fullPath))
			{
				var dest = Path.Combine(pathToBuiltProject, "LineNumberMappings.json");
				File.Copy(fullPath, dest, true);
				Debug.Log($"BugSplat: Copied LineNumberMappings.json to Xcode project ({new FileInfo(fullPath).Length / 1024}KB)");
				return;
			}
		}

		Debug.LogWarning("BugSplat: LineNumberMappings.json not found. IL2CPP C# symbolication will not be available. Ensure Scripting Backend is set to IL2CPP.");
	}

	private static void DisableUnityCrashReporter(string pathToBuiltProject)
	{
		var crashReporterPath = Path.Combine(pathToBuiltProject, "Classes", "CrashReporter.h");
		if (!File.Exists(crashReporterPath))
		{
			Debug.Log("BugSplat: CrashReporter.h not found, Unity crash reporter may not be present in this version.");
			return;
		}

		var content = File.ReadAllText(crashReporterPath);
		var modified = content
			.Replace("#define ENABLE_CUSTOM_CRASH_REPORTER 1", "#define ENABLE_CUSTOM_CRASH_REPORTER 0")
			.Replace("#define ENABLE_CRASH_REPORT_SUBMISSION 1", "#define ENABLE_CRASH_REPORT_SUBMISSION 0");

		if (content != modified)
		{
			File.WriteAllText(crashReporterPath, modified);
			Debug.Log("BugSplat: Disabled Unity's built-in crash reporter to prevent PLCrashReporter conflict.");
		}
	}

	private static void HandleUploadSymbols(string targetGuid, PBXProject project, BugSplatOptions options)
	{
		if (!options.UploadDebugSymbolsForIos)
			return;

		string clientId = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
		if (string.IsNullOrEmpty(clientId))
			clientId = options.SymbolUploadClientId;

		string clientSecret = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");
		if (string.IsNullOrEmpty(clientSecret))
			clientSecret = options.SymbolUploadClientSecret;

		if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
		{
			Debug.LogWarning("BugSplat: SymbolUploadClientId/Secret not set. Skipping iOS dSYM upload build phase.");
			return;
		}

		var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;
		var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;

		const string shellPath = "/bin/sh";
		const int index = 999;
		const string name = "Upload dSYM files to BugSplat";
		var shellScript =
			$"if [ \"$(uname -m)\" = \"x86_64\" ]; then\n" +
			$"    VARIANT=\"symbol-upload-macos-intel\"\n" +
			$"else\n" +
			$"    VARIANT=\"symbol-upload-macos\"\n" +
			$"fi\n" +
			$"SYMBOL_UPLOAD=\"${{TMPDIR}}/$VARIANT\"\n" +
			$"if [ ! -f \"$SYMBOL_UPLOAD\" ]; then\n" +
			$"    echo \"Downloading $VARIANT...\"\n" +
			$"    curl -sL -o \"$SYMBOL_UPLOAD\" \"https://app.bugsplat.com/download/$VARIANT\"\n" +
			$"    chmod +x \"$SYMBOL_UPLOAD\"\n" +
			$"fi\n\n" +
			$"\"$SYMBOL_UPLOAD\" \\\n" +
			$"    --database \"{options.Database}\" \\\n" +
			$"    --application \"{application}\" \\\n" +
			$"    --version \"{version}\" \\\n" +
			$"    --clientId \"{clientId}\" \\\n" +
			$"    --clientSecret \"{clientSecret}\" \\\n" +
			$"    --files \"**/*.dSYM\" \\\n" +
			$"    --directory \"${{BUILT_PRODUCTS_DIR}}\"\n\n" +
			$"# Upload LineNumberMappings.json for IL2CPP C# symbolication\n" +
			$"MAPPINGS=\"${{PROJECT_DIR}}/LineNumberMappings.json\"\n" +
			$"if [ -f \"$MAPPINGS\" ]; then\n" +
			$"    \"$SYMBOL_UPLOAD\" \\\n" +
			$"        --database \"{options.Database}\" \\\n" +
			$"        --application \"{application}\" \\\n" +
			$"        --version \"{version}\" \\\n" +
			$"        --clientId \"{clientId}\" \\\n" +
			$"        --clientSecret \"{clientSecret}\" \\\n" +
			$"        --files \"LineNumberMappings.json\" \\\n" +
			$"        --directory \"${{PROJECT_DIR}}\"\n" +
			$"fi";

		if (string.IsNullOrEmpty(project.GetShellScriptBuildPhaseForTarget(targetGuid, name, shellPath, shellScript)))
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
		string clientId = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
		if (string.IsNullOrEmpty(clientId))
		{
			clientId = options.SymbolUploadClientId;
		}
		
		if (string.IsNullOrEmpty(clientId))
		{
			Debug.LogWarning("BugSplat: SymbolUploadClientId is not set in BugSplatOptions or in the environment variable BUGSPLAT_CLIENT_ID. Skipping symbol uploads.");
			onCompleted(0);
			return;
		}

		string clientSecret = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");
		if (string.IsNullOrEmpty(clientSecret))
		{
			clientSecret = options.SymbolUploadClientSecret;
		}

		if (string.IsNullOrEmpty(clientSecret))
		{
			Debug.LogWarning("BugSplat. SymbolUploadClientSecret is not set in BugSplatOptions or in the environment variable BUGSPLAT_CLIENT_SECRET. Skipping symbol uploads");
			onCompleted(0);
			return;
		}

		var symbolUploadPath = Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.unity", "Editor", GetSymUploaderName()));
		if (!File.Exists(symbolUploadPath))
		{
			DownloadSymbolUpload(symbolUploadPath);
		}

		var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
		var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;

		var symUploadProcessInfo = new ProcessStartInfo
		{
			FileName = symbolUploadPath,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			Arguments = $"--database {options.Database} --application \"{application}\" --clientId {clientId} --clientSecret {clientSecret} " +
				$"--version \"{version}\" --files \"{globPattern}\" --directory \"{artifactsDirPath}\""
		};

		if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
		{
			symUploadProcessInfo.Arguments += " --dumpSyms";
		}

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

	private static void DownloadSymbolUpload(string destinationPath)
	{
		var varient = Path.GetFileName(destinationPath);
		var fileUrl = $"https://app.bugsplat.com/download/{varient}";

		try
		{
			using (var client = new WebClient())
			{
				Debug.Log($"BugSplat. Downloading {varient} to {destinationPath}");

				client.DownloadFile(fileUrl, destinationPath);

				if (File.Exists(destinationPath))
				{
					Debug.Log($"BugSplat. {varient} downloaded successfully to {destinationPath}");
				}
				else
				{
					Debug.LogError($"BugSplat. Could not download {varient}");
				}
			}
		}
		catch (WebException ex)
		{
			Debug.LogError($"BugSplat. Failed to download file from {fileUrl}. Error: {ex.Message}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"BugSplat. Unexpected error during file download. Error: {ex.Message}");
		}

		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			return;
		}

		try
		{
			var absolutePath = Path.GetFullPath(destinationPath);

			// Run chmod +x to make the file executable
			var process = new Process();
			process.StartInfo.FileName = "chmod";
			process.StartInfo.Arguments = $"+x \"{absolutePath}\"";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.Start();

			var output = process.StandardOutput.ReadToEnd();
			var error = process.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode == 0)
			{
				Debug.Log($"PostBuild: Successfully made {destinationPath} executable. Output: {output}");
			}
			else
			{
				Debug.LogError($"PostBuild: Failed to make {destinationPath} executable. Error: {error}");
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"PostBuild: Error setting executable permission for {destinationPath}. Error: {ex.Message}");
		}
	}
}
