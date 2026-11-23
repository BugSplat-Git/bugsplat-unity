using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	[Serializable]
	internal class WebGLBuildSettings
	{
		public WebGLDebugSymbolMode DebugSymbolMode;
		public string EmscriptenArgs = "";
		public string Il2CppArgs = "";
		public string LinkerArgs = "";

		internal bool Matches(WebGLBuildSettings other) =>
			other != null
			&& DebugSymbolMode == other.DebugSymbolMode
			&& EmscriptenArgs == other.EmscriptenArgs
			&& Il2CppArgs == other.Il2CppArgs
			&& LinkerArgs == other.LinkerArgs;
	}

	/// <summary>
	/// While a WebGL build with symbol upload runs, sets what line information needs and restores the
	/// project's own settings afterwards.
	/// </summary>
	internal class WebGLBuild : IPreprocessBuildWithReport
	{
		internal const string LinkerArgsEnvironmentVariable = "UNITYLINKER_ADDITIONAL_ARGS";
		internal const string SourceMapArg = "-gsource-map";
		internal static readonly string[] Il2CppLineInfoArgs = { "--compiler-flags=-g", "--emit-source-mapping" };
		internal const string LinkSymbolsArg = "--link-symbols";
		internal const string LineNumberMappingsName = "LineNumberMappings-webgl.json";
		internal const string JspreAssetPath = "Packages/com.bugsplat.sdk/Runtime/Plugins/WebGL/BugSplatWebGL.jspre";

		static readonly string SavedSettingsPath = Path.Combine("Library", "BugSplat", "webgl-build-settings.json");
		static readonly string BeeBuildDir = Path.Combine("Library", "Bee", "artifacts", "WebGL", "build", "debug_WebGL_wasm");
		static readonly string BeeLineNumberMappings = Path.Combine("Library", "Bee", "artifacts", "WebGL", "il2cppOutput", "cpp", "Symbols", "LineNumberMappings.json");

		// Set while a build runs with our settings applied; null otherwise.
		internal static WebGLBuildSettings Original { get; private set; }

		public int callbackOrder => 0;

		[InitializeOnLoadMethod]
		static void OnLoad()
		{
			FilterJspre();

			if (!BuildPipeline.isBuildingPlayer)
				RestoreInterruptedBuild();
		}

		public void OnPreprocessBuild(BuildReport report)
		{
			RestoreInterruptedBuild();

			if (report.summary.platform != BuildTarget.WebGL)
				return;

			FilterJspre();

			var options = BuildPostprocessors.GetBugSplatOptions();
			if (options == null || !options.UploadDebugSymbolsForWebGL)
				return;

			var original = Apply();
			var applied = Read();

			if (original.DebugSymbolMode == WebGLDebugSymbolMode.Embedded)
				Debug.Log("BugSplat: WebGL Debug Symbols is Embedded, so there is no symbols.json to upload; function names come from the wasm's name section instead.");

			Debug.Log($"BugSplat: for this WebGL build, Debug Symbols = {applied.DebugSymbolMode}, emscripten args \"{applied.EmscriptenArgs}\", IL2CPP args \"{applied.Il2CppArgs}\", {LinkerArgsEnvironmentVariable}=\"{applied.LinkerArgs}\". Your settings are restored after the build.");
		}

		static void FilterJspre()
		{
			if (AssetImporter.GetAtPath(JspreAssetPath) is PluginImporter jspre)
				jspre.SetIncludeInBuildDelegate(_ => ShouldIncludeJspre(BuildPostprocessors.GetBugSplatOptions(), PlayerBuildDefines.For(NamedBuildTarget.WebGL)));
		}

		internal static WebGLBuildSettings Apply()
		{
			var original = Read();
			Directory.CreateDirectory(Path.GetDirectoryName(SavedSettingsPath));
			File.WriteAllText(SavedSettingsPath, JsonUtility.ToJson(original));
			Original = original;
			Write(WithLineInfo(original));
			return original;
		}

		internal static WebGLBuildSettings WithLineInfo(WebGLBuildSettings original) =>
			new WebGLBuildSettings
			{
				DebugSymbolMode = original.DebugSymbolMode == WebGLDebugSymbolMode.Off ? WebGLDebugSymbolMode.External : original.DebugSymbolMode,
				EmscriptenArgs = AppendArgs(original.EmscriptenArgs, SourceMapArg),
				Il2CppArgs = AppendArgs(original.Il2CppArgs, Il2CppLineInfoArgs),
				LinkerArgs = AppendArgs(original.LinkerArgs, LinkSymbolsArg),
			};

		internal static string AppendArgs(string existing, params string[] args)
		{
			var current = (existing ?? "").Trim();
			var tokens = new HashSet<string>(Regex.Split(current, @"\s+"));
			var builder = new StringBuilder(current);
			foreach (var arg in args)
			{
				if (tokens.Contains(arg))
					continue;
				if (builder.Length > 0)
					builder.Append(' ');
				builder.Append(arg);
			}
			return builder.ToString();
		}

		internal static bool HasSourceMapArg(string emscriptenArgs) =>
			Regex.Split((emscriptenArgs ?? "").Trim(), @"\s+").Any(arg => arg.StartsWith(SourceMapArg, StringComparison.Ordinal));

		internal static WebGLBuildSettings Read() =>
			new WebGLBuildSettings
			{
				DebugSymbolMode = PlayerSettings.WebGL.debugSymbolMode,
				EmscriptenArgs = PlayerSettings.WebGL.emscriptenArgs ?? "",
				Il2CppArgs = PlayerSettings.GetAdditionalIl2CppArgs() ?? "",
				LinkerArgs = Environment.GetEnvironmentVariable(LinkerArgsEnvironmentVariable) ?? "",
			};

		internal static void Write(WebGLBuildSettings settings)
		{
			PlayerSettings.WebGL.debugSymbolMode = settings.DebugSymbolMode;
			PlayerSettings.WebGL.emscriptenArgs = settings.EmscriptenArgs;
			PlayerSettings.SetAdditionalIl2CppArgs(settings.Il2CppArgs);
			Environment.SetEnvironmentVariable(LinkerArgsEnvironmentVariable, string.IsNullOrEmpty(settings.LinkerArgs) ? null : settings.LinkerArgs);
		}

		internal static void Restore()
		{
			if (Original == null)
				return;

			Write(Original);
			Original = null;
			try
			{
				File.Delete(SavedSettingsPath);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				Debug.LogWarning($"BugSplat: could not delete {SavedSettingsPath}: {e.Message}");
			}
		}

		// A build that fails never reaches the postprocess callbacks, so the settings it changed are
		// put back on the next build or editor load instead.
		static void RestoreInterruptedBuild()
		{
			if (Original != null)
			{
				Restore();
				return;
			}

			if (!File.Exists(SavedSettingsPath))
				return;

			try
			{
				Original = JsonUtility.FromJson<WebGLBuildSettings>(File.ReadAllText(SavedSettingsPath));
			}
			catch (Exception e)
			{
				Debug.LogWarning($"BugSplat: could not read {SavedSettingsPath}: {e.Message}");
				File.Delete(SavedSettingsPath);
				return;
			}

			Debug.Log("BugSplat: restoring the WebGL build settings changed by a build that did not finish.");
			Restore();
		}

		internal static string GetWasmBase(string fileName)
		{
			var name = Path.GetFileName(fileName);
			foreach (var suffix in new[] { ".br", ".gz", ".unityweb" })
			{
				if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				{
					name = name.Substring(0, name.Length - suffix.Length);
					break;
				}
			}

			return name.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - ".wasm".Length) : null;
		}

		internal static string FindShippedWasm(string buildDir)
		{
			if (!Directory.Exists(buildDir))
				return null;

			var wasm = Directory.GetFiles(buildDir).Where(path => GetWasmBase(path) != null).ToArray();
			return wasm.Length == 1 ? wasm[0] : null;
		}

		// Emscripten always appends a sourceMappingURL section for -gsource-map; without it the wasm is
		// byte-identical to a build without the flag.
		internal static byte[] RemoveSourceMappingUrlSection(byte[] wasm)
		{
			if (wasm.Length < 8 || wasm[0] != 0 || wasm[1] != (byte)'a' || wasm[2] != (byte)'s' || wasm[3] != (byte)'m')
				return null;

			var position = 8;
			while (position < wasm.Length)
			{
				var start = position;
				var id = wasm[position++];
				var size = ReadLeb128(wasm, ref position);
				var contentStart = position;
				if (size < 0 || contentStart + size > wasm.Length)
					return null;

				if (id == 0)
				{
					var namePosition = contentStart;
					var nameLength = ReadLeb128(wasm, ref namePosition);
					if (nameLength >= 0 && namePosition + nameLength <= contentStart + size
						&& Encoding.UTF8.GetString(wasm, namePosition, nameLength) == "sourceMappingURL")
					{
						var stripped = new byte[wasm.Length - (contentStart + size - start)];
						Buffer.BlockCopy(wasm, 0, stripped, 0, start);
						Buffer.BlockCopy(wasm, contentStart + size, stripped, start, wasm.Length - contentStart - size);
						return stripped;
					}
				}

				position = contentStart + size;
			}

			return null;
		}

		static int ReadLeb128(byte[] bytes, ref int position)
		{
			var result = 0;
			var shift = 0;
			while (position < bytes.Length && shift < 35)
			{
				var b = bytes[position++];
				result |= (b & 0x7f) << shift;
				if ((b & 0x80) == 0)
					return result;
				shift += 7;
			}
			return -1;
		}

		internal static string RemoveSymbolsUrl(string html, string symbolsFileName)
		{
			var property = new Regex(@"^\s*symbolsUrl\s*:\s*[^,;{}]*[""'][^""']*" + Regex.Escape(symbolsFileName) + @"[""']\s*,?\s*\r?$");
			var kept = new List<string>();
			foreach (var line in html.Split('\n'))
			{
				if (property.IsMatch(line))
					continue;
				if (line.Contains("symbolsUrl") && line.Contains(symbolsFileName))
					Debug.LogWarning($"BugSplat: left the symbolsUrl reference to {symbolsFileName} in index.html because it shares a line with other code. The file was not deployed, so remove the reference by hand.");
				kept.Add(line);
			}
			return string.Join("\n", kept);
		}

		const string BuildConfigMarker = "/* BugSplat build config */";

		internal static string InjectBuildConfig(string loaderJs, string configJson)
		{
			var markerIndex = loaderJs.IndexOf("\n" + BuildConfigMarker, StringComparison.Ordinal);
			if (markerIndex >= 0)
				loaderJs = loaderJs.Substring(0, markerIndex);

			return loaderJs + "\n" + BuildConfigMarker +
				"(function(){if(typeof createUnityInstance!==\"function\")return;var create=createUnityInstance;" +
				"createUnityInstance=function(canvas,config,onProgress){var merged={};config=config||{};" +
				"for(var key in config)merged[key]=config[key];" +
				"if(!merged.bugsplat)merged.bugsplat=" + configJson + ";" +
				"return create.call(this,canvas,merged,onProgress)}})();\n";
		}

		internal static string BuildConfigJson(BugSplatOptions options, string productName, string bundleVersion)
		{
			var attributes = new Dictionary<string, string>();
			foreach (var attribute in options.Attributes ?? new List<BugSplatAttribute>())
			{
				if (attribute != null && !string.IsNullOrEmpty(attribute.Name))
					attributes[attribute.Name] = attribute.Value ?? "";
			}

			var config = Runtime.Util.AttributesJson.Serialize(new Dictionary<string, string>
			{
				{ "database", options.Database },
				{ "application", string.IsNullOrEmpty(options.Application) ? productName : options.Application },
				{ "version", string.IsNullOrEmpty(options.Version) ? bundleVersion : options.Version },
				{ "key", options.Key },
				{ "user", options.User },
				{ "email", options.Email },
				{ "description", options.Description },
				{ "notes", options.Notes },
			});

			return config.Substring(0, config.Length - 1) + ",\"attributes\":" + Runtime.Util.AttributesJson.Serialize(attributes) + "}";
		}

		internal static bool IsDisabled(string scriptingDefines) =>
			(scriptingDefines ?? "").Split(';', ',').Select(define => define.Trim()).Contains(BugSplatOptions.DisabledDefine);

		internal static bool ShouldIncludeJspre(BugSplatOptions options, string scriptingDefines) =>
			(options?.CaptureWebAssemblyCrashes ?? true) && !IsDisabled(scriptingDefines);

		internal static bool ShouldBakeConfig(BugSplatOptions options, string scriptingDefines) =>
			options != null
			&& options.Enabled
			&& options.InitializeAutomatically
			&& options.CaptureWebAssemblyCrashes
			&& !string.IsNullOrEmpty(options.Database)
			&& !IsDisabled(scriptingDefines);

		/// <summary>
		/// Post-build steps for a WebGL player: bakes the options into the loader so a crash before C#
		/// starts can still be reported, puts the deployed output back the way the project's own
		/// settings would have left it, and uploads the symbol files.
		/// </summary>
		internal static void PostProcess(string pathToBuiltProject, BugSplatOptions options)
		{
			var buildDir = Path.Combine(pathToBuiltProject, "Build");

			BakeConfig(buildDir, options);

			var original = Original;
			if (original == null)
				return;

			var wasmPath = FindShippedWasm(buildDir);
			if (wasmPath == null)
			{
				Debug.LogError($"BugSplat: could not find exactly one .wasm in {buildDir}, so WebGL symbols were not uploaded.");
				return;
			}

			var wasmBase = GetWasmBase(wasmPath);

			if (!HasSourceMapArg(original.EmscriptenArgs))
				RemoveSourceMappingUrl(wasmPath);

			var stagingDir = Path.GetFullPath(Path.Combine("Temp", "BugSplat", Guid.NewGuid().ToString("N")));
			try
			{
				Directory.CreateDirectory(stagingDir);
				var staged = new List<string>();

				var symbols = Path.GetFullPath(Path.Combine(BeeBuildDir, "build.js.symbols.json"));
				if (original.DebugSymbolMode != WebGLDebugSymbolMode.Embedded)
					Stage(symbols, Path.Combine(stagingDir, wasmBase + ".symbols.json"), staged);

				Stage(Path.GetFullPath(Path.Combine(BeeBuildDir, "build.wasm.map")), Path.Combine(stagingDir, wasmBase + ".wasm.map"), staged);
				Stage(Path.GetFullPath(BeeLineNumberMappings), Path.Combine(stagingDir, LineNumberMappingsName), staged);

				if (original.DebugSymbolMode == WebGLDebugSymbolMode.Off)
					RemoveDeployedSymbols(pathToBuiltProject, buildDir);

				if (staged.Count == 0)
					return;

				Debug.Log($"BugSplat: uploading WebGL symbols: {string.Join(", ", staged)}");
				BuildPostprocessors.UploadSymbols(stagingDir, "*", options, exitCode =>
				{
					if (exitCode != 0)
						Debug.LogError("BugSplat. Could not upload WebGL symbols.");
				});
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				Debug.LogError($"BugSplat: could not stage WebGL symbols for upload: {e.Message}");
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

		static void Stage(string source, string destination, List<string> staged)
		{
			if (!File.Exists(source))
			{
				Debug.LogWarning($"BugSplat: {source} not found, so {Path.GetFileName(destination)} was not uploaded.");
				return;
			}

			File.Copy(source, destination, true);
			staged.Add(Path.GetFileName(destination));
		}

		static void BakeConfig(string buildDir, BugSplatOptions options)
		{
			var defines = PlayerBuildDefines.For(NamedBuildTarget.WebGL);
			if (!ShouldBakeConfig(options, defines) || !Directory.Exists(buildDir))
				return;

			var loaders = Directory.GetFiles(buildDir, "*.loader.js");
			if (loaders.Length != 1)
			{
				Debug.LogWarning($"BugSplat: could not find exactly one loader.js in {buildDir}, so a WebAssembly crash before BugSplat initializes will not be reported.");
				return;
			}

			var config = BuildConfigJson(options, PlayerSettings.productName, PlayerSettings.bundleVersion);
			File.WriteAllText(loaders[0], InjectBuildConfig(File.ReadAllText(loaders[0]), config));
		}

		static void RemoveSourceMappingUrl(string wasmPath)
		{
			if (!wasmPath.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
			{
				Debug.Log($"BugSplat: {Path.GetFileName(wasmPath)} is compressed, so the sourceMappingURL section -gsource-map added to it (34 bytes, naming build.wasm.map) stays in.");
				return;
			}

			var stripped = RemoveSourceMappingUrlSection(File.ReadAllBytes(wasmPath));
			if (stripped != null)
				File.WriteAllBytes(wasmPath, stripped);
		}

		// External symbols were only turned on for the upload, so the deployed build gets neither the
		// function-name file nor the index.html reference the project's own settings would not have made.
		static void RemoveDeployedSymbols(string pathToBuiltProject, string buildDir)
		{
			foreach (var symbols in Directory.GetFiles(buildDir, "*.symbols.json*"))
			{
				File.Delete(symbols);

				var index = Path.Combine(pathToBuiltProject, "index.html");
				if (File.Exists(index))
				{
					var html = File.ReadAllText(index);
					var updated = RemoveSymbolsUrl(html, Path.GetFileName(symbols));
					if (updated != html)
						File.WriteAllText(index, updated);
				}
			}
		}
	}

	// After PostBuild's [PostProcessBuild(1)], which reads WebGLBuild.Original.
	internal class WebGLBuildRestore : IPostprocessBuildWithReport
	{
		public int callbackOrder => 1000;

		public void OnPostprocessBuild(BuildReport report) => WebGLBuild.Restore();
	}
}
