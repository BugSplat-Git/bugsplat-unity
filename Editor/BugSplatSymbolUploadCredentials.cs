using System;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Resolves symbol upload credentials, which are per-database and machine-local.
///
/// They are never stored in the project: an asset carrying them ends up in version control and in
/// shipped player builds, which is what this replaces. Instead they live in the user's home
/// directory, one file per BugSplat database, in the shell format the symbol-upload CLI already
/// understands so the generated Xcode build phase can source the same file directly.
/// </summary>
public static class BugSplatSymbolUploadCredentials
{
	// The names the symbol-upload CLI reads, so nothing has to be remapped on the way down.
	public const string ClientIdEnvironmentVariable = "SYMBOL_UPLOAD_CLIENT_ID";
	public const string ClientSecretEnvironmentVariable = "SYMBOL_UPLOAD_CLIENT_SECRET";

	const string CredentialsDirectoryName = ".bugsplat";
	const string CredentialsSubdirectoryName = "credentials";

	/// <summary>
	/// Absolute path to the credentials file for a database. Shared with the generated Xcode build
	/// phase, which resolves the same path against $HOME.
	/// </summary>
	public static string GetCredentialsPath(string database)
	{
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		return Path.Combine(home, CredentialsDirectoryName, CredentialsSubdirectoryName, $"{SanitizeDatabase(database)}.sh");
	}

	/// <summary>
	/// The path the Xcode build phase uses, expressed relative to $HOME so the generated script
	/// carries no absolute path from the machine that ran the Unity build.
	/// </summary>
	public static string GetCredentialsPathRelativeToHome(string database)
		=> $"{CredentialsDirectoryName}/{CredentialsSubdirectoryName}/{SanitizeDatabase(database)}.sh";

	/// <summary>
	/// Resolves credentials for a database: the environment wins, then the credentials file.
	/// Returns false when neither supplies both values.
	/// </summary>
	public static bool TryResolve(string database, out string clientId, out string clientSecret)
	{
		clientId = Environment.GetEnvironmentVariable(ClientIdEnvironmentVariable);
		clientSecret = Environment.GetEnvironmentVariable(ClientSecretEnvironmentVariable);

		if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
		{
			return true;
		}

		var stored = Read(database);
		if (string.IsNullOrEmpty(clientId))
		{
			clientId = stored.clientId;
		}

		if (string.IsNullOrEmpty(clientSecret))
		{
			clientSecret = stored.clientSecret;
		}

		return !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);
	}

	public static bool Exists(string database) => File.Exists(GetCredentialsPath(database));

	public static (string clientId, string clientSecret) Read(string database)
	{
		var path = GetCredentialsPath(database);
		if (!File.Exists(path))
		{
			return (string.Empty, string.Empty);
		}

		string id = string.Empty, secret = string.Empty;
		foreach (var line in File.ReadAllLines(path))
		{
			var match = Regex.Match(line.Trim(), @"^export\s+(\w+)='(.*)'$");
			if (!match.Success)
			{
				continue;
			}

			// Undo the POSIX single-quote escaping applied by Write.
			var value = match.Groups[2].Value.Replace("'\\''", "'");
			if (match.Groups[1].Value == ClientIdEnvironmentVariable)
			{
				id = value;
			}
			else if (match.Groups[1].Value == ClientSecretEnvironmentVariable)
			{
				secret = value;
			}
		}

		return (id, secret);
	}

	public static void Write(string database, string clientId, string clientSecret)
	{
		var path = GetCredentialsPath(database);
		Directory.CreateDirectory(Path.GetDirectoryName(path));

		File.WriteAllText(
			path,
			$"# BugSplat symbol upload credentials for the '{database}' database.\n" +
			"# Machine-local and not part of any project. Delete this file to revoke it here.\n" +
			$"export {ClientIdEnvironmentVariable}='{EscapeShellSingleQuoted(clientId)}'\n" +
			$"export {ClientSecretEnvironmentVariable}='{EscapeShellSingleQuoted(clientSecret)}'\n");

		RestrictToOwner(path);
	}

	public static bool Clear(string database)
	{
		var path = GetCredentialsPath(database);
		if (!File.Exists(path))
		{
			return false;
		}

		File.Delete(path);
		return true;
	}

	// Database names are subdomains of bugsplat.com, so this should never alter a real one - it is
	// here so a malformed value cannot escape the credentials directory.
	static string SanitizeDatabase(string database)
	{
		if (string.IsNullOrWhiteSpace(database))
		{
			return "unknown";
		}

		return Regex.Replace(database.Trim(), @"[^A-Za-z0-9_.-]", "_");
	}

	static string EscapeShellSingleQuoted(string value) => (value ?? string.Empty).Replace("'", "'\\''");

	static void RestrictToOwner(string path)
	{
		if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX)
		{
			return;
		}

		try
		{
			var chmod = new System.Diagnostics.ProcessStartInfo("/bin/chmod", $"600 \"{path}\"")
			{
				UseShellExecute = false,
				CreateNoWindow = true
			};
			System.Diagnostics.Process.Start(chmod)?.WaitForExit();
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning($"BugSplat: could not restrict permissions on {path}: {ex.Message}");
		}
	}
}
