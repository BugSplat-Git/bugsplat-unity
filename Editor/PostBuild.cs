using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditorInternal;
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

		// On-disk filename to copy. The dialog resolves it by resource name - "bugsplat-logo",
		// without the extension - so the base name is what has to survive into the player.
		const string LogoFileName = "bugsplat-logo.png";

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

		internal static string GetSymUploaderPath()
		{
			var uploaderName = GetSymUploaderName();
			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BuildPostprocessors).Assembly);
			var packageRoot = packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.sdk"));
			var packagePath = Path.Combine(packageRoot, "Editor", uploaderName);

			// Registry and git installs resolve under Library/PackageCache, which Unity owns and may
			// re-extract, so anything we have to download has to land outside the package. A copy in the
			// package is only used if it is the pinned release: 4.x downloaded uploaders there, so
			// local and embedded installs can still hold an older, unverified one.
			return SymbolUploadRelease.IsVerified(packagePath, uploaderName) ? packagePath : GetSymUploaderDownloadPath();
		}

		static string GetSymUploaderDownloadPath() =>
			Path.GetFullPath(Path.Combine("Temp", $"symbol-upload-{SymbolUploadRelease.Version}", GetSymUploaderName()));

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
				Debug.LogWarning("BugSplat: no BugSplatOptions asset is selected, skipping build post-process tasks. " + BugSplatOptions.ConfigureHint);
				return;
			}

			FailedSymbolUploads.Clear();

#if UNITY_IOS
			if (target == BuildTarget.iOS)
				PostProcessIos(pathToBuiltProject, options);
#elif UNITY_ANDROID
			if (target == BuildTarget.Android)
				UploadSymbolsAndroid(pathToBuiltProject, options);
