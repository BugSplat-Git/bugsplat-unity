using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
		// The desktop runtime, per platform. Directories are copied recursively.
		static readonly string[] WindowsRuntimeFiles = { "BugSplatMonitor.exe", "BugSplatReporter.exe", "theme" };
		static readonly string[] MacRuntimeFiles = { "BugSplatMonitor", "BugSplatReporter.app", "theme" };
		static readonly string[] LinuxRuntimeFiles = { "BugSplatMonitor", "BugSplatReporter", "theme" };

		internal static string GetSymUploaderName() =>
			Application.platform switch
			{
				RuntimePlatform.WindowsEditor => SymbolUploadRelease.Windows,
				RuntimePlatform.OSXEditor => RuntimeInformation.OSArchitecture == Architecture.X64
					? SymbolUploadRelease.MacOSIntel
					: SymbolUploadRelease.MacOS,
				RuntimePlatform.LinuxEditor => SymbolUploadRelease.Linux,
				_ => throw new InvalidOperationException($"BugSplat. Failed to obtain symbol uploader for {Application.platform}")
			};

		internal static string GetPackageRoot()
		{
			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BuildPostprocessors).Assembly);
			return packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.sdk"));
		}

		internal static string GetSymUploaderPath()
		{
			var uploaderName = GetSymUploaderName();
			var packagePath = Path.Combine(GetPackageRoot(), "Editor", uploaderName);

			// Registry and git installs resolve under Library/PackageCache, which Unity owns and may
			// re-extract, so anything we have to download has to land outside the package. A copy in the
			// package is only used if it is the pinned release: 4.x downloaded uploaders there, so
			// local and embedded installs can still hold an older, unverified one.
			return SymbolUploadRelease.IsVerified(packagePath, uploaderName) ? packagePath : GetSymUploaderDownloadPath();
		}

		static string GetSymUploaderDownloadPath() =>
			Path.GetFullPath(Path.Combine("Temp", $"symbol-upload-{SymbolUploadRelease.Version}", GetSymUploaderName()));

		[PostProcessBuild(1)]
		public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
		{
			var options = GetBugSplatOptions();

			if (options == null)
			{
				Debug.LogWarning("BugSplat: no BugSplatOptions asset is selected, skipping build post-process tasks. " + BugSplatOptions.ConfigureHint);
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

			// Monitor, reporter and theme go next to the executable, the first place the SDK looks.
			// BugSplatWer.dll goes next to BugSplat.dll: the SDK registers <library dir>/BugSplatWer.dll
			// with Windows Error Reporting, and Unity places the library under <Game>_Data/Plugins.
			var copied = CopyRuntime("Windows", arch, WindowsRuntimeFiles, buildDir, $"Windows {arch}");
			var pluginDir = FindWindowsPluginDirectory(pathToBuiltProject);
			if (pluginDir == null)
			{
				Debug.LogError("BugSplat. Could not find BugSplat.dll under the player's Plugins folder, so BugSplatWer.dll was not placed. Fail-fast crashes will not be reported.");
				copied = false;
			}
			else
			{
				copied &= CopyRuntime("Windows", arch, new[] { "BugSplatWer.dll" }, pluginDir, $"Windows {arch}");
			}

			// 8.x shipped the dialog as BugSplatRc.dll; 9.x replaced it with BugSplatReporter.exe.
			var staleRc = Path.Combine(buildDir, "BugSplatRc.dll");
			if (File.Exists(staleRc))
				File.Delete(staleRc);

			if (copied)
			{
				Debug.Log($"BugSplat. Copied the Windows native runtime ({arch}): BugSplatMonitor.exe, BugSplatReporter.exe and theme/ next to the executable, BugSplatWer.dll next to BugSplat.dll in {pluginDir}. Ship all of them with your game.");
			}
		}

		/// <summary>
		/// The folder Unity placed BugSplat.dll in: &lt;Game&gt;_Data/Plugins/x86_64 (or x86 / ARM64), searched rather
		/// than assumed because the architecture folder name is Unity's to choose.
		/// </summary>
		internal static string FindWindowsPluginDirectory(string pathToBuiltProject)
		{
			var buildDir = Path.GetDirectoryName(pathToBuiltProject);
			var dataDir = Path.Combine(buildDir ?? string.Empty, Path.GetFileNameWithoutExtension(pathToBuiltProject) + "_Data");
			var pluginsDir = Path.Combine(dataDir, "Plugins");
			if (!Directory.Exists(pluginsDir))
				return null;

			foreach (var candidate in Directory.GetFiles(pluginsDir, "BugSplat.dll", SearchOption.AllDirectories))
			{
				return Path.GetDirectoryName(candidate);
			}

			return null;
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

		private static readonly string[] WindowsBeeLineNumberMappings =
		{
			Path.Combine("Library", "Bee", "artifacts", "WinPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "WinPlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "WindowsPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "WindowsPlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
		};

		private static readonly string[] LinuxBeeLineNumberMappings =
		{
			Path.Combine("Library", "Bee", "artifacts", "LinuxPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "LinuxPlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
		};

		private static readonly string[] MacBeeLineNumberMappings =
		{
			Path.Combine("Library", "Bee", "artifacts", "MacStandalonePlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "MacStandalonePlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "MacPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "artifacts", "MacPlayerBuildProgram", "il2cppOutput", "LineNumberMappings.json"),
		};

		// IL2CPP writes LineNumberMappings.json only when its stack traces include file and line
		// numbers, which is not Unity's default.
		internal static void LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget target, string platform)
		{
			if (PlayerSettings.GetScriptingBackend(target) != ScriptingImplementation.IL2CPP)
			{
				Debug.Log($"BugSplat: no LineNumberMappings.json, as expected for a Mono build. C# line numbers in native {platform} crash reports need IL2CPP.");
				return;
			}

			if (PlayerSettings.GetIl2CppStacktraceInformation(target) == Il2CppStacktraceInformation.MethodOnly)
			{
				Debug.LogWarning(
					$"BugSplat: IL2CPP did not write LineNumberMappings.json, so native {platform} crash reports will show generated C++ " +
					"instead of your C# files and line numbers. Set Edit > Project Settings > Player > Other Settings > " +
					"IL2CPP Stacktrace Information to \"Method Name, File Name, and Line Number\".");
				return;
			}

			Debug.LogWarning($"BugSplat: LineNumberMappings.json not found, so native {platform} crash reports will not map back to C# files and line numbers.");
		}

		// The current player's IL2CPP backup folder first, so another player's backup in the same
		// output folder is never picked; Library/Bee for Unity versions that don't back it up.
		private static string FindLineNumberMappings(string pathToBuiltProject, string[] beeSearchPaths)
		{
			var backedUp = Path.Combine(
				Path.GetDirectoryName(pathToBuiltProject),
				Path.GetFileNameWithoutExtension(pathToBuiltProject) + "_BackUpThisFolder_ButDontShipItWithYourGame",
				"il2cppOutput", "Symbols", "LineNumberMappings.json");
			if (File.Exists(backedUp))
				return backedUp;

			foreach (var searchPath in beeSearchPaths)
			{
				var fullPath = Path.GetFullPath(searchPath);
				if (File.Exists(fullPath))
					return fullPath;
			}

			return null;
		}

		// The backend keeps one mappings file per name for a database, application and version, so each
		// platform uploads under its own name and a macOS and an iOS build sharing a version don't
		// overwrite each other's. Windows keeps the original name, which the Xbox add-on uploads too.
		internal const string WindowsLineNumberMappingsName = "LineNumberMappings.json";
		internal const string MacLineNumberMappingsName = "LineNumberMappings-macos.json";
		internal const string IosLineNumberMappingsName = "LineNumberMappings-ios.json";
		internal const string AndroidLineNumberMappingsName = "LineNumberMappings-android.json";
		internal const string LinuxLineNumberMappingsName = "LineNumberMappings-linux.json";

		private static void UploadLineNumberMappings(string mappingsPath, string uploadName, BugSplatOptions options)
		{
			if (Path.GetFileName(mappingsPath) == uploadName)
			{
				UploadLineNumberMappingsFrom(Path.GetDirectoryName(mappingsPath), uploadName, options);
				return;
			}

			// Upload a renamed copy; the file Unity wrote stays where it is, under its own name.
			var stagingDir = Path.GetFullPath(Path.Combine("Temp", "BugSplat", Guid.NewGuid().ToString("N")));
			try
			{
				Directory.CreateDirectory(stagingDir);
				File.Copy(mappingsPath, Path.Combine(stagingDir, uploadName));
				Debug.Log($"BugSplat: uploading {mappingsPath} as {uploadName}");
				UploadLineNumberMappingsFrom(stagingDir, uploadName, options);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				Debug.LogError($"BugSplat. Could not stage {mappingsPath} for upload as {uploadName}: {e.Message}");
			}
			finally
			{
				try
				{
					if (Directory.Exists(stagingDir))
						Directory.Delete(stagingDir, true);
				}
				catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
				{
					Debug.LogWarning($"BugSplat. Could not clean up {stagingDir}: {e.Message}");
				}
			}
		}

		private static void UploadLineNumberMappingsFrom(string directory, string fileName, BugSplatOptions options)
		{
			// Never --dumpSyms: that's for native binaries, and symbol-upload drops a file it can't dump.
			UploadSymbols(directory, fileName, options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
					Debug.LogError($"BugSplat. Could not upload {fileName}.");
			});
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

			UploadSymbols(buildDir, "**/*.dSYM", options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload macOS symbols.");
					return;
				}

				Debug.Log("BugSplat. macOS symbols uploading completed.");
			}, dumpSyms: true);

			var mappingsPath = FindLineNumberMappings(pathToBuiltProject, MacBeeLineNumberMappings);
			if (mappingsPath == null)
			{
				LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget.Standalone, "macOS");
				return;
			}

			UploadLineNumberMappings(mappingsPath, MacLineNumberMappingsName, options);
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

			UploadSymbols(Path.GetDirectoryName(pathToBuiltProject), "**/{*.pdb,*.dll,*.exe}", options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload symbols.");
					return;
				}

				Debug.Log("BugSplat. Symbols uploading completed.");
			});

			var mappingsPath = FindLineNumberMappings(pathToBuiltProject, WindowsBeeLineNumberMappings);
			if (mappingsPath == null)
			{
				LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget.Standalone, "Windows");
				return;
			}

			UploadLineNumberMappings(mappingsPath, WindowsLineNumberMappingsName, options);
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

		internal static BugSplatOptions GetBugSplatOptions() => BugSplatProjectOptions.Get();

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

			project = HandleUploadSymbols(mainTargetGuid, project, options);

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

			// An export into an existing Xcode project keeps files from the last one: drop the copy older
			// versions wrote under the shared name, and last build's mappings in case this build has none.
			foreach (var stale in new[] { WindowsLineNumberMappingsName, IosLineNumberMappingsName })
			{
				var stalePath = Path.Combine(pathToBuiltProject, stale);
				if (File.Exists(stalePath))
					File.Delete(stalePath);
			}

			foreach (var searchPath in searchPaths)
			{
				var fullPath = Path.GetFullPath(searchPath);
				if (File.Exists(fullPath))
				{
					File.Copy(fullPath, Path.Combine(pathToBuiltProject, IosLineNumberMappingsName), true);
					Debug.Log($"BugSplat: Copied LineNumberMappings.json to the Xcode project as {IosLineNumberMappingsName} ({new FileInfo(fullPath).Length / 1024}KB)");
					return;
				}
			}

			LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget.iOS, "iOS");
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

		private static PBXProject HandleUploadSymbols(string targetGuid, PBXProject project, BugSplatOptions options)
		{
			if (!options.UploadDebugSymbolsForIos)
				return project;

			if (!BugSplatSymbolUploadCredentials.TryResolve(options.Database, out _, out _))
			{
				Debug.LogWarning(
					$"BugSplat: no symbol upload credentials for database '{options.Database}'. Set " +
					$"{BugSplatSymbolUploadCredentials.ClientIdEnvironmentVariable} and " +
					$"{BugSplatSymbolUploadCredentials.ClientSecretEnvironmentVariable} in the Xcode build environment, or use " +
					"Tools > BugSplat > Symbol Upload > Set Credentials. The dSYM upload build phase will skip uploading without them.");
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
				$"    ENV_CLIENT_ID=\"$SYMBOL_UPLOAD_CLIENT_ID\"\n" +
				$"    ENV_CLIENT_SECRET=\"$SYMBOL_UPLOAD_CLIENT_SECRET\"\n" +
				$"    . \"$BUGSPLAT_CREDENTIALS\"\n" +
				$"    if [ -n \"$ENV_CLIENT_ID\" ]; then SYMBOL_UPLOAD_CLIENT_ID=\"$ENV_CLIENT_ID\"; fi\n" +
				$"    if [ -n \"$ENV_CLIENT_SECRET\" ]; then SYMBOL_UPLOAD_CLIENT_SECRET=\"$ENV_CLIENT_SECRET\"; fi\n" +
				$"fi\n" +
				$"if [ -z \"$SYMBOL_UPLOAD_CLIENT_ID\" ] || [ -z \"$SYMBOL_UPLOAD_CLIENT_SECRET\" ]; then\n" +
				$"    echo \"warning: BugSplat symbol upload credentials not found. Set SYMBOL_UPLOAD_CLIENT_ID and SYMBOL_UPLOAD_CLIENT_SECRET, or run Tools > BugSplat > Symbol Upload > Set Credentials in Unity. Skipping dSYM upload.\"\n" +
				$"    exit 0\n" +
				$"fi\n" +
				$"export SYMBOL_UPLOAD_CLIENT_ID SYMBOL_UPLOAD_CLIENT_SECRET\n\n" +
				$"if [ \"$(uname -m)\" = \"x86_64\" ]; then\n" +
				$"    VARIANT=\"{SymbolUploadRelease.MacOSIntel}\"\n" +
				$"    EXPECTED_SHA256=\"{SymbolUploadRelease.GetSha256(SymbolUploadRelease.MacOSIntel)}\"\n" +
				$"else\n" +
				$"    VARIANT=\"{SymbolUploadRelease.MacOS}\"\n" +
				$"    EXPECTED_SHA256=\"{SymbolUploadRelease.GetSha256(SymbolUploadRelease.MacOS)}\"\n" +
				$"fi\n" +
				$"SYMBOL_UPLOAD=\"${{TMPDIR}}/{SymbolUploadRelease.Version}-$VARIANT\"\n" +
				$"sha256_of() {{ shasum -a 256 \"$1\" | cut -d ' ' -f 1; }}\n" +
				$"if [ ! -f \"$SYMBOL_UPLOAD\" ] || [ \"$(sha256_of \"$SYMBOL_UPLOAD\")\" != \"$EXPECTED_SHA256\" ]; then\n" +
				$"    echo \"Downloading $VARIANT {SymbolUploadRelease.Version}...\"\n" +
				$"    DOWNLOAD=\"$(mktemp \"$SYMBOL_UPLOAD.XXXXXX\")\" || {{ echo \"warning: Could not create a temporary file for $VARIANT. Skipping dSYM upload.\"; exit 0; }}\n" +
				$"    if ! curl -sfL -o \"$DOWNLOAD\" \"{SymbolUploadRelease.GetDownloadUrl("$VARIANT")}\"; then\n" +
				$"        rm -f \"$DOWNLOAD\"\n" +
				$"        echo \"warning: Could not download $VARIANT. Skipping dSYM upload.\"\n" +
				$"        exit 0\n" +
				$"    fi\n" +
				$"    if [ \"$(sha256_of \"$DOWNLOAD\")\" != \"$EXPECTED_SHA256\" ]; then\n" +
				$"        rm -f \"$DOWNLOAD\"\n" +
				$"        echo \"error: $VARIANT failed its integrity check and was deleted without running. Expected SHA-256 $EXPECTED_SHA256.\"\n" +
				$"        exit 1\n" +
				$"    fi\n" +
				$"    if ! chmod +x \"$DOWNLOAD\" || ! mv -f \"$DOWNLOAD\" \"$SYMBOL_UPLOAD\"; then\n" +
				$"        rm -f \"$DOWNLOAD\"\n" +
				$"        echo \"warning: Could not install $VARIANT at $SYMBOL_UPLOAD. Skipping dSYM upload.\"\n" +
				$"        exit 0\n" +
				$"    fi\n" +
				$"fi\n\n" +
				$"# Breakpad .sym files: the dSYM is dump_syms' input, so pass --dumpSyms.\n" +
				$"\"$SYMBOL_UPLOAD\" \\\n" +
				$"    --database {ShellQuote(options.Database)} \\\n" +
				$"    --application {ShellQuote(application)} \\\n" +
				$"    --version {ShellQuote(version)} \\\n" +
				$"    --files \"**/*.dSYM\" \\\n" +
				$"    --directory \"${{BUILT_PRODUCTS_DIR}}\" \\\n" +
				$"    --dumpSyms\n\n" +
				$"# Upload IL2CPP's LineNumberMappings.json, copied here as {IosLineNumberMappingsName}, for C# symbolication.\n" +
				$"MAPPINGS=\"${{PROJECT_DIR}}/{IosLineNumberMappingsName}\"\n" +
				$"if [ -f \"$MAPPINGS\" ]; then\n" +
				$"    \"$SYMBOL_UPLOAD\" \\\n" +
				$"        --database {ShellQuote(options.Database)} \\\n" +
				$"        --application {ShellQuote(application)} \\\n" +
				$"        --version {ShellQuote(version)} \\\n" +
				$"        --files \"{IosLineNumberMappingsName}\" \\\n" +
				$"        --directory \"${{PROJECT_DIR}}\"\n" +
				$"fi";

			// GetShellScriptBuildPhaseForTarget doesn't find a phase this method wrote in an earlier export -
			// once the project has been saved and read back, its script no longer compares equal - and
			// PBXProject has no way to remove a phase. Inserting regardless would leave two phases, the older
			// one still uploading under its old names and, before 5.0.0, carrying credentials inlined into
			// project.pbxproj. So the comparison and the removal happen in the serialized project instead:
			// an identical phase is kept as it is, and any other phase of this name is replaced.
			var pbxproj = RemoveShellScriptBuildPhasesNamed(project.WriteToString(), name, out var removedPhases);
			var updated = new PBXProject();
			updated.ReadFromString(pbxproj);
			updated.InsertShellScriptBuildPhase(index, targetGuid, name, shellPath, shellScript);

			if (removedPhases.Length == 0)
				return updated;

			RemoveShellScriptBuildPhasesNamed(updated.WriteToString(), name, out var insertedPhase);
			if (WithoutObjectIds(insertedPhase) == WithoutObjectIds(removedPhases))
				return project;

			if (removedPhases.Contains("--clientSecret"))
			{
				Debug.LogWarning(
					$"BugSplat: replaced the Xcode project's '{name}' build phase. The old one was generated before 5.0.0 and " +
					"contained your symbol upload Client ID and Secret in plain text - rotate them if this project was ever shared or committed.");
			}
			else
			{
				Debug.Log($"BugSplat: replaced the Xcode project's '{name}' build phase from an earlier export.");
			}

			return updated;
		}

		private static string ShellQuote(string value) => "'" + (value ?? string.Empty).Replace("'", "'\\''") + "'";

#endif

		// Drops the object ID that starts each object declaration, so two phases compare by content alone.
		// Hex anywhere else, such as a version in the script, still counts.
		internal static string WithoutObjectIds(string pbxprojObjects) => Regex.Replace(pbxprojObjects, @"(?m)^(\s*)[0-9A-Fa-f]{24}\b", "$1");

		// Removes every PBXShellScriptBuildPhase called `name` from project.pbxproj text, and its entries
		// in targets' buildPhases lists. removedPhases returns the removed objects' text. Works on the
		// multi-line layout PBXProject.WriteToString and Xcode both write for build phases.
		internal static string RemoveShellScriptBuildPhasesNamed(string pbxproj, string name, out string removedPhases)
		{
			var lines = pbxproj.Split('\n');
			var objectStart = new Regex(@"^\s*([0-9A-Fa-f]{24})\b[^=]*=\s*\{\s*$");
			var objectEnd = new Regex(@"^\s*\};\s*$");
			var nameLine = new Regex(
				@"^\s*name\s*=\s*(""" + Regex.Escape(name) + @"""|" +
				Regex.Escape(name) + @");\s*$");

			var removedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var removed = new StringBuilder();
			var kept = new List<string>(lines.Length);

			for (var i = 0; i < lines.Length; i++)
			{
				var start = objectStart.Match(lines[i]);
				if (start.Success)
				{
					var end = i + 1;
					while (end < lines.Length && !objectEnd.IsMatch(lines[end]) && !objectStart.IsMatch(lines[end]))
						end++;

					if (end < lines.Length && objectEnd.IsMatch(lines[end]))
					{
						var isShellScriptPhase = false;
						var hasName = false;
						for (var j = i + 1; j < end; j++)
						{
							isShellScriptPhase |= lines[j].Contains("isa = PBXShellScriptBuildPhase;");
							hasName |= nameLine.IsMatch(lines[j]);
						}

						if (isShellScriptPhase && hasName)
						{
							removedGuids.Add(start.Groups[1].Value);
							for (var j = i; j <= end; j++)
								removed.Append(lines[j]).Append('\n');
							i = end;
							continue;
						}
					}
				}

				kept.Add(lines[i]);
			}

			removedPhases = removed.ToString();
			if (removedGuids.Count == 0)
				return pbxproj;

			var reference = new Regex(@"^\s*([0-9A-Fa-f]{24})\b[^,=]*,\s*$");
			kept.RemoveAll(line =>
			{
				var match = reference.Match(line);
				return match.Success && removedGuids.Contains(match.Groups[1].Value);
			});

			return string.Join("\n", kept);
		}

#if UNITY_ANDROID
		private static readonly string[] AndroidBeeLineNumberMappings =
		{
			Path.Combine("Library", "Bee", "artifacts", "Android", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json"),
			Path.Combine("Library", "Bee", "Android", "Prj", "IL2CPP", "Il2CppBackup", "il2cppOutput", "Symbols", "LineNumberMappings.json"),
		};

		private static void UploadSymbolsAndroid(string pathToBuiltProject, BugSplatOptions options)
		{
			if (!options.UploadDebugSymbolsForAndroid)
			{
				return;
			}

			if (EditorUserBuildSettings.exportAsGoogleAndroidProject)
			{
				var exportedMappings = Path.Combine(pathToBuiltProject, "unityLibrary", "src", "main", "Il2CppOutputProject",
					"Source", "il2cppOutput", "Symbols", "LineNumberMappings.json");
				Debug.LogWarning("BugSplat. Skipping symbols and LineNumberMappings.json uploading since \"Export Project\" is enabled in BuildSettings->Android." +
					(File.Exists(exportedMappings)
						? $" For C# line numbers, upload {exportedMappings} yourself, renamed to {AndroidLineNumberMappingsName}."
						: ""));
				return;
			}

			var debugSymbolLevel = UnityEditor.Android.UserBuildSettings.DebugSymbols.level;
			if (debugSymbolLevel == Unity.Android.Types.DebugSymbolLevel.None)
			{
				Debug.LogWarning("BugSplat. Skipping symbols uploading since \"Debug Symbols\" is set to None in BuildSettings->Android.");
				return;
			}

			var isIl2Cpp = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP;
			if (isIl2Cpp && debugSymbolLevel == Unity.Android.Types.DebugSymbolLevel.SymbolTable)
			{
				Debug.LogWarning(
					"BugSplat: Android \"Debug Symbols\" is set to Public, so libil2cpp.so's symbols carry no file and line information " +
					"and native Android crash reports can't map IL2CPP frames back to C# files and line numbers. " +
					"Set Debug Symbols to Full in the Android build settings.");
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

			var mappingsPath = FindLineNumberMappings(pathToBuiltProject, AndroidBeeLineNumberMappings);
			if (mappingsPath == null)
			{
				LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget.Android, "Android");
				return;
			}

			UploadLineNumberMappings(mappingsPath, AndroidLineNumberMappingsName, options);
		}

		private static void ProcessSymbolsArchive(string filePath, BugSplatOptions options)
		{
			string symbolsUnzipPath = Path.Combine(Path.GetDirectoryName(filePath), "symbols");

			try
			{
				System.IO.Compression.ZipFile.ExtractToDirectory(filePath, symbolsUnzipPath, true);

				// Without "legacy extensions", Unity 6 zips libfoo.so.dbg (Full) or libfoo.so.sym (Public). Most have no
				// SONAME, so dump_syms names the module after the file; rename them to libfoo.so to match crashes.
				foreach (var suffix in new[] { ".dbg", ".sym" })
				{
					foreach (var file in Directory.GetFiles(symbolsUnzipPath, "*.so" + suffix, SearchOption.AllDirectories))
					{
						var renamed = file.Substring(0, file.Length - suffix.Length);
						if (!File.Exists(renamed))
							File.Move(file, renamed);
					}
				}
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

		internal static void UploadSymbols(string artifactsDirPath, string globPattern, BugSplatOptions options, Action<int> onCompleted, bool dumpSyms = false)
		{
			if (!BugSplatSymbolUploadCredentials.TryResolve(options.Database, out var clientId, out var clientSecret))
			{
				Debug.LogWarning(
					$"BugSplat: no symbol upload credentials for database '{options.Database}'. Set " +
					$"{BugSplatSymbolUploadCredentials.ClientIdEnvironmentVariable} and " +
					$"{BugSplatSymbolUploadCredentials.ClientSecretEnvironmentVariable}, or use " +
					"Tools > BugSplat > Symbol Upload > Set Credentials. Skipping symbol uploads.");
				onCompleted(0);
				return;
			}

			var symbolUploadPath = GetSymUploaderPath();
			if (!SymbolUploadRelease.IsVerified(symbolUploadPath, GetSymUploaderName())
				&& !DownloadSymbolUpload(symbolUploadPath))
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
			var fileUrl = SymbolUploadRelease.GetDownloadUrl(variant);
			var partialPath = destinationPath + ".download";

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

				using (var client = new WebClient())
				{
					Debug.Log($"BugSplat. Downloading {variant} {SymbolUploadRelease.Version} to {destinationPath}");

					client.DownloadFile(fileUrl, partialPath);
				}

				var actualSha256 = SymbolUploadRelease.ComputeSha256(partialPath);
				var expectedSha256 = SymbolUploadRelease.GetSha256(variant);
				if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
				{
					File.Delete(partialPath);
					Debug.LogError(
						$"BugSplat. {variant} from {fileUrl} failed its integrity check and was deleted without running. " +
						$"Expected SHA-256 {expectedSha256}, got {actualSha256}. Skipping symbol upload.");
					return false;
				}

				if (File.Exists(destinationPath))
					File.Delete(destinationPath);
				File.Move(partialPath, destinationPath);

				Debug.Log($"BugSplat. {variant} downloaded and verified at {destinationPath}");
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
