using System;
using System.Collections.Generic;
using System.Text;
using BugSplatUnity.Editor;
using BugSplatUnity.Runtime.Client;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.EditorTests
{
	public class WebGLBuildTests
	{
		[TestCase("MyGame.wasm", "MyGame")]
		[TestCase("MyGame.wasm.br", "MyGame")]
		[TestCase("MyGame.wasm.gz", "MyGame")]
		[TestCase("MyGame.wasm.unityweb", "MyGame")]
		[TestCase("Build/0f3a9c1e2b.wasm.br", "0f3a9c1e2b")]
		[TestCase("My.Game.WASM", "My.Game")]
		[TestCase("MyGame.data.br", null)]
		[TestCase("MyGame.framework.js", null)]
		[TestCase("MyGame.symbols.json", null)]
		public void GetWasmBase_ShouldStripTheCompressionSuffixAndWasmExtension(string fileName, string expected)
		{
			Assert.AreEqual(expected, WebGLBuild.GetWasmBase(fileName));
		}

		[TestCase(null, "-gsource-map")]
		[TestCase("", "-gsource-map")]
		[TestCase("-sFOO=1", "-sFOO=1 -gsource-map")]
		[TestCase("  -sFOO=1  ", "-sFOO=1 -gsource-map")]
		[TestCase("-gsource-map -sFOO=1", "-gsource-map -sFOO=1")]
		public void AppendArgs_ShouldAppendMissingArgsOnly(string existing, string expected)
		{
			Assert.AreEqual(expected, WebGLBuild.AppendArgs(existing, "-gsource-map"));
		}

		[Test]
		public void WithLineInfo_ShouldAppendToTheProjectsOwnArgs()
		{
			var original = new WebGLBuildSettings
			{
				DebugSymbolMode = WebGLDebugSymbolMode.Off,
				EmscriptenArgs = "-sTOTAL_STACK=1048576",
				Il2CppArgs = "--some-arg",
				LinkerArgs = "--verbose"
			};

			var applied = WebGLBuild.WithLineInfo(original);

			Assert.AreEqual(WebGLDebugSymbolMode.External, applied.DebugSymbolMode);
			Assert.AreEqual("-sTOTAL_STACK=1048576 -gsource-map", applied.EmscriptenArgs);
			Assert.AreEqual("--some-arg --compiler-flags=-g --emit-source-mapping", applied.Il2CppArgs);
			Assert.AreEqual("--verbose --link-symbols", applied.LinkerArgs);
			Assert.AreEqual(WebGLDebugSymbolMode.Off, original.DebugSymbolMode, "the original must not change");
		}

		[TestCase(WebGLDebugSymbolMode.Off, WebGLDebugSymbolMode.External)]
		[TestCase(WebGLDebugSymbolMode.External, WebGLDebugSymbolMode.External)]
		[TestCase(WebGLDebugSymbolMode.Embedded, WebGLDebugSymbolMode.Embedded)]
		public void WithLineInfo_ShouldOnlyTurnDebugSymbolsOnWhenTheyAreOff(WebGLDebugSymbolMode original, WebGLDebugSymbolMode expected)
		{
			Assert.AreEqual(expected, WebGLBuild.WithLineInfo(new WebGLBuildSettings { DebugSymbolMode = original }).DebugSymbolMode);
		}

		[Test]
		public void WithLineInfo_ShouldBeIdempotent()
		{
			var once = WebGLBuild.WithLineInfo(new WebGLBuildSettings());
			Assert.IsTrue(once.Matches(WebGLBuild.WithLineInfo(once)));
		}

		[Test]
		public void ApplyAndRestore_ShouldPutTheProjectSettingsBack()
		{
			var before = WebGLBuild.Read();
			var custom = new WebGLBuildSettings
			{
				DebugSymbolMode = WebGLDebugSymbolMode.Off,
				EmscriptenArgs = "-sBUGSPLAT_TEST=1",
				Il2CppArgs = "--bugsplat-test",
				LinkerArgs = ""
			};

			try
			{
				WebGLBuild.Write(custom);

				var original = WebGLBuild.Apply();
				var applied = WebGLBuild.Read();

				Assert.IsTrue(original.Matches(custom));
				Assert.AreSame(original, WebGLBuild.Original);
				Assert.AreEqual(WebGLDebugSymbolMode.External, applied.DebugSymbolMode);
				Assert.AreEqual("-sBUGSPLAT_TEST=1 -gsource-map", applied.EmscriptenArgs);
				Assert.AreEqual("--bugsplat-test --compiler-flags=-g --emit-source-mapping", applied.Il2CppArgs);
				Assert.AreEqual("--link-symbols", Environment.GetEnvironmentVariable(WebGLBuild.LinkerArgsEnvironmentVariable));

				WebGLBuild.Restore();

				Assert.IsNull(WebGLBuild.Original);
				Assert.IsTrue(WebGLBuild.Read().Matches(custom));
				Assert.IsNull(Environment.GetEnvironmentVariable(WebGLBuild.LinkerArgsEnvironmentVariable));
			}
			finally
			{
				WebGLBuild.Restore();
				WebGLBuild.Write(before);
			}
		}

		[TestCase("-gsource-map", true)]
		[TestCase("-O3 -gsource-map=inline", true)]
		[TestCase("-g", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void HasSourceMapArg_ShouldRecognizeTheFlag(string args, bool expected)
		{
			Assert.AreEqual(expected, WebGLBuild.HasSourceMapArg(args));
		}

		static byte[] Section(byte id, byte[] content)
		{
			var bytes = new List<byte> { id, (byte)content.Length };
			bytes.AddRange(content);
			return bytes.ToArray();
		}

		static byte[] CustomSection(string name, string payload)
		{
			var content = new List<byte> { (byte)name.Length };
			content.AddRange(Encoding.UTF8.GetBytes(name));
			content.AddRange(Encoding.UTF8.GetBytes(payload));
			return Section(0, content.ToArray());
		}

		static byte[] Wasm(params byte[][] sections)
		{
			var bytes = new List<byte> { 0, (byte)'a', (byte)'s', (byte)'m', 1, 0, 0, 0 };
			foreach (var section in sections)
				bytes.AddRange(section);
			return bytes.ToArray();
		}

		[Test]
		public void RemoveSourceMappingUrlSection_ShouldReturnTheWasmWithoutTheSection()
		{
			var type = Section(1, new byte[] { 1, 0x60, 0, 0 });
			var code = Section(10, new byte[] { 1, 2, 0, 0x0b });
			var url = CustomSection("sourceMappingURL", "\u000ebuild.wasm.map");

			var stripped = WebGLBuild.RemoveSourceMappingUrlSection(Wasm(type, code, url));

			CollectionAssert.AreEqual(Wasm(type, code), stripped);
		}

		[Test]
		public void RemoveSourceMappingUrlSection_ShouldKeepOtherCustomSections()
		{
			var name = CustomSection("name", "xyz");
			var url = CustomSection("sourceMappingURL", "\u0003a.m");
			var code = Section(10, new byte[] { 0 });

			CollectionAssert.AreEqual(Wasm(name, code), WebGLBuild.RemoveSourceMappingUrlSection(Wasm(name, url, code)));
		}

		[Test]
		public void RemoveSourceMappingUrlSection_ShouldReturnNullWithoutTheSectionOrForNonWasm()
		{
			Assert.IsNull(WebGLBuild.RemoveSourceMappingUrlSection(Wasm(Section(10, new byte[] { 0 }))));
			Assert.IsNull(WebGLBuild.RemoveSourceMappingUrlSection(Encoding.UTF8.GetBytes("not a wasm file")));
		}

		[Test]
		public void RemoveSymbolsUrl_ShouldDropOnlyTheSymbolsUrlLine()
		{
			var html = "        codeUrl: buildUrl + \"/MyGame.wasm\",\r\n        symbolsUrl: buildUrl + \"/MyGame.symbols.json\",\r\n        streamingAssetsUrl: \"StreamingAssets\",\r\n";

			Assert.AreEqual(
				"        codeUrl: buildUrl + \"/MyGame.wasm\",\r\n        streamingAssetsUrl: \"StreamingAssets\",\r\n",
				WebGLBuild.RemoveSymbolsUrl(html, "MyGame.symbols.json"));
		}

		[Test]
		public void InjectBuildConfig_ShouldAppendOnceAndReplaceAnEarlierConfig()
		{
			var loader = "function createUnityInstance(r,t,l){return t}";

			var once = WebGLBuild.InjectBuildConfig(loader, "{\"database\":\"a\"}");
			var twice = WebGLBuild.InjectBuildConfig(once, "{\"database\":\"b\"}");

			StringAssert.StartsWith(loader, twice);
			StringAssert.Contains("{\"database\":\"b\"}", twice);
			StringAssert.DoesNotContain("{\"database\":\"a\"}", twice);
			Assert.AreEqual(once.Length, twice.Length);
		}

		static BugSplatOptions Options()
		{
			var options = ScriptableObject.CreateInstance<BugSplatOptions>();
			options.Database = "fred";
			return options;
		}

		[Test]
		public void BuildConfigJson_ShouldFallBackToProductNameAndVersionAndIncludeAttributes()
		{
			var options = Options();
			options.User = "bobby";
			options.Attributes = new List<BugSplatAttribute>
			{
				new BugSplatAttribute { Name = "level", Value = "3" },
				new BugSplatAttribute { Name = "", Value = "skipped" },
				null
			};

			var json = WebGLBuild.BuildConfigJson(options, "My Game", "1.2.3");

			Assert.AreEqual(
				"{\"database\":\"fred\",\"application\":\"My Game\",\"version\":\"1.2.3\",\"key\":null,\"user\":\"bobby\"," +
				"\"email\":null,\"description\":null,\"notes\":null,\"attributes\":{\"level\":\"3\"}}",
				json);
			UnityEngine.Object.DestroyImmediate(options);
		}

		[Test]
		public void ShouldBakeConfig_ShouldRespectConsentAndTheOptOuts()
		{
			var options = Options();
			Assert.IsTrue(WebGLBuild.ShouldBakeConfig(options, "FOO;BAR"));
			Assert.IsFalse(WebGLBuild.ShouldBakeConfig(options, "FOO;BUGSPLAT_DISABLED"));

			options.InitializeAutomatically = false;
			Assert.IsFalse(WebGLBuild.ShouldBakeConfig(options, ""), "waiting for consent: nothing may post before Initialize");

			options.InitializeAutomatically = true;
			options.CaptureWebAssemblyCrashes = false;
			Assert.IsFalse(WebGLBuild.ShouldBakeConfig(options, ""));

			options.CaptureWebAssemblyCrashes = true;
			options.Enabled = false;
			Assert.IsFalse(WebGLBuild.ShouldBakeConfig(options, ""));

			options.Enabled = true;
			options.Database = "";
			Assert.IsFalse(WebGLBuild.ShouldBakeConfig(options, ""));
			Assert.IsFalse(WebGLBuild.ShouldBakeConfig(null, ""));
			UnityEngine.Object.DestroyImmediate(options);
		}
	}
}
