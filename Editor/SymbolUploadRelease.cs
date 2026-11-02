using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("BugSplat.Unity.EditorTests")]

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// The symbol-upload release the package downloads, pinned by version and SHA-256 so a
	/// replaced or corrupted binary is never executed. Bump all of these together.
	/// </summary>
	internal static class SymbolUploadRelease
	{
		internal const string Version = "v11.1.0";

		internal const string Windows = "symbol-upload-windows.exe";
		internal const string MacOS = "symbol-upload-macos";
		internal const string MacOSIntel = "symbol-upload-macos-intel";
		internal const string Linux = "symbol-upload-linux";

		internal static string GetDownloadUrl(string variant) =>
			$"https://github.com/BugSplat-Git/symbol-upload/releases/download/{Version}/{variant}";

		internal static string GetSha256(string variant) =>
			variant switch
			{
				Windows => "febf5b79d174435624e8043b477b691dad281cc0d2a003256930ec75414a61a1",
				MacOS => "56fdae555f9161be4a0647b2573a0bc15a8a2dc3f1fea68e1d228bbda7f4feca",
				MacOSIntel => "1763cd77f7f7fe5ccea081fde954cabac6e8b7424cee2939583a8e35cea95ac6",
				Linux => "eebd832e27e6f934d086ce2e13d38c8e2f5be140869ca23c3630c5c2f83f4e81",
				_ => throw new ArgumentException($"BugSplat. Unknown symbol-upload variant {variant}", nameof(variant))
			};

		internal static string ComputeSha256(string path)
		{
			using (var sha256 = SHA256.Create())
			using (var stream = File.OpenRead(path))
			{
				return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
			}
		}

		internal static bool IsVerified(string path, string variant)
		{
			if (!File.Exists(path)) return false;

			try
			{
				return string.Equals(ComputeSha256(path), GetSha256(variant), StringComparison.OrdinalIgnoreCase);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				return false;
			}
		}
	}
}
