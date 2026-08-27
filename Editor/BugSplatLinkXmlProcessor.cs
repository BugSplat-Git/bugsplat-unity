using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Hands Runtime/link.xml to UnityLinker.
	///
	/// UnityLinker only globs Assets/**/link.xml, so a link.xml shipped inside a UPM package is
	/// never read and its preservations are silently lost. Naming the file from this callback is
	/// the supported way for a package to contribute one.
	/// </summary>
	internal class BugSplatLinkXmlProcessor : IUnityLinkerProcessor
	{
		public int callbackOrder => 0;

		public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
		{
			var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BugSplatLinkXmlProcessor).Assembly);
			if (package != null)
			{
				var linkXml = Path.Combine(package.resolvedPath, "Runtime", "link.xml");
				if (!File.Exists(linkXml))
				{
					Debug.LogWarning($"BugSplat warning: {linkXml} is missing, managed stripping may leave crash report responses empty");
					return null;
				}

				return linkXml;
			}

			// FindForAssembly can return null even for package installs, so try the package root
			// directly before concluding the sources were copied under Assets/ (where UnityLinker
			// already reads link.xml).
			var fallbackLinkXml = Path.GetFullPath(Path.Combine("Packages", "com.bugsplat.unity", "Runtime", "link.xml"));
			return File.Exists(fallbackLinkXml) ? fallbackLinkXml : null;
		}
	}
}