#endif
			if (IsWindowsTarget(target))
			{
				PostProcessWindows(pathToBuiltProject, options);
				UploadSymbolFilesWin(pathToBuiltProject, options);
			}

			if (target == BuildTarget.StandaloneOSX)
			{
				if (options.UseNativeCrashReportingForMac &&
					PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone) != ScriptingImplementation.IL2CPP)
				{
					Debug.LogWarning(BugSplatOptions.MacNativeRequiresIl2CppMessage);
				}

				CopyMacCrashDialogLogo(pathToBuiltProject, options);
				PostProcessMac(pathToBuiltProject, options);
			}

			var failure = DescribeSymbolUploadFailures(FailedSymbolUploads, options.FailBuildOnSymbolUploadError);
			if (failure == null)
				return;

			if (options.FailBuildOnSymbolUploadError)
				throw new BuildFailedException(failure);

			Debug.LogError(failure);
		}

		static readonly List<string> FailedSymbolUploads = new List<string>();

		internal static string DescribeSymbolUploadFailures(IReadOnlyCollection<string> failures, bool failBuild)
		{
			if (failures == null || failures.Count == 0)
				return null;

			var summary = $"BugSplat: {failures.Count} symbol upload{(failures.Count == 1 ? "" : "s")} failed ({string.Join("; ", failures)}), " +
				"so crash reports from this build may not symbolicate. The errors above say why.";

			return failBuild
				? summary + " Failing the build because Fail Build On Symbol Upload Error is on."
				: summary + " The player was still built; turn on Fail Build On Symbol Upload Error in Edit > Project Settings > BugSplat to fail the build instead.";
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

			// Trailing separators are trimmed before the suffix test. Unity has never supplied one,
			// but if it did the check would misfire on a real .app build - skipping the copy and
			// logging an Xcode export as the reason, which is a poor way to discover the bug.
			var builtAppPath = pathToBuiltProject.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			// An Xcode project export has no .app yet — Xcode assembles Contents/Resources at its own build time.
			if (!builtAppPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				Debug.Log($"BugSplat: Xcode project export detected, skipping the macOS crash dialog logo. Add {LogoFileName} to the Xcode target's resources to show it.");
				return;
			}

			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BuildPostprocessors).Assembly);
			var packageRoot = packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.sdk"));
			var source = Path.Combine(
				packageRoot, "Editor", "IOS", "Frameworks", "BugSplat.xcframework",
				"macos-arm64_x86_64", "BugSplat.framework", "Versions", "A", "Resources", LogoFileName);

			if (!File.Exists(source))
			{
				Debug.LogWarning($"BugSplat. Missing {source}. The macOS crash dialog will draw its fallback logo.");
				return;
			}

			var resourcesDir = Path.Combine(builtAppPath, "Contents", "Resources");
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

		private static void UploadSymbolFilesWin(string pathToBuiltProject, BugSplatOptions options)
		{
			if (!options.UploadDebugSymbolsForWindows)
				return;

#if UNITY_EDITOR_WIN
			if (!UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles)
			{
				Debug.LogWarning(CopyPdbFilesOffMessage);
				return;
			}
#else
			Debug.LogWarning("BugSplat. \"Copy PDB files\" (Build Profiles > Windows) can only be read from a Windows editor, so it was not checked. If it is disabled the build contains no .pdb files and Windows crash reports will not symbolicate.");
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

			var mappingsPath = FindWindowsLineNumberMappings(pathToBuiltProject, options.WindowsLineNumberMappingsPath);
			if (mappingsPath == null)
			{
				LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget.Standalone, "Windows");
				return;
			}

			UploadLineNumberMappings(mappingsPath, WindowsLineNumberMappingsName, options);
		}

		internal const string CopyPdbFilesOffMessage =
			"BugSplat: Upload Debug Symbols For Windows is on, but \"Copy PDB files\" is off in File > Build Profiles > Windows, " +
			"so the build has no .pdb files and BugSplat skips the Windows symbol upload. Native crash reports from this build " +
			"will not symbolicate. Turn on Copy PDB files, or turn off Upload Debug Symbols For Windows in Edit > Project Settings > BugSplat.";

		internal static bool IsWindowsTarget(BuildTarget target) =>
			target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows;

		internal static bool ShouldWarnCopyPdbFilesOff(BuildTarget target, BugSplatOptions options, bool copyPdbFiles) =>
			IsWindowsTarget(target) && options != null && options.UploadDebugSymbolsForWindows && !copyPdbFiles;

		private static string FindWindowsLineNumberMappings(string pathToBuiltProject, string overridePath)
		{
			var projectRoot = Directory.GetCurrentDirectory();
			if (!string.IsNullOrWhiteSpace(overridePath))
			{
				var resolved = LineNumberMappingsSearch.ResolveOverride(overridePath, projectRoot);
				if (resolved != null)
					return resolved;

				Debug.LogWarning($"BugSplat: Windows Line Number Mappings Path is set to \"{overridePath}\", but no {LineNumberMappingsSearch.FileName} is there. Searching the build output and Library/Bee instead.");
			}

			return FindLineNumberMappings(pathToBuiltProject, WindowsBeeFolderPrefixes);
		}

		private static void PostProcessWindows(string pathToBuiltProject, BugSplatOptions options)
		{
			var buildDir = Path.GetDirectoryName(pathToBuiltProject);
			if (buildDir == null)
			{
				Debug.LogError("BugSplat. Could not find build directory. Skipping Windows post-build tasks.");
				return;
			}

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
			var packageRoot = packageInfo?.resolvedPath ?? Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.sdk"));
			var supportDir = Path.Combine(packageRoot, "Runtime", "Plugins", "Windows", "Support~", arch);

			foreach (var fileName in new[] { "BugSplatMonitor.exe", "BugSplatReporter.exe", "BugSplatWer.dll" })
			{
				var source = Path.Combine(supportDir, fileName);
				if (!File.Exists(source))
				{
					Debug.LogError($"BugSplat. Missing native runtime support file {source}. Native crash reports may not upload.");
					continue;
				}

				File.Copy(source, Path.Combine(buildDir, fileName), true);
			}

			// The reporter reads its theme only from BugSplatTheme next to itself, and falls back to
			// built-in defaults silently when it is missing.
			var themeSource = Path.Combine(supportDir, ThemeDirectoryName);
			var themeDestination = Path.Combine(buildDir, ThemeDirectoryName);
			if (Directory.Exists(themeSource))
			{
				if (Directory.Exists(themeDestination))
					Directory.Delete(themeDestination, true);
				CopyDirectory(themeSource, themeDestination);
			}
			else
			{
				Debug.LogError($"BugSplat. Missing crash dialog theme {themeSource}. The dialog will use its built-in defaults.");
			}

			// 8.x shipped the dialog as BugSplatRc.dll; 9.x replaced it with BugSplatReporter.exe.
			var staleRc = Path.Combine(buildDir, "BugSplatRc.dll");
			if (File.Exists(staleRc))
				File.Delete(staleRc);

			Debug.Log($"BugSplat. Copied Windows native runtime support files ({arch}) next to the built executable.");
		}

		private const string ThemeDirectoryName = "BugSplatTheme";

		private static void CopyDirectory(string source, string destination)
		{
			Directory.CreateDirectory(destination);
			foreach (var file in Directory.GetFiles(source))
				File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
			foreach (var directory in Directory.GetDirectories(source))
				CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
		}

		// Library/Bee/artifacts folder name prefixes, which have changed less across Unity versions than
		// the paths beneath them: Win matches WinPlayerBuildProgram and WindowsPlayerBuildProgram.
		private static readonly string[] WindowsBeeFolderPrefixes = { "Win" };
		private static readonly string[] MacBeeFolderPrefixes = { "Mac" };

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

		private static string FindLineNumberMappings(string pathToBuiltProject, string[] beeFolderPrefixes, params string[] extraBeeFolders)
		{
			var projectRoot = Directory.GetCurrentDirectory();
			var beeFolders = LineNumberMappingsSearch.GetBeeArtifactFolders(projectRoot, beeFolderPrefixes)
				.Concat(extraBeeFolders.Select(folder => Path.Combine(projectRoot, folder)));
			var backupFolder = pathToBuiltProject == null ? null : LineNumberMappingsSearch.GetBackupFolder(pathToBuiltProject);

			return LineNumberMappingsSearch.Find(backupFolder, beeFolders);
		}

		// The backend keeps one mappings file per name for a database, application and version, so each
		// platform uploads under its own name and a macOS and an iOS build sharing a version don't
		// overwrite each other's. Windows keeps the original name, which the Xbox add-on uploads too.
		internal const string WindowsLineNumberMappingsName = "LineNumberMappings.json";
		internal const string MacLineNumberMappingsName = "LineNumberMappings-macos.json";
		internal const string IosLineNumberMappingsName = "LineNumberMappings-ios.json";
		internal const string AndroidLineNumberMappingsName = "LineNumberMappings-android.json";

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
			// Never --dumpSyms: that's for Android .so files, and symbol-upload drops a file it can't dump.
			UploadSymbols(directory, fileName, options, false, uploadExitCode =>
			{
				if (uploadExitCode != 0)
					Debug.LogError($"BugSplat. Could not upload {fileName}.");
			});
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

			UploadSymbols(buildDir, "**/*.dSYM", options, uploadExitCode =>
			{
				if (uploadExitCode != 0)
				{
					Debug.LogError("BugSplat. Could not upload macOS symbols.");
					return;
				}

				Debug.Log("BugSplat. macOS symbols uploading completed.");
			});

			var mappingsPath = FindLineNumberMappings(pathToBuiltProject, MacBeeFolderPrefixes);
			if (mappingsPath == null)
			{
				LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget.Standalone, "macOS");
				return;
			}

			UploadLineNumberMappings(mappingsPath, MacLineNumberMappingsName, options);
		}

		internal static BugSplatOptions GetBugSplatOptions() => BugSplatProjectOptions.Get();

