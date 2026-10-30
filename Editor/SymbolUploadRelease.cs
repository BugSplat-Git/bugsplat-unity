using System;
using System.IO;
using System.Security.Cryptography;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// The symbol-upload release the package downloads, pinned by version and SHA-256 so a
	/// replaced or corrupted binary is never executed. Bump all of these together.
	/// </summary>
	internal static class SymbolUploadRelease
	{
		internal const string Version = "v11.0.1";

		internal const string Windows = "symbol-upload-windows.exe";
		internal const string MacOS = "symbol-upload-macos";
		internal const string MacOSIntel = "symbol-upload-macos-intel";
		internal const string Linux = "symbol-upload-linux";

		internal static string GetDownloadUrl(string variant) =>
			$"https://github.com/BugSplat-Git/symbol-upload/releases/download/{Version}/{variant}";

		internal static string GetSha256(string variant) =>
			variant switch
			{
				Windows => "11b152a0ea616fb0e8f955aefd67dbcf3fa3a5de04e3e70e5470184dfbd630b3",
				MacOS => "963cf0a2cdeefbe79957232123ac57e64f60856523fef825f98b81e338909f63",
				MacOSIntel => "60f53c663ba5d3d8db63b16cd3e08d0d6fab08f496f84f23d497a3b725655d38",
				Linux => "27e85206ddac208fbc3dc3a5cd46b098801c9c03381a160740913971ec08cd1b",
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

		internal static bool IsVerified(string path, string variant) =>
			File.Exists(path) && string.Equals(ComputeSha256(path), GetSha256(variant), StringComparison.OrdinalIgnoreCase);
	}
}
