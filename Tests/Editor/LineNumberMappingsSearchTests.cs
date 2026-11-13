using System;
using System.IO;
using System.Linq;
using BugSplatUnity.Editor;
using NUnit.Framework;

namespace BugSplatUnity.EditorTests
{
	public class LineNumberMappingsSearchTests
	{
		string root;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "bugsplat-mappings-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(root))
				Directory.Delete(root, true);
		}

		string WriteMappings(DateTime writtenUtc, params string[] folders)
		{
			var folder = Path.Combine(new[] { root }.Concat(folders).ToArray());
			Directory.CreateDirectory(folder);
			var path = Path.Combine(folder, LineNumberMappingsSearch.FileName);
			File.WriteAllText(path, "{}");
			File.SetLastWriteTimeUtc(path, writtenUtc);
			return path;
		}

		static readonly DateTime Older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		static readonly DateTime Newer = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

		[Test]
		public void GetBackupFolder_ShouldSitNextToThePlayer()
		{
			var player = Path.Combine(root, "Build", "My Game.exe");

			Assert.AreEqual(
				Path.Combine(root, "Build", "My Game_BackUpThisFolder_ButDontShipItWithYourGame"),
				LineNumberMappingsSearch.GetBackupFolder(player));
		}

		[Test]
		public void Find_ShouldSearchTheBackupFolderAtAnyDepth()
		{
			var expected = WriteMappings(Older, "Game_BackUpThisFolder_ButDontShipItWithYourGame", "il2cppOutput", "cpp", "Symbols");

			Assert.AreEqual(expected, LineNumberMappingsSearch.Find(Path.Combine(root, "Game_BackUpThisFolder_ButDontShipItWithYourGame"), null));
		}

		[Test]
		public void Find_ShouldPreferTheBackupFolderOverNewerBeeOutput()
		{
			var backedUp = WriteMappings(Older, "Backup", "il2cppOutput", "Symbols");
			WriteMappings(Newer, "Bee", "WinPlayerBuildProgram", "il2cppOutput", "cpp", "Symbols");

			Assert.AreEqual(backedUp, LineNumberMappingsSearch.Find(Path.Combine(root, "Backup"), new[] { Path.Combine(root, "Bee", "WinPlayerBuildProgram") }));
		}

		[Test]
		public void Find_ShouldPickTheNewestAcrossBeeFolders()
		{
			WriteMappings(Older, "WindowsPlayerBuildProgram", "il2cppOutput");
			var newest = WriteMappings(Newer, "WinPlayerBuildProgram", "some", "future", "layout");

			var found = LineNumberMappingsSearch.Find(
				Path.Combine(root, "MissingBackup"),
				new[] { Path.Combine(root, "WindowsPlayerBuildProgram"), Path.Combine(root, "WinPlayerBuildProgram") });

			Assert.AreEqual(newest, found);
		}

		[Test]
		public void Find_ShouldReturnNullWhenNothingIsThere()
		{
			Directory.CreateDirectory(Path.Combine(root, "Empty"));

			Assert.IsNull(LineNumberMappingsSearch.Find(null, new[] { Path.Combine(root, "Empty"), Path.Combine(root, "Missing") }));
		}

		[Test]
		public void GetBeeArtifactFolders_ShouldMatchByPrefixIgnoringCase()
		{
			foreach (var name in new[] { "WinPlayerBuildProgram", "windowsPlayerBuildProgram", "MacPlayerBuildProgram", "WebGL" })
				Directory.CreateDirectory(Path.Combine(root, "Library", "Bee", "artifacts", name));

			var folders = LineNumberMappingsSearch.GetBeeArtifactFolders(root, "Win").Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal);

			CollectionAssert.AreEqual(new[] { "WinPlayerBuildProgram", "windowsPlayerBuildProgram" }, folders);
		}

		[Test]
		public void GetBeeArtifactFolders_ShouldBeEmptyWithoutALibrary()
		{
			CollectionAssert.IsEmpty(LineNumberMappingsSearch.GetBeeArtifactFolders(root, "Win"));
		}

		[Test]
		public void ResolveOverride_ShouldResolveAFileRelativeToTheProject()
		{
			var expected = WriteMappings(Older, "Mappings");

			Assert.AreEqual(expected, LineNumberMappingsSearch.ResolveOverride(Path.Combine("Mappings", LineNumberMappingsSearch.FileName), root));
		}

		[Test]
		public void ResolveOverride_ShouldAcceptAFolderHoldingTheFile()
		{
			var expected = WriteMappings(Older, "Mappings");

			Assert.AreEqual(expected, LineNumberMappingsSearch.ResolveOverride("Mappings", root));
		}

		[Test]
		public void ResolveOverride_ShouldAcceptAnAbsolutePath()
		{
			var expected = WriteMappings(Older, "Elsewhere");

			Assert.AreEqual(expected, LineNumberMappingsSearch.ResolveOverride(expected, Path.Combine(root, "Project")));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("Missing/LineNumberMappings.json")]
		[TestCase("Missing")]
		public void ResolveOverride_ShouldReturnNullWithoutAFile(string overridePath)
		{
			Assert.IsNull(LineNumberMappingsSearch.ResolveOverride(overridePath, root));
		}
	}
}
