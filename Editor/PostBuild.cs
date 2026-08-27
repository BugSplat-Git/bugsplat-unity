using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

namespace BugSplatUnity.Editor
{
	public class BuildPostprocessors
	{
		static string _platform;

		// Resource name the macOS crash dialog looks up; the file has to keep this name in the player.
		const string LogoFileName = "bugsplat-logo.png";

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

		internal static string GetSymUploaderPath()
		{
			var uploaderName = GetSymUploaderName();
			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BuildPostprocessors).Assembly);
			var packageRoot = packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.unity"));
			var packagePath = Path.Combine(packageRoot, "Editor", uploaderName);

			// Registry and git installs resolve under Library/PackageCache, which Unity owns and may
			// re-extract, so anything we have to download has to land outside the package.
			return File.Exists(packagePath)
				? packagePath
				: Path.GetFullPath(Path.Combine("Temp", uploaderName));
		}

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
#endif
			if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
			{
				PostProcessWindows(pathToBuiltProject, options);
				UploadSymbolFilesWin(pathToBuiltProject, options);
			}

			if (target == BuildTarget.StandaloneOSX)
			{
				CopyMacCrashDialogLogo(pathToBuiltProject, options);
				PostProcessMac(pathToBuiltProject, options);
			}
		}

		/// <summary>
		/// Puts the BugSplat logo where the macOS crash dialog can find it.
		///
		/// The dialog loads its banner with [[NSBundle bundleForClass:self] imageForResource:@"bugsplat-logo"].
		/// An app that links BugSplat.framework resolves that inside the framework's own Resources, but Unity
		/// ships the SDK as a bare dylib, which carries no resources of its own — so the lookup lands on the
		/// player's bundle instead, misses, and the dialog silently falls back to a programmatically drawn
		/// logo. Copying the framework's own PNG into the player's Resources is what makes the real one resolve.
		/// </summary>
		private static void CopyMacCrashDialogLogo(string pathToBuiltProject, BugSplatOptions options)
		{
			if (!options.UseNativeCrashReportingForMac)
				return;

			// An Xcode project export has no .app yet — Xcode assembles Contents/Resources at its own build time.
			if (!pathToBuiltProject.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				Debug.Log("BugSplat: Xcode project export detected, skipping the macOS crash dialog logo. Add bugsplat-logo.png to the Xcode target's resources to show it.");
				return;
			}

			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BuildPostprocessors).Assembly);
			var packageRoot = packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.unity"));
			var source = Path.Combine(
				packageRoot, "Editor", "IOS", "Frameworks", "BugSplat.xcframework",
				"macos-arm64_x86_64", "BugSplat.framework", "Versions", "A", "Resources", LogoFileName);

			if (!File.Exists(source))
			{
				Debug.LogWarning($"BugSplat. Missing {source}. The macOS crash dialog will draw its fallback logo.");
				return;
			}

			var resourcesDir = Path.Combine(pathToBuiltProject, "Contents", "Resources");
			if (!Directory.Exists(resourcesDir))
			{
				Debug.LogWarning($"BugSplat. {resourcesDir} does not exist. The macOS crash dialog will draw its fallback logo.");
				return;
			}

			try
			{
				File.Copy(source, Path.Combine(resourcesDir, LogoFileName), true);
				Debug.Log($"BugSplat. Copied {LogoFileName} into the player's Resources so the macOS crash dialog shows the BugSplat logo.");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"BugSplat. Could not copy {LogoFileName} into the player: {ex.Message}. The macOS crash dialog will draw its fallback logo.");
			}
		}

		// Zips a single file with the entry at the archive root and writes it to destZip.
		// symbol-upload skips no-dbgId files (e.g. LineNumberMappings.json) but uploads
		// .zip files as-is via the versions path, so we zip the mapping before upload.
		private static void ZipForUpload(string sourceFile, string destZip)
		{
			if (File.Exists(destZip))
			{
				File.Delete(destZip);
			}

			using (var archive = ZipFile.Open(destZip, ZipArchiveMode.Create))
			{
				archive.CreateEntryFromFile(sourceFile, Path.GetFileName(sourceFile));
			}
		}

		private static void UploadSymbolFilesWin(string pathToBuiltProject, BugSplatOptions options)
		{
#if UNITY_EDITOR_WIN
			if (!UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles)
			{
				Debug.LogWarning("BugSplat. Skipping symbols uploading since \"Copy PDB files\" is disabled in BuildSettings->Windows.");
				return;
			}
#else
			Debug.LogWarning("BugSplat. \"Copy PDB files\" (BuildSettings->Windows) can only be read from a Windows editor, so it was not checked. If it is disabled the build contains no .pdb files and Windows crash reports will not symbolicate.");
#endif

			UploadSymbols(Path.GetDirectoryName(pathToBuiltProject), "**/{*.pdb,*.dll,*.exe,LineNumberMappings.json.zip}", options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload symbols.");
					return;
				}

				Debug.Log("BugSplat. Symbols uploading completed.");
			});
		}

		private static void PostProcessWindows(string pathToBuiltProject, BugSplatOptions options)
		{
			var buildDir = Path.GetDirectoryName(pathToBuiltProject);
			if (buildDir == null)
			{
				Debug.LogError("BugSplat. Could not find build directory. Skipping Windows post-build tasks.");
				return;
			}

			CopyWindowsLineNumberMappings(buildDir);

			if (!options.UseNativeCrashReportingForWindows)
				return;

			string arch;
			try
			{
				arch = GetPEMachineArchitecture(pathToBuiltProject);
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat. Could not determine built executable architecture: {ex.Message}. Skipping native runtime support file copy.");
				return;
			}

			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BuildPostprocessors).Assembly);
			var packageRoot = packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.unity"));
			var supportDir = Path.Combine(packageRoot, "Runtime", "Plugins", "Windows", "Support~", arch);

			foreach (var fileName in new[] { "BugSplatMonitor.exe", "BugSplatRc.dll", "BugSplatWer.dll" })
			{
				var source = Path.Combine(supportDir, fileName);
				if (!File.Exists(source))
				{
					Debug.LogError($"BugSplat. Missing native runtime support file {source}. Native crash reports may not upload.");
					continue;
				}

				File.Copy(source, Path.Combine(buildDir, fileName), true);
			}

			Debug.Log($"BugSplat. Copied Windows native runtime support files ({arch}) next to the built executable.");
		}

		private static void CopyWindowsLineNumberMappings(string buildDir)
		{
			// Copy LineNumberMappings.json for IL2CPP symbolication. Mono builds don't produce one.
			var mappingSearchPaths = new[]
			{
				Path.Combine("Library", "Bee", "artifacts", "WinPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
				Path.Combine("Library", "Bee", "artifacts", "WinPlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
				Path.Combine("Library", "Bee", "artifacts", "WindowsPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
				Path.Combine("Library", "Bee", "artifacts", "WindowsPlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
			};

			foreach (var searchPath in mappingSearchPaths)
			{
				var fullPath = Path.GetFullPath(searchPath);
				if (File.Exists(fullPath))
				{
					var destZip = Path.Combine(buildDir, "LineNumberMappings.json.zip");
					ZipForUpload(fullPath, destZip);
					Debug.Log($"BugSplat: Zipped LineNumberMappings.json for upload ({new FileInfo(fullPath).Length / 1024}KB -> {new FileInfo(destZip).Length / 1024}KB); symbol-upload skips the raw .json (no dbgId), the .zip uploads via the versions path.");
					return;
				}
			}

			Debug.Log("BugSplat: LineNumberMappings.json not found. IL2CPP C# symbolication will not be available for Windows. This is expected for Mono builds.");
		}

		private static string GetPEMachineArchitecture(string exePath)
		{
			// Read the COFF machine field from the PE header: 0x014C = x86, 0x8664 = x64, 0xAA64 = ARM64
			using (var stream = File.OpenRead(exePath))
			using (var reader = new BinaryReader(stream))
			{
				stream.Seek(0x3C, SeekOrigin.Begin);
				var peHeaderOffset = reader.ReadInt32();
				stream.Seek(peHeaderOffset + 4, SeekOrigin.Begin);
				var machine = reader.ReadUInt16();

				switch (machine)
				{
					case 0x014C:
						return "x86";
					case 0x8664:
						return "x64";
					case 0xAA64:
						return "ARM64";
					default:
						throw new InvalidOperationException($"Unsupported PE machine type 0x{machine:X4}");
				}
			}
		}

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
					var destZip = Path.Combine(buildDir, "LineNumberMappings.json.zip");
					ZipForUpload(fullPath, destZip);
					Debug.Log($"BugSplat: Zipped LineNumberMappings.json for upload ({new FileInfo(fullPath).Length / 1024}KB -> {new FileInfo(destZip).Length / 1024}KB); symbol-upload skips the raw .json (no dbgId), the .zip uploads via the versions path.");
					mappingFound = true;
					break;
				}
			}

			if (!mappingFound)
			{
				Debug.LogWarning("BugSplat: LineNumberMappings.json not found. IL2CPP C# symbolication will not be available for macOS. Ensure Scripting Backend is set to IL2CPP.");
			}

			UploadSymbols(buildDir, "**/{*.dSYM,LineNumberMappings.json.zip}", options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload macOS symbols.");
					return;
				}

				Debug.Log("BugSplat. macOS symbols uploading completed.");
			});
		}

		internal static BugSplatOptions GetBugSplatOptions()
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

			if (!BugSplatSymbolUploadCredentials.TryResolve(options.Database, out _, out _))
			{
				Debug.LogWarning(
					$"BugSplat: no symbol upload credentials for database '{options.Database}'. Set " +
					$"{BugSplatSymbolUploadCredentials.ClientIdEnvironmentVariable} and " +
					$"{BugSplatSymbolUploadCredentials.ClientSecretEnvironmentVariable} in the Xcode build environment, or use " +
					"BugSplat > Symbol Upload > Set Credentials. The dSYM upload build phase will skip uploading without them.");
			}

			var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;
			var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;

			// Resolved against $HOME at Xcode build time, so the credentials never enter the project
			// and the generated script carries no path from the machine that ran the Unity build.
			var credentialsRelativePath = BugSplatSymbolUploadCredentials.GetCredentialsPathRelativeToHome(options.Database);

			const string shellPath = "/bin/sh";
			const int index = 999;
			const string name = "Upload dSYM files to BugSplat";
			var shellScript =
				$"BUGSPLAT_CREDENTIALS=\"$HOME/{credentialsRelativePath}\"\n" +
				$"if [ -f \"$BUGSPLAT_CREDENTIALS\" ]; then\n" +
				$"    . \"$BUGSPLAT_CREDENTIALS\"\n" +
				$"fi\n" +
				$"if [ -z \"$SYMBOL_UPLOAD_CLIENT_ID\" ] || [ -z \"$SYMBOL_UPLOAD_CLIENT_SECRET\" ]; then\n" +
				$"    echo \"warning: BugSplat symbol upload credentials not found. Set SYMBOL_UPLOAD_CLIENT_ID and SYMBOL_UPLOAD_CLIENT_SECRET, or run BugSplat > Symbol Upload > Set Credentials in Unity. Skipping dSYM upload.\"\n" +
				$"    exit 0\n" +
				$"fi\n" +
				$"export SYMBOL_UPLOAD_CLIENT_ID SYMBOL_UPLOAD_CLIENT_SECRET\n\n" +
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
				$"    --files \"**/*.dSYM\" \\\n" +
				$"    --directory \"${{BUILT_PRODUCTS_DIR}}\"\n\n" +
				$"# Upload LineNumberMappings.json for IL2CPP C# symbolication.\n" +
				$"# symbol-upload skips the raw .json (no dbgId), so zip it; the .zip uploads via the versions path.\n" +
				$"MAPPINGS=\"${{PROJECT_DIR}}/LineNumberMappings.json\"\n" +
				$"if [ -f \"$MAPPINGS\" ]; then\n" +
				$"    (cd \"${{PROJECT_DIR}}\" && zip -j -q LineNumberMappings.json.zip LineNumberMappings.json)\n" +
				$"    \"$SYMBOL_UPLOAD\" \\\n" +
				$"        --database \"{options.Database}\" \\\n" +
				$"        --application \"{application}\" \\\n" +
				$"        --version \"{version}\" \\\n" +
				$"        --files \"LineNumberMappings.json.zip\" \\\n" +
				$"        --directory \"${{PROJECT_DIR}}\"\n" +
				$"fi";

			if (!string.IsNullOrEmpty(project.GetShellScriptBuildPhaseForTarget(targetGuid, name, shellPath, shellScript)))
				return;

			// GetShellScriptBuildPhaseForTarget matches on name, shellPath *and* script body, so a phase
			// written by an older version does not match this one. Inserting regardless would leave two
			// phases, with the older one still uploading - and, before this change, still carrying
			// credentials inlined into project.pbxproj.
			if (HasBuildPhaseNamed(project, targetGuid, name))
			{
				Debug.LogWarning(
					$"BugSplat: the Xcode project already has a '{name}' build phase from an earlier version, so a new one was not added. " +
					"Delete that phase and build again, or export with Replace instead of Append. " +
					"If it was generated before 5.0.0 it contains your symbol upload Client ID and Secret in plain text - rotate them.");
				return;
			}

			project.InsertShellScriptBuildPhase(index, targetGuid, name, shellPath, shellScript);
		}

		private static bool HasBuildPhaseNamed(PBXProject project, string targetGuid, string name)
		{
			foreach (var phaseGuid in project.GetAllBuildPhasesForTarget(targetGuid))
			{
				if (string.Equals(project.GetBuildPhaseName(phaseGuid), name))
					return true;
			}

			return false;
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
				try
				{
					Directory.Delete(symbolsUnzipPath, true);
				}
				catch (Exception e)
				{
					Debug.LogWarning($"BugSplat. Could not clean up unzipped symbols at {symbolsUnzipPath}: {e.Message}");
				}

				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload symbols.");
					return;
				}

				Debug.Log("BugSplat. Symbols uploading completed.");
			});
		}
