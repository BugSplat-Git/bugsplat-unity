using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Warns when a Windows build starts that its symbols will not be uploaded because Copy PDB files is
	/// off, instead of only in the post-build log once the build is done.
	/// </summary>
	internal sealed class WindowsSymbolsBuildCheck : IPreprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
#if UNITY_EDITOR_WIN
			if (BuildPostprocessors.ShouldWarnCopyPdbFilesOff(
				report.summary.platform,
				BugSplatProjectOptions.Get(),
				UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles))
			{
				Debug.LogWarning(BuildPostprocessors.CopyPdbFilesOffMessage);
			}
#endif
		}
	}
}
