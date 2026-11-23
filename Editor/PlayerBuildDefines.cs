using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// The defines the player being built compiles with: the target's PlayerSettings defines plus a build
	/// script's BuildPlayerOptions.extraScriptingDefines, which never reach PlayerSettings.
	/// </summary>
	internal class PlayerBuildDefines : BuildPlayerProcessor
	{
		static string[] extra = Array.Empty<string>();

		public override int callbackOrder => 0;

		public override void PrepareForBuild(BuildPlayerContext buildPlayerContext) =>
			extra = buildPlayerContext.BuildPlayerOptions.extraScriptingDefines ?? Array.Empty<string>();

		internal static string For(NamedBuildTarget target) =>
			string.Join(";", new[] { PlayerSettings.GetScriptingDefineSymbols(target) }.Concat(extra));
	}
}
