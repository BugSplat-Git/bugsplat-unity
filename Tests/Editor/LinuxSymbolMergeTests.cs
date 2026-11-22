using System;
using System.IO;
using System.Linq;
using BugSplatUnity.Editor;
using NUnit.Framework;

namespace BugSplatUnity.EditorTests
{
	public class LinuxSymbolMergeTests
	{
		const string Id = "8EFD535C2FA78CC83A30D990E34E4A560";
		string root;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "bugsplat-merge-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
		}

		[TearDown]
		public void TearDown() => Directory.Delete(root, true);

		void WriteSym(string relativePath, params string[] lines)
		{
			var path = Path.Combine(root, "dumped", relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, string.Join("\n", lines) + "\n");
		}

		[Test]
		public void MergeSplitDebugSymbols_ShouldGiveTheBinaryTheDebugFilesFunctionsUnderItsOwnModule()
		{
			WriteSym($"GameAssembly.so/{Id}/GameAssembly.so.sym",
				$"MODULE Linux x86_64 {Id} GameAssembly.so",
				"INFO CODE_ID 5C53FD8EA72FC88C3A30D990E34E4A56",
				"PUBLIC 1000 0 il2cpp_init",
				"STACK CFI INIT 1000 10 .cfa: $rsp 8 + .ra: .cfa -8 + ^");
			WriteSym($"GameAssembly/{Id}/GameAssembly.sym",
				$"MODULE Linux x86_64 {Id} GameAssembly.debug",
				"INFO CODE_ID 5C53FD8EA72FC88C3A30D990E34E4A56",
				"FILE 0 il2cppOutput/Assembly-CSharp.cpp",
				"FUNC 2000 20 0 SpikeRunner_Frame2_m0",
				"2000 20 110 0",
				"PUBLIC 1000 0 il2cpp_init");

			Assert.AreEqual(1, BuildPostprocessors.MergeSplitDebugSymbols(Path.Combine(root, "dumped"), Path.Combine(root, "merged")));

			var merged = File.ReadAllLines(Path.Combine(root, "merged", Id, "GameAssembly.so.sym"));
			CollectionAssert.AreEqual(new[]
			{
				$"MODULE Linux x86_64 {Id} GameAssembly.so",
				"INFO CODE_ID 5C53FD8EA72FC88C3A30D990E34E4A56",
				"FILE 0 il2cppOutput/Assembly-CSharp.cpp",
				"FUNC 2000 20 0 SpikeRunner_Frame2_m0",
				"2000 20 110 0",
				"PUBLIC 1000 0 il2cpp_init",
				"STACK CFI INIT 1000 10 .cfa: $rsp 8 + .ra: .cfa -8 + ^",
			}, merged);
		}

		[Test]
		public void MergeSplitDebugSymbols_ShouldKeepBinariesWithoutDebugFilesAndDropOrphanDebugFiles()
		{
			WriteSym("libbugsplat.so.1/A16E/libbugsplat.so.sym", "MODULE Linux x86_64 A16E libbugsplat.so.1", "PUBLIC 10 0 bugsplat_init");
			WriteSym("Orphan_s/B000/Orphan_s.sym", "MODULE Linux x86_64 B000 Orphan_s.debug", "PUBLIC 10 0 f");

			Assert.AreEqual(0, BuildPostprocessors.MergeSplitDebugSymbols(Path.Combine(root, "dumped"), Path.Combine(root, "merged")));

			var output = Directory.GetFiles(Path.Combine(root, "merged"), "*.sym", SearchOption.AllDirectories).Select(Path.GetFileName).ToArray();
			CollectionAssert.AreEquivalent(new[] { "libbugsplat.so.sym" }, output);
		}
	}
}
