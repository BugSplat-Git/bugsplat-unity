#if UNITY_EDITOR_WIN
using BugSplatUnity.Editor;
using NUnit.Framework;

namespace BugSplatUnity.EditorTests
{
	public class WerRegistrationTests
	{
		[TestCase(@"C:\Program Files\My Game\BugSplatWer.dll")]
		[TestCase(@"C:\Games\Tom & Jerry\BugSplatWer.dll")]
		[TestCase(@"C:\Games\Caret^Game (x64)!\BugSplatWer.dll")]
		public void IsSafeForElevatedShell_ShouldAcceptOrdinaryPaths(string path)
		{
			Assert.IsTrue(WerRegistration.IsSafeForElevatedShell(path));
		}

		[TestCase(@"C:\Games\100%Fun%\BugSplatWer.dll")]
		[TestCase(@"C:\Games\a"" & calc & ""\BugSplatWer.dll")]
		[TestCase("C:\\Games\\line\nbreak\\BugSplatWer.dll")]
		public void IsSafeForElevatedShell_ShouldRejectWhatCmdWouldInterpret(string path)
		{
			Assert.IsFalse(WerRegistration.IsSafeForElevatedShell(path));
		}
	}
}
#endif
