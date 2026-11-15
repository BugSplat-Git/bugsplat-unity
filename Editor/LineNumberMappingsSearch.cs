using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Finds the LineNumberMappings.json IL2CPP wrote for a build. Where Unity puts it has moved between
	/// versions, so this searches the folders it has lived in rather than a list of exact paths.
	/// </summary>
	internal static class LineNumberMappingsSearch
	{
		internal const string FileName = "LineNumberMappings.json";
		internal const string BackupFolderSuffix = "_BackUpThisFolder_ButDontShipItWithYourGame";

		/// <summary>The IL2CPP backup folder Unity writes next to a standalone player.</summary>
		internal static string GetBackupFolder(string pathToBuiltProject)
		{
			var trimmed = pathToBuiltProject.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var buildDir = Path.GetDirectoryName(trimmed);
			return buildDir == null ? null : Path.Combine(buildDir, Path.GetFileNameWithoutExtension(trimmed) + BackupFolderSuffix);
		}

		/// <summary>
		/// The folders under Library/Bee/artifacts whose names start with one of <paramref name="prefixes"/>,
		/// such as "Win" for both WinPlayerBuildProgram and the older WindowsPlayerBuildProgram.
		/// </summary>
		internal static IEnumerable<string> GetBeeArtifactFolders(string projectRoot, params string[] prefixes)
		{
			var artifacts = Path.Combine(projectRoot, "Library", "Bee", "artifacts");
			if (!Directory.Exists(artifacts))
				return Array.Empty<string>();

			try
			{
				return Directory.GetDirectories(artifacts)
					.Where(folder => prefixes.Any(prefix => Path.GetFileName(folder).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
					.ToArray();
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				return Array.Empty<string>();
			}
		}

		/// <summary>
		/// The current player's backup folder first, so another player's backup in the same output folder
		/// is never picked; then the newest match across <paramref name="beeFolders"/>, which is the one
		/// this build wrote when a folder also holds one from an older Unity version or another architecture.
		/// </summary>
		internal static string Find(string backupFolder, IEnumerable<string> beeFolders)
		{
			return FindNewest(backupFolder == null ? Array.Empty<string>() : new[] { backupFolder })
				?? FindNewest(beeFolders ?? Array.Empty<string>());
		}

		/// <summary>
		/// Resolves a user-supplied path, relative to <paramref name="projectRoot"/> unless absolute, to a
		/// file. A folder means the LineNumberMappings.json inside it. Returns null when there is no such file.
		/// </summary>
		internal static string ResolveOverride(string overridePath, string projectRoot)
		{
			if (string.IsNullOrWhiteSpace(overridePath))
				return null;

			string fullPath;
			try
			{
				fullPath = Path.GetFullPath(Path.Combine(projectRoot, overridePath.Trim()));
			}
			catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
			{
				return null;
			}

			if (Directory.Exists(fullPath))
				fullPath = Path.Combine(fullPath, FileName);

			return File.Exists(fullPath) ? fullPath : null;
		}

		static string FindNewest(IEnumerable<string> roots)
		{
			string newest = null;
			var newestTime = DateTime.MinValue;

			foreach (var root in roots)
			{
				foreach (var file in EnumerateMappings(root))
				{
					var time = File.GetLastWriteTimeUtc(file);
					if (newest == null || time > newestTime)
					{
						newest = file;
						newestTime = time;
					}
				}
			}

			return newest;
		}

		static IEnumerable<string> EnumerateMappings(string root)
		{
			if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
				return Array.Empty<string>();

			try
			{
				return Directory.GetFiles(root, FileName, SearchOption.AllDirectories);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				return Array.Empty<string>();
			}
		}
	}
}
