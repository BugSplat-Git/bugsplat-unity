using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using BugSplatUnity.Runtime.Client;
using Debug = UnityEngine.Debug;
using System.Net;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

#if UNITY_EDITOR_WIN
using UnityEditor.WindowsStandalone;
#endif

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Two jobs after every player build: put the BugSplat native runtime where bugsplat_init()
	/// looks for it, and upload symbols.
	///
	/// The runtime is the same set of files on every desktop platform - BugSplatMonitor (captures
	/// the crash out of process), BugSplatReporter with its theme folder (the dialog and the
	/// upload), and on Windows BugSplatWer.dll (fail-fast crashes via Windows Error Reporting).
	/// They live under Runtime/Plugins/&lt;platform&gt;/Support~, which Unity ignores (the tilde) so it
	/// never tries to import an executable as a plugin, and are copied next to the built player
	/// here. Missing them is the number-one field failure: the SDK refuses to start without them
	/// and says so in the log.
	/// </summary>
	public class BuildPostprocessors
	{
		const string SymUploaderWindows = "symbol-upload-windows.exe";
		const string SymUploaderMacOS = "symbol-upload-macos";
		const string SymUploaderLinux = "symbol-upload-linux";

		// The desktop runtime, per platform. Directories are copied recursively.
		static readonly string[] WindowsRuntimeFiles = { "BugSplatMonitor.exe", "BugSplatReporter.exe", "BugSplatWer.dll", "theme" };
		static readonly string[] MacRuntimeFiles = { "BugSplatMonitor", "BugSplatReporter.app", "theme" };
		static readonly string[] LinuxRuntimeFiles = { "BugSplatMonitor", "BugSplatReporter", "theme" };

		internal static string GetSymUploaderName() =>
			Application.platform switch
			{
				RuntimePlatform.WindowsEditor => SymUploaderWindows,
				RuntimePlatform.OSXEditor => SymUploaderMacOS,
				RuntimePlatform.LinuxEditor => SymUploaderLinux,
				_ => throw new InvalidOperationException($"BugSplat. Failed to obtain symbol uploader for {Application.platform}")
			};

		internal static string GetPackageRoot()
		{
			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BuildPostprocessors).Assembly);
			return packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.unity"));
		}

		internal static string GetSymUploaderPath()
		{
			var uploaderName = GetSymUploaderName();
			var packagePath = Path.Combine(GetPackageRoot(), "Editor", uploaderName);

			// Registry and git installs resolve under Library/PackageCache, which Unity owns and may
			// rewrite at any time, so a download there does not survive. Temp/ is the project's own.
			return Directory.Exists(Path.GetDirectoryName(packagePath)) && IsWritable(Path.GetDirectoryName(packagePath))
				? packagePath
				: Path.GetFullPath(Path.Combine("Temp", uploaderName));
		}

		static bool IsWritable(string directory)
		{
			try
			{
				var probe = Path.Combine(directory, ".bugsplat-write-probe");
				File.WriteAllText(probe, string.Empty);
				File.Delete(probe);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

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

			if (target == BuildTarget.StandaloneLinux64)
			{
				PostProcessLinux(pathToBuiltProject, options);
			}

			if (target == BuildTarget.StandaloneOSX)
			{
				PostProcessMac(pathToBuiltProject, options);
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

		// ---- the native runtime ----------------------------------------------------------------

		/// <summary>
		/// Copies the platform's runtime files from Runtime/Plugins/&lt;platform&gt;/Support~/&lt;arch&gt; into
		/// destinationDir. Returns false when any file is missing, which the caller reports once.
		/// </summary>
		internal static bool CopyRuntime(string platformFolder, string arch, string[] files, string destinationDir, string description)
		{
			var supportDir = Path.Combine(GetPackageRoot(), "Runtime", "Plugins", platformFolder, "Support~");
			if (!string.IsNullOrEmpty(arch))
			{
				supportDir = Path.Combine(supportDir, arch);
			}

			if (!Directory.Exists(supportDir))
			{
				Debug.LogError(
					$"BugSplat. The native runtime for {description} is not in this package ({supportDir}). " +
					"Run Tools~/fetch-native-runtime to download the bugsplat-native release for this platform, " +
					"or the built player will refuse to start native crash reporting.");
				return false;
			}

			Directory.CreateDirectory(destinationDir);

			var complete = true;
			foreach (var name in files)
			{
				var source = Path.Combine(supportDir, name);
				var destination = Path.Combine(destinationDir, name);

				if (Directory.Exists(source))
				{
					CopyDirectory(source, destination);
				}
				else if (File.Exists(source))
				{
					File.Copy(source, destination, true);
					MarkExecutable(destination);
				}
				else
				{
					Debug.LogError($"BugSplat. Missing native runtime file {source}. Native crash reports will not be captured or uploaded.");
					complete = false;
				}
			}

			return complete;
		}

		private static void CopyDirectory(string source, string destination)
		{
			Directory.CreateDirectory(destination);
			foreach (var file in Directory.GetFiles(source))
			{
				var target = Path.Combine(destination, Path.GetFileName(file));
				File.Copy(file, target, true);
				MarkExecutable(target);
			}
			foreach (var dir in Directory.GetDirectories(source))
			{
				CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
			}
		}

		/// <summary>
		/// Git and zip round-trips lose the execute bit; without it macOS and Linux cannot spawn the
		/// monitor. Only meaningful on a POSIX editor, where chmod exists.
		/// </summary>
		private static void MarkExecutable(string path)
		{
			if (Application.platform == RuntimePlatform.WindowsEditor)
				return;

			var extension = Path.GetExtension(path);
			if (extension == ".json" || extension == ".plist" || extension == ".png")
				return;

			try
			{
				using (var chmod = Process.Start(new ProcessStartInfo("chmod", $"+x \"{path}\"") { UseShellExecute = false, CreateNoWindow = true }))
				{
					chmod?.WaitForExit();
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"BugSplat. Could not mark {path} executable: {ex.Message}");
			}
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

			if (!options.UseNativeCrashReporting)
				return;

			string arch;
			try
			{
				arch = GetPEMachineArchitecture(pathToBuiltProject);
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat. Could not determine built executable architecture: {ex.Message}. Skipping native runtime copy.");
				return;
			}

			// Next to the executable: the first place the SDK looks, and where BugSplatWer.dll has
			// to be for the WER registry value to name a stable path.
			if (CopyRuntime("Windows", arch, WindowsRuntimeFiles, buildDir, $"Windows {arch}"))
			{
				Debug.Log($"BugSplat. Copied the Windows native runtime ({arch}) next to the built executable. Ship BugSplatMonitor.exe, BugSplatReporter.exe, BugSplatWer.dll and theme/ with your game.");
			}
		}

		private static void PostProcessLinux(string pathToBuiltProject, BugSplatOptions options)
		{
			var buildDir = Path.GetDirectoryName(pathToBuiltProject);
			if (buildDir == null)
			{
				Debug.LogError("BugSplat. Could not find build directory. Skipping Linux post-build tasks.");
				return;
			}

			if (options.UseNativeCrashReporting &&
				CopyRuntime("Linux", "x86_64", LinuxRuntimeFiles, buildDir, "Linux x86_64"))
			{
				Debug.Log("BugSplat. Copied the Linux native runtime next to the built executable. Ship BugSplatMonitor, BugSplatReporter and theme/ with your game.");
			}

			if (!options.UploadDebugSymbolsForLinux)
				return;

			// Unity ships a stripped player; the debug information is in the .debug files next to
			// it when debug symbols are on. dump_syms turns either into .sym.
			UploadSymbols(buildDir, "**/{*.so,*.debug,*.x86_64}", options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload Linux symbols.");
					return;
				}

				Debug.Log("BugSplat. Linux symbols uploading completed.");
			}, dumpSyms: true);
		}

		private static void PostProcessMac(string pathToBuiltProject, BugSplatOptions options)
		{
			var builtAppPath = pathToBuiltProject.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			// An Xcode project export has no .app yet; Xcode assembles the bundle at its own build time.
			var isBundle = builtAppPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase);

			if (options.UseNativeCrashReporting)
			{
				if (isBundle)
				{
					// Contents/Helpers is where the SDK looks inside a bundle, and where the helper
					// executables have to live to be code-signed with the app.
					var helpers = Path.Combine(builtAppPath, "Contents", "Helpers");
					if (CopyRuntime("macOS", null, MacRuntimeFiles, helpers, "macOS"))
					{
						Debug.Log("BugSplat. Copied the macOS native runtime into Contents/Helpers. Sign BugSplatMonitor and BugSplatReporter.app along with the app (Developer ID, hardened runtime) before notarizing.");
					}
				}
				else
				{
					Debug.Log("BugSplat. Xcode project export detected: add Runtime/Plugins/macOS/Support~ (BugSplatMonitor, BugSplatReporter.app, theme) to the app's Contents/Helpers in the Xcode target.");
				}
			}

			if (!options.UploadDebugSymbolsForMac)
				return;

			if (!isBundle)
			{
				Debug.Log("BugSplat: Xcode project export detected, skipping symbol upload. Symbols will be available after building in Xcode.");
				return;
			}

			var buildDir = Path.GetDirectoryName(builtAppPath);
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

			// Breakpad .sym files everywhere Crashpad runs: the dSYM is only dump_syms' input.
			UploadSymbols(buildDir, "**/{*.dSYM,LineNumberMappings.json.zip}", options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload macOS symbols.");
					return;
				}

				Debug.Log("BugSplat. macOS symbols uploading completed.");
			}, dumpSyms: true);
		}

		private static void UploadSymbolFilesWin(string pathToBuiltProject, BugSplatOptions options)
		{
			if (!options.UploadDebugSymbolsForWindows)
				return;

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

		internal static string GetPEMachineArchitecture(string exePath)
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

			// bugsplat-native for iOS is a static library over Crashpad's in-process handler; it
			// needs the system zlib and the C++ runtime UnityFramework already links.
			project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-lz");
			project.AddBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");

			project.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

			var mainTargetGuid = project.GetUnityMainTargetGuid();
			project.AddBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
			project.SetBuildProperty(mainTargetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

			HandleUploadSymbols(mainTargetGuid, project, options);

			File.WriteAllText(projectPath, project.WriteToString());

			CopyLineNumberMappings(pathToBuiltProject);

			if (options.UseNativeCrashReporting)
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

		/// <summary>
		/// Two in-process crash handlers cannot share a process: Unity's own would claim the
		/// signals before bugsplat-native's Crashpad handler saw them.
		/// </summary>
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
				Debug.Log("BugSplat: Disabled Unity's built-in crash reporter so bugsplat-native's handler sees crashes first.");
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
				$"# Breakpad .sym files: the dSYM is dump_syms' input, so pass --dumpSyms.\n" +
				$"\"$SYMBOL_UPLOAD\" \\\n" +
				$"    --database \"{options.Database}\" \\\n" +
				$"    --application \"{application}\" \\\n" +
				$"    --version \"{version}\" \\\n" +
				$"    --files \"**/*.dSYM\" \\\n" +
				$"    --directory \"${{BUILT_PRODUCTS_DIR}}\" \\\n" +
				$"    --dumpSyms\n\n" +
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
			// phases, with the older one still uploading.
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
			}, dumpSyms: true);
		}
#endif

		private static void UploadSymbols(string artifactsDirPath, string globPattern, BugSplatOptions options, Action<int> onCompleted, bool dumpSyms = false)
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

			// Every platform Crashpad runs on is symbolicated from Breakpad .sym files; PDBs stay
			// PDBs for the Windows pipeline.
			if (dumpSyms)
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

			MarkExecutable(destinationPath);
			return true;
		}
	}
}
