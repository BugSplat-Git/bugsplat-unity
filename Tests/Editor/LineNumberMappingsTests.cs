using BugSplatUnity.Editor;
using NUnit.Framework;

namespace BugSplatUnity.EditorTests
{
	public class LineNumberMappingsTests
	{
		const string PhaseName = "Upload dSYM files to BugSplat";

		const string Pbxproj =
			"// !$*UTF8*$!\n" +
			"{\n" +
			"\tobjects = {\n" +
			"\n" +
			"/* Begin PBXNativeTarget section */\n" +
			"\t\t1D6058900D05DD3D006BFB54 /* Unity-iPhone */ = {\n" +
			"\t\t\tisa = PBXNativeTarget;\n" +
			"\t\t\tbuildPhases = (\n" +
			"\t\t\t\t1D60588D0D05DD3D006BFB54 /* Resources */,\n" +
			"\t\t\t\tAAAAAAAAAAAAAAAAAAAAAAAA /* Upload dSYM files to BugSplat */,\n" +
			"\t\t\t\tBBBBBBBBBBBBBBBBBBBBBBBB /* Run Script */,\n" +
			"\t\t\t);\n" +
			"\t\t\tname = \"Unity-iPhone\";\n" +
			"\t\t};\n" +
			"/* End PBXNativeTarget section */\n" +
			"\n" +
			"/* Begin PBXShellScriptBuildPhase section */\n" +
			"\t\tAAAAAAAAAAAAAAAAAAAAAAAA /* Upload dSYM files to BugSplat */ = {\n" +
			"\t\t\tisa = PBXShellScriptBuildPhase;\n" +
			"\t\t\tbuildActionMask = 2147483647;\n" +
			"\t\t\tfiles = (\n" +
			"\t\t\t);\n" +
			"\t\t\tname = \"Upload dSYM files to BugSplat\";\n" +
			"\t\t\trunOnlyForDeploymentPostprocessing = 0;\n" +
			"\t\t\tshellPath = /bin/sh;\n" +
			"\t\t\tshellScript = \"upload --clientSecret \\\"secret\\\" --files \\\"LineNumberMappings.json\\\"\\nif [ -f x ]; then exit 0; }\";\n" +
			"\t\t};\n" +
			"\t\tBBBBBBBBBBBBBBBBBBBBBBBB /* Run Script */ = {\n" +
			"\t\t\tisa = PBXShellScriptBuildPhase;\n" +
			"\t\t\tbuildActionMask = 2147483647;\n" +
			"\t\t\tname = \"Run Script\";\n" +
			"\t\t\tshellPath = /bin/sh;\n" +
			"\t\t\tshellScript = \"echo hi\";\n" +
			"\t\t};\n" +
			"/* End PBXShellScriptBuildPhase section */\n" +
			"\t};\n" +
			"}\n";

		[Test]
		public void PlatformMappingNames_ShouldMatchTheBackendContract()
		{
			Assert.AreEqual("LineNumberMappings.json", BuildPostprocessors.WindowsLineNumberMappingsName);
			Assert.AreEqual("LineNumberMappings-macos.json", BuildPostprocessors.MacLineNumberMappingsName);
			Assert.AreEqual("LineNumberMappings-ios.json", BuildPostprocessors.IosLineNumberMappingsName);
			Assert.AreEqual("LineNumberMappings-android.json", BuildPostprocessors.AndroidLineNumberMappingsName);
		}

		[Test]
		public void RemoveShellScriptBuildPhasesNamed_ShouldRemoveThePhaseAndItsTargetReference()
		{
			var result = BuildPostprocessors.RemoveShellScriptBuildPhasesNamed(Pbxproj, PhaseName, out _);

			StringAssert.DoesNotContain("AAAAAAAAAAAAAAAAAAAAAAAA", result);
			StringAssert.DoesNotContain("--clientSecret", result);
			StringAssert.Contains("\t\t\t\t1D60588D0D05DD3D006BFB54 /* Resources */,\n\t\t\t\tBBBBBBBBBBBBBBBBBBBBBBBB /* Run Script */,\n", result);
			StringAssert.Contains("\t\tBBBBBBBBBBBBBBBBBBBBBBBB /* Run Script */ = {\n", result);
			StringAssert.Contains("shellScript = \"echo hi\";", result);
		}

		[Test]
		public void RemoveShellScriptBuildPhasesNamed_ShouldReturnTheRemovedPhase()
		{
			BuildPostprocessors.RemoveShellScriptBuildPhasesNamed(Pbxproj, PhaseName, out var removed);

			StringAssert.StartsWith("\t\tAAAAAAAAAAAAAAAAAAAAAAAA /* Upload dSYM files to BugSplat */ = {\n", removed);
			StringAssert.Contains("--clientSecret", removed);
			StringAssert.DoesNotContain("echo hi", removed);
		}

		[Test]
		public void RemoveShellScriptBuildPhasesNamed_ShouldLeaveAProjectWithoutThePhaseUnchanged()
		{
			var result = BuildPostprocessors.RemoveShellScriptBuildPhasesNamed(Pbxproj, "Some Other Phase", out var removed);

			Assert.AreEqual(Pbxproj, result);
			Assert.IsEmpty(removed);
		}

		[Test]
		public void RemoveShellScriptBuildPhasesNamed_ShouldPreserveCrlfLineEndings()
		{
			var crlf = Pbxproj.Replace("\n", "\r\n");

			var result = BuildPostprocessors.RemoveShellScriptBuildPhasesNamed(crlf, PhaseName, out _);

			Assert.AreEqual(
				BuildPostprocessors.RemoveShellScriptBuildPhasesNamed(Pbxproj, PhaseName, out _).Replace("\n", "\r\n"),
				result);
		}
	}
}
