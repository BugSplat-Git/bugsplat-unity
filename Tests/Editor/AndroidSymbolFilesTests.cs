using System.Collections.Generic;
using System.IO;
using BugSplatUnity.Editor;
using NUnit.Framework;

namespace BugSplatUnity.EditorTests
{
	public class AndroidSymbolFilesTests
	{
		[TestCase("libil2cpp.so.dbg", "libil2cpp.so", 0)]
		[TestCase("libil2cpp.dbg.so", "libil2cpp.so", 0)]
		[TestCase("libil2cpp.so", "libil2cpp.so", 1)]
		[TestCase("libil2cpp.so.sym", "libil2cpp.so", 2)]
		[TestCase("libil2cpp.sym.so", "libil2cpp.so", 2)]
		[TestCase("libc++_shared.so.dbg", "libc++_shared.so", 0)]
		[TestCase("LIBUNITY.SO.DBG", "LIBUNITY.so", 0)]
		public void TryGetLibraryName_ShouldNameTheShippedLibrary(string fileName, string expectedLibrary, int expectedRank)
		{
			Assert.IsTrue(AndroidSymbolFiles.TryGetLibraryName(fileName, out var library, out var rank));
			Assert.AreEqual(expectedLibrary, library);
			Assert.AreEqual(expectedRank, rank);
		}

		[TestCase("LineNumberMappings.json")]
		[TestCase("libil2cpp.so.map")]
		[TestCase("readme.txt")]
		[TestCase(".so")]
		[TestCase(".so.dbg")]
		public void TryGetLibraryName_ShouldRejectFilesThatAreNotLibrarySymbols(string fileName)
		{
			Assert.IsFalse(AndroidSymbolFiles.TryGetLibraryName(fileName, out _, out _));
		}

		[Test]
		public void Select_ShouldRenameUnity6DebugFilesPerAbi()
		{
			// A Unity 6000.6 symbols.zip with Debug Symbols set to Full.
			var selected = AndroidSymbolFiles.Select(new[]
			{
				"arm64-v8a/libil2cpp.so.dbg",
				"arm64-v8a/libunity.so.dbg",
				"armeabi-v7a/libbugsplat.so.dbg",
			});

			CollectionAssert.AreEquivalent(new Dictionary<string, string>
			{
				["arm64-v8a/libil2cpp.so"] = "arm64-v8a/libil2cpp.so.dbg",
				["arm64-v8a/libunity.so"] = "arm64-v8a/libunity.so.dbg",
				["armeabi-v7a/libbugsplat.so"] = "armeabi-v7a/libbugsplat.so.dbg",
			}, selected);
		}

		[Test]
		public void Select_ShouldKeepLegacyExtensionFilesAsTheyAre()
		{
			var selected = AndroidSymbolFiles.Select(new[] { "arm64-v8a/libil2cpp.so", "arm64-v8a/libmain.so" });

			CollectionAssert.AreEquivalent(new Dictionary<string, string>
			{
				["arm64-v8a/libil2cpp.so"] = "arm64-v8a/libil2cpp.so",
				["arm64-v8a/libmain.so"] = "arm64-v8a/libmain.so",
			}, selected);
		}

		[TestCase("libil2cpp.so.dbg", "libil2cpp.so.sym")]
		[TestCase("libil2cpp.dbg.so", "libil2cpp.sym.so")]
		[TestCase("libil2cpp.so.dbg", "libil2cpp.so")]
		[TestCase("libil2cpp.so", "libil2cpp.so.sym")]
		public void Select_ShouldPreferTheFileWithMoreDebugInformation(string better, string worse)
		{
			foreach (var order in new[] { new[] { better, worse }, new[] { worse, better } })
			{
				var selected = AndroidSymbolFiles.Select(new[] { "arm64-v8a/" + order[0], "arm64-v8a/" + order[1] });

				Assert.AreEqual(1, selected.Count);
				Assert.AreEqual("arm64-v8a/" + better, selected["arm64-v8a/libil2cpp.so"]);
			}
		}

		[Test]
		public void Select_ShouldFallBackToSymbolTablesWhenThatIsAllThereIs()
		{
			var selected = AndroidSymbolFiles.Select(new[] { "arm64-v8a/libunity.so.sym" });

			Assert.AreEqual("arm64-v8a/libunity.so.sym", selected["arm64-v8a/libunity.so"]);
		}

		[Test]
		public void Select_ShouldAcceptWindowsSeparatorsAndFilesAtTheRoot()
		{
			var selected = AndroidSymbolFiles.Select(new[] { @"arm64-v8a\libmain.so.dbg", "libgame.so.dbg", "notes.txt" });

			CollectionAssert.AreEquivalent(new Dictionary<string, string>
			{
				["arm64-v8a/libmain.so"] = "arm64-v8a/libmain.so.dbg",
				["libgame.so"] = "libgame.so.dbg",
			}, selected);
		}

		[Test]
		public void Stage_ShouldMoveOneFilePerLibraryUnderItsShippedName()
		{
			var root = Path.Combine(Path.GetTempPath(), "bugsplat-android-symbols-" + Path.GetRandomFileName());
			var unzipped = Path.Combine(root, "symbols");
			var staging = Path.Combine(root, "symbols-bugsplat");
			try
			{
				Write(unzipped, "arm64-v8a/libil2cpp.so.dbg", "full");
				Write(unzipped, "arm64-v8a/libil2cpp.so.sym", "table");
				Write(unzipped, "arm64-v8a/libunity.so.dbg", "unity");
				Write(unzipped, "notes.txt", "ignored");
				Write(staging, "stale/libold.so", "left over from an earlier build");

				Assert.AreEqual(2, AndroidSymbolFiles.Stage(unzipped, staging));

				Assert.AreEqual("full", File.ReadAllText(Path.Combine(staging, "arm64-v8a", "libil2cpp.so")));
				Assert.AreEqual("unity", File.ReadAllText(Path.Combine(staging, "arm64-v8a", "libunity.so")));
				CollectionAssert.AreEquivalent(
					new[] { Path.Combine(staging, "arm64-v8a", "libil2cpp.so"), Path.Combine(staging, "arm64-v8a", "libunity.so") },
					Directory.GetFiles(staging, "*", SearchOption.AllDirectories));
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[Test]
		public void Stage_ShouldReturnZeroForAnArchiveWithNoLibraries()
		{
			var root = Path.Combine(Path.GetTempPath(), "bugsplat-android-symbols-" + Path.GetRandomFileName());
			var unzipped = Path.Combine(root, "symbols");
			try
			{
				Write(unzipped, "notes.txt", "ignored");

				Assert.AreEqual(0, AndroidSymbolFiles.Stage(unzipped, Path.Combine(root, "symbols-bugsplat")));
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		static void Write(string root, string relativePath, string contents)
		{
			var path = Path.Combine(root, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, contents);
		}
	}
}