#endif

		private static void UploadSymbols(string artifactsDirPath, string globPattern, BugSplatOptions options, Action<int> onCompleted)
		{
			if (!BugSplatSymbolUploadCredentials.TryResolve(options.Database, out var clientId, out var clientSecret))
			{
				Debug.LogWarning(
					$"BugSplat: no symbol upload credentials for database '{options.Database}'. Set " +
					$"{BugSplatSymbolUploadCredentials.ClientIdEnvironmentVariable} and " +
					$"{BugSplatSymbolUploadCredentials.ClientSecretEnvironmentVariable}, or use " +
					"BugSplat > Symbol Upload > Set Credentials. Skipping symbol uploads.");
				onCompleted(0);
				return;
			}

			var symbolUploadPath = GetSymUploaderPath();
			if (!File.Exists(symbolUploadPath) && !DownloadSymbolUpload(symbolUploadPath))
			{
				onCompleted(-1);
				return;
			}

			var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
			var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;

			var symUploadProcessInfo = new ProcessStartInfo
			{
				FileName = symbolUploadPath,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				Arguments = $"--database {options.Database} --application \"{application}\" " +
					$"--version \"{version}\" --files \"{globPattern}\" --directory \"{artifactsDirPath}\""
			};

			symUploadProcessInfo.EnvironmentVariables["SYMBOL_UPLOAD_CLIENT_ID"] = clientId;
			symUploadProcessInfo.EnvironmentVariables["SYMBOL_UPLOAD_CLIENT_SECRET"] = clientSecret;

			if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
			{
				symUploadProcessInfo.Arguments += " --dumpSyms";
			}

			Process uploadSymProcess;
			try
			{
				uploadSymProcess = Process.Start(symUploadProcessInfo);
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat. Failed to start {symbolUploadPath}. Error: {ex}");
				onCompleted(-1);
				return;
			}

			if (uploadSymProcess == null)
			{
				onCompleted(-1);
				return;
			}

			Debug.Log(uploadSymProcess.StandardOutput.ReadToEnd());

			uploadSymProcess.WaitForExit();

			onCompleted(uploadSymProcess.ExitCode);
		}

		private static bool DownloadSymbolUpload(string destinationPath)
		{
			var variant = Path.GetFileName(destinationPath);
			var fileUrl = $"https://app.bugsplat.com/download/{variant}";

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

				using (var client = new WebClient())
				{
					Debug.Log($"BugSplat. Downloading {variant} to {destinationPath}");

					client.DownloadFile(fileUrl, destinationPath);

					if (File.Exists(destinationPath))
					{
						Debug.Log($"BugSplat. {variant} downloaded successfully to {destinationPath}");
					}
					else
					{
						Debug.LogError($"BugSplat. Could not download {variant}");
						return false;
					}
				}
			}
			catch (WebException ex)
			{
				Debug.LogError($"BugSplat. Failed to download file from {fileUrl}. Error: {ex.Message}");
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat. Unexpected error during file download. Error: {ex}");
				return false;
			}

			if (Application.platform == RuntimePlatform.WindowsEditor)
			{
				return true;
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

				if (process.ExitCode != 0)
				{
					Debug.LogError($"BugSplat. Failed to make {destinationPath} executable. Error: {error}");
					return false;
				}

				Debug.Log($"BugSplat. Successfully made {destinationPath} executable. Output: {output}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat. Error setting executable permission for {destinationPath}. Error: {ex}");
				return false;
			}

			return true;
		}
	}
}
