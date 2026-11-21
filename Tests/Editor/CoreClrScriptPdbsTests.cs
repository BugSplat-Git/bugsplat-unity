using System;
using System.IO;
using BugSplatUnity.Editor;
using NUnit.Framework;

namespace BugSplatUnity.EditorTests
{
	public class CoreClrScriptPdbsTests
	{
		string buildDir;
		string editorAssembly;

		[SetUp]
		public void SetUp()
		{
			buildDir = Path.Combine(Path.GetTempPath(), "BugSplatCoreClrPdbs-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(buildDir, "Managed"));
			Directory.CreateDirectory(Path.Combine(buildDir, "CoreCLR"));
			editorAssembly = typeof(CoreClrScriptPdbs).Assembly.Location;
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(buildDir))
				Directory.Delete(buildDir, true);
		}

		string CopyIntoManaged()
		{
			var copy = Path.Combine(buildDir, "Managed", Path.GetFileName(editorAssembly));
			File.Copy(editorAssembly, copy);
			return copy;
		}

		[Test]
		public void ReadCodeViewPdbPath_ShouldNameTheCompilersPdb()
		{
			var pdb = CoreClrScriptPdbs.ReadCodeViewPdbPath(editorAssembly);

			Assert.That(pdb, Does.EndWith("BugSplat.Unity.Editor.pdb"));
		}

		[Test]
		public void ReadCodeViewPdbPath_ShouldReturnNullForAFileThatIsNotAnImage()
		{
			var notAnImage = Path.Combine(buildDir, "Managed", "NotAnImage.dll");
			File.WriteAllText(notAnImage, "not a PE image");

			Assert.IsNull(CoreClrScriptPdbs.ReadCodeViewPdbPath(notAnImage));
		}

		[Test]
		public void Find_ShouldReturnThePdbOfAnAssemblyShippedWithoutOne()
		{
			CopyIntoManaged();
			var expected = Path.GetFullPath(CoreClrScriptPdbs.ReadCodeViewPdbPath(editorAssembly));
			Assume.That(File.Exists(expected), "the editor assembly's PDB is where its debug directory says");

			CollectionAssert.AreEqual(new[] { expected }, CoreClrScriptPdbs.Find(buildDir));
		}

		[Test]
		public void Find_ShouldSkipAnAssemblyWhosePdbShipsBesideIt()
		{
			var copy = CopyIntoManaged();
			File.WriteAllText(Path.ChangeExtension(copy, ".pdb"), "");

			CollectionAssert.IsEmpty(CoreClrScriptPdbs.Find(buildDir));
		}

		[Test]
		public void Find_ShouldReturnNothingForAPlayerWithoutCoreClr()
		{
			CopyIntoManaged();
			Directory.Delete(Path.Combine(buildDir, "CoreCLR"));

			Assert.IsFalse(CoreClrScriptPdbs.IsCoreClrBuild(buildDir));
			CollectionAssert.IsEmpty(CoreClrScriptPdbs.Find(buildDir));
		}
	}
}
