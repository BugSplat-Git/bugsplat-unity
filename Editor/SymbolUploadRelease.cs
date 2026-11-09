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
		internal const string Version = "v11.2.0";

		internal const string Windows = "symbol-upload-windows.exe";
		internal const string MacOS = "symbol-upload-macos";
		internal const string MacOSIntel = "symbol-upload-macos-intel";
		internal const string Linux = "symbol-upload-linux";

		internal static string GetDownloadUrl(string variant) =>
			$"https://github.com/BugSplat-Git/symbol-upload/releases/download/{Version}/{variant}";

		internal static string GetSha256(string variant) =>
			variant switch
			{
				Windows => "410918a93cf6f2589e3ed0a6bc59369d7027971a0fc88f19c6a7d67be2158569",
				MacOS => "6df74617ea435b8e062e9779dc3a1f303dd4b010f5c6dea024c2a33583cb4531",
				MacOSIntel => "e9a11b0c78c8f067ecd06084ea808ac07bb10395f31dbe50ff2a902989d34396",
				Linux => "281dcfa6a1a16157cbe080eb862456bb756b1588cb0f4ec998289f477c33c6cf",
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
