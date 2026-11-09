using System;
using System.Collections.Generic;
using System.IO;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Chooses which files in Unity's Android <c>*.symbols.zip</c> go to dump_syms, and the names they go under.
	/// </summary>
	/// <remarks>
	/// Unity 6 names the zipped files <c>libfoo.so.dbg</c> (Debug Symbols: Full) or <c>libfoo.so.sym</c> (Public),
	/// or plain <c>libfoo.so</c> when Debug Symbols' "Use legacy extensions" is on; older releases used
	/// <c>libfoo.dbg.so</c> and <c>libfoo.sym.so</c>. Most are <c>--only-keep-debug</c> files with no SONAME, so
	/// dump_syms names the module after the file, while a crash names the module <c>libfoo.so</c>: a symbol file
	/// for <c>libfoo.so.dbg</c> never matches. Each library is staged as <c>libfoo.so</c>, from the file with the
	/// most debug information when the archive has more than one for it.
	/// </remarks>
	internal static class AndroidSymbolFiles
	{
		// Lower rank wins. Full debug information beats an unmarked .so, which beats a symbol table only.
		static readonly (string Suffix, int Rank)[] Suffixes =
		{
			(".so.dbg", 0),
			(".dbg.so", 0),
			(".so.sym", 2),
			(".sym.so", 2),
			(".so", 1),
		};

		/// <summary>
		/// Maps a file name from the archive to the library it carries symbols for, or returns false when it carries none.
		/// </summary>
		internal static bool TryGetLibraryName(string fileName, out string libraryName, out int rank)
		{
			foreach (var (suffix, suffixRank) in Suffixes)
			{
				if (fileName.Length > suffix.Length && fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				{
					libraryName = fileName.Substring(0, fileName.Length - suffix.Length) + ".so";
					rank = suffixRank;
					return true;
				}
			}

			libraryName = null;
			rank = int.MaxValue;
			return false;
		}

		/// <summary>
		/// Picks one file per library per directory (ABI). Paths are relative to the unzipped archive, and the result
		/// maps each staged path, such as <c>arm64-v8a/libil2cpp.so</c>, to the path of the file to stage there.
		/// </summary>
		internal static IDictionary<string, string> Select(IEnumerable<string> relativePaths)
		{
			var chosen = new SortedDictionary<string, (string Source, int Rank)>(StringComparer.Ordinal);
			foreach (var path in relativePaths)
			{
				var normalized = path.Replace('\\', '/');
				var slash = normalized.LastIndexOf('/');
				if (!TryGetLibraryName(normalized.Substring(slash + 1), out var libraryName, out var rank))
					continue;

				var stagedPath = normalized.Substring(0, slash + 1) + libraryName;
				if (!chosen.TryGetValue(stagedPath, out var current) || rank < current.Rank)
					chosen[stagedPath] = (normalized, rank);
			}

			var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
			foreach (var entry in chosen)
				result[entry.Key] = entry.Value.Source;
			return result;
		}

		/// <summary>
		/// Moves the selected files from <paramref name="unzippedPath"/> into <paramref name="stagingPath"/> under
		/// their library names, replacing anything already there, and returns how many were staged.
		/// </summary>
		internal static int Stage(string unzippedPath, string stagingPath)
		{
			if (Directory.Exists(stagingPath))
				Directory.Delete(stagingPath, true);

			// GetFiles prefixes each result with unzippedPath as given. (Path.GetRelativePath isn't in .NET Framework.)
			var relativePaths = new List<string>();
			foreach (var file in Directory.GetFiles(unzippedPath, "*", SearchOption.AllDirectories))
				relativePaths.Add(file.Substring(unzippedPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

			var selected = Select(relativePaths);
			foreach (var entry in selected)
			{
				var destination = Path.Combine(stagingPath, entry.Key);
				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				File.Move(Path.Combine(unzippedPath, entry.Value), destination);
			}

			return selected.Count;
		}
	}
}