#if UNITY_IOS
		private static void PostProcessIos(string pathToBuiltProject, BugSplatOptions options)
		{
			var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

			var project = new PBXProject();
			project.ReadFromString(File.ReadAllText(projectPath));

			var targetGuid = project.GetUnityFrameworkTargetGuid();

			EnableObjectiveCExceptionsForBridge(project, targetGuid);

			project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
			project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-lz");
			project.AddBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");

			project.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

			var mainTargetGuid = project.GetUnityMainTargetGuid();
			project.AddBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
			project.SetBuildProperty(mainTargetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

			project = HandleUploadSymbols(mainTargetGuid, project, options);

			File.WriteAllText(projectPath, project.WriteToString());

			CopyLineNumberMappings(pathToBuiltProject);

			if (options.UseNativeCrashReportingForIos)
				DisableUnityCrashReporter(pathToBuiltProject);
		}

		private static void CopyLineNumberMappings(string pathToBuiltProject)
		{
			// An export into an existing Xcode project keeps files from the last one: drop the copy older
			// versions wrote under the shared name, and last build's mappings in case this build has none.
			foreach (var stale in new[] { WindowsLineNumberMappingsName, IosLineNumberMappingsName })
			{
				var stalePath = Path.Combine(pathToBuiltProject, stale);
				if (File.Exists(stalePath))
					File.Delete(stalePath);
			}

			// An Xcode export has no IL2CPP backup folder beside it, so only Library/Bee is searched.
			var mappingsPath = FindLineNumberMappings(null, new[] { "iOS" });
			if (mappingsPath == null)
			{
				LogMissingLineNumberMappings(UnityEditor.Build.NamedBuildTarget.iOS, "iOS");
				return;
			}

			File.Copy(mappingsPath, Path.Combine(pathToBuiltProject, IosLineNumberMappingsName), true);
			Debug.Log($"BugSplat: Copied {mappingsPath} to the Xcode project as {IosLineNumberMappingsName} ({new FileInfo(mappingsPath).Length / 1024}KB)");
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

		/// <summary>
		/// BugSplatBridge.mm guards its NSFileHandle reads with @try, and Unity compiles UnityFramework
		/// with GCC_ENABLE_OBJC_EXCEPTIONS = NO. The plugin importer carries -fobjc-exceptions for the
		/// file, but a project that imported the plugin before that setting existed can still be building
		/// with the old flags, so it is set on the file here as well.
		///
		/// Per file rather than per target: Unity disables Objective-C exceptions for the whole framework
		/// deliberately, and one bridge needing them is no reason to change how Unity's own code compiles.
		/// The exception is a project where the file cannot be located, where the target property is set
		/// instead - a wider change than wanted, but better than an iOS build that does not compile.
		/// </summary>
		private static void EnableObjectiveCExceptionsForBridge(PBXProject project, string targetGuid)
		{
			const string flag = "-fobjc-exceptions";
			const string bridgePath = "Libraries/com.bugsplat.sdk/Editor/IOS/ObjC/BugSplatBridge.mm";

			var bridgeGuid = project.FindFileGuidByProjectPath(bridgePath);
			if (bridgeGuid == null)
			{
				// The package is somewhere this did not predict. Falling back to the target property keeps
				// the build working, which matters more than the narrower scope.
				Debug.Log($"BugSplat info: could not find {bridgePath} in the Xcode project; enabling Objective-C exceptions for the whole UnityFramework target instead.");
				project.SetBuildProperty(targetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");
				return;
			}

			var flags = project.GetCompileFlagsForFile(targetGuid, bridgeGuid) ?? new List<string>();
			if (flags.Contains(flag))
			{
				return;
			}

			flags.Add(flag);
			project.SetCompileFlagsForFile(targetGuid, bridgeGuid, flags);
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
				$"\"$SYMBOL_UPLOAD\" \\\n" +
				$"    --database \"{options.Database}\" \\\n" +
				$"    --application \"{application}\" \\\n" +
				$"    --version \"{version}\" \\\n" +
				$"    --files \"**/*.dSYM\" \\\n" +
				$"    --directory \"${{BUILT_PRODUCTS_DIR}}\"\n\n" +
				$"# Upload IL2CPP's LineNumberMappings.json, copied here as {IosLineNumberMappingsName}, for C# symbolication.\n" +
				$"MAPPINGS=\"${{PROJECT_DIR}}/{IosLineNumberMappingsName}\"\n" +
				$"if [ -f \"$MAPPINGS\" ]; then\n" +
				$"    \"$SYMBOL_UPLOAD\" \\\n" +
				$"        --database \"{options.Database}\" \\\n" +
				$"        --application \"{application}\" \\\n" +
				$"        --version \"{version}\" \\\n" +
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
		private static readonly string AndroidIl2CppBackupFolder = Path.Combine("Library", "Bee", "Android", "Prj", "IL2CPP", "Il2CppBackup");

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

			var mappingsPath = FindLineNumberMappings(pathToBuiltProject, new[] { "Android" }, AndroidIl2CppBackupFolder);
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
			});
		}
#endif

		internal static void UploadSymbols(string artifactsDirPath, string globPattern, BugSplatOptions options, Action<int> onCompleted) =>
			UploadSymbols(artifactsDirPath, globPattern, options, EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, onCompleted);

		private static void UploadSymbols(string artifactsDirPath, string globPattern, BugSplatOptions options, bool dumpSyms, Action<int> onCompleted)
		{
			var exitCode = RunSymbolUpload(artifactsDirPath, globPattern, options, dumpSyms);
			if (exitCode != 0)
				FailedSymbolUploads.Add($"{globPattern} from {artifactsDirPath}");

			onCompleted(exitCode);
		}

		static readonly TimeSpan SymbolUploadHeartbeatInterval = TimeSpan.FromMinutes(1);

		internal static TimeSpan GetSymbolUploadTimeout(int minutes) =>
			minutes > 0 ? TimeSpan.FromMinutes(minutes) : Timeout.InfiniteTimeSpan;

		private static int RunSymbolUpload(string artifactsDirPath, string globPattern, BugSplatOptions options, bool dumpSyms)
		{
			if (!BugSplatSymbolUploadCredentials.TryResolve(options.Database, out var clientId, out var clientSecret))
			{
				Debug.LogWarning(
					$"BugSplat: no symbol upload credentials for database '{options.Database}'. Set " +
					$"{BugSplatSymbolUploadCredentials.ClientIdEnvironmentVariable} and " +
					$"{BugSplatSymbolUploadCredentials.ClientSecretEnvironmentVariable}, or use " +
					"Tools > BugSplat > Symbol Upload > Set Credentials. Skipping symbol uploads.");
				return 0;
			}

			var symbolUploadPath = GetSymUploaderPath();
			if (!SymbolUploadRelease.IsVerified(symbolUploadPath, GetSymUploaderName())
				&& !DownloadSymbolUpload(symbolUploadPath))
			{
				return -1;
			}

			var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
			var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;

			var symUploadProcessInfo = new ProcessStartInfo
			{
				FileName = symbolUploadPath,
				Arguments = $"--database {options.Database} --application \"{application}\" " +
					$"--version \"{version}\" --files \"{globPattern}\" --directory \"{artifactsDirPath}\""
			};

			symUploadProcessInfo.EnvironmentVariables["SYMBOL_UPLOAD_CLIENT_ID"] = clientId;
			symUploadProcessInfo.EnvironmentVariables["SYMBOL_UPLOAD_CLIENT_SECRET"] = clientSecret;

			if (dumpSyms)
			{
				symUploadProcessInfo.Arguments += " --dumpSyms";
			}

			var timeout = GetSymbolUploadTimeout(options.SymbolUploadTimeoutMinutes);
			var bounded = timeout > TimeSpan.Zero;
			var limit = bounded ? $" (stops after {ProcessRunner.FormatDuration(timeout)})" : string.Empty;
			var showProgress = !Application.isBatchMode && InternalEditorUtility.CurrentThreadIsMainThread();
			var nextHeartbeat = SymbolUploadHeartbeatInterval;

			bool OnPoll(TimeSpan elapsed, string lastLine)
			{
				if (showProgress)
				{
					var progress = bounded
						? (float)(elapsed.TotalSeconds / timeout.TotalSeconds)
						: (float)(elapsed.TotalSeconds % 60 / 60);
					return EditorUtility.DisplayCancelableProgressBar(
						"BugSplat Symbol Upload",
						$"Uploading {globPattern} to {options.Database}, {ProcessRunner.FormatDuration(elapsed)}{limit}. {lastLine}",
						progress);
				}

				if (elapsed >= nextHeartbeat)
				{
					nextHeartbeat += SymbolUploadHeartbeatInterval;
					Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
						"BugSplat: symbol upload of {0} still running after {1}{2}. Last output: {3}",
						globPattern, ProcessRunner.FormatDuration(elapsed), limit, lastLine ?? "none");
				}

				return false;
			}

			Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
				"BugSplat: uploading {0} from {1} to {2}{3}.", globPattern, artifactsDirPath, options.Database, limit);

			ProcessRunResult result;
			try
			{
				result = ProcessRunner.Run(symUploadProcessInfo, timeout, OnPoll);
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat. Failed to start {symbolUploadPath}. Error: {ex}");
				return -1;
			}
			finally
			{
				if (showProgress)
					EditorUtility.ClearProgressBar();
			}

			if (result.TimedOut)
			{
				Debug.LogError(
					$"BugSplat: the upload of {globPattern} from {artifactsDirPath} did not finish within {ProcessRunner.FormatDuration(timeout)} and was stopped. " +
					$"Check the network connection, and that security software is not blocking or quarantining {symbolUploadPath}. " +
					"If the symbols are just large, raise Symbol Upload Timeout Minutes in Edit > Project Settings > BugSplat (0 waits indefinitely). " +
					$"Output so far:\n{result.Output}");
				return -1;
			}

			if (result.Cancelled)
			{
				Debug.LogError($"BugSplat: the upload of {globPattern} from {artifactsDirPath} was cancelled, so those symbols were not uploaded. Output so far:\n{result.Output}");
				return -1;
			}

			if (result.ExitCode != 0)
			{
				Debug.LogError($"BugSplat: symbol-upload exited with code {result.ExitCode} uploading {globPattern} from {artifactsDirPath}. Output:\n{result.Output}");
				return result.ExitCode;
			}

			Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", result.Output);
			return 0;
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

			if (Application.platform == RuntimePlatform.WindowsEditor)
			{
				return true;
			}

			try
			{
				var absolutePath = Path.GetFullPath(destinationPath);

				var chmod = ProcessRunner.Run(new ProcessStartInfo("chmod", $"+x \"{absolutePath}\""), TimeSpan.FromSeconds(30));
				if (!chmod.Succeeded)
				{
					Debug.LogError($"BugSplat. Failed to make {destinationPath} executable{(chmod.TimedOut ? " (chmod timed out)" : "")}. Output: {chmod.Output}");
					return false;
				}

				Debug.Log($"BugSplat. Successfully made {destinationPath} executable.");
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
