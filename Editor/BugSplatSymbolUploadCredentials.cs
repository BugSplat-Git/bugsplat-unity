using System;
using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Keeps symbol upload credentials out of built players. Before a player build serializes the
/// BugSplatOptions asset, the OAuth2 Client ID and Client Secret are cached in SessionState and
/// blanked on the asset; they are restored after the build completes. If the build throws before
/// the post-build callback runs, the values are restored by the next editor update tick, the next
/// domain reload, or editor shutdown, whichever happens first.
/// </summary>
public class BugSplatSymbolUploadCredentials : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
	const string ClientIdEnvironmentVariable = "BUGSPLAT_CLIENT_ID";
	const string ClientSecretEnvironmentVariable = "BUGSPLAT_CLIENT_SECRET";
	const string ClientIdSessionKey = "BugSplat.SymbolUploadClientId";
	const string ClientSecretSessionKey = "BugSplat.SymbolUploadClientSecret";
	const string RestorePendingSessionKey = "BugSplat.SymbolUploadCredentialsRestorePending";

	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		var options = BuildPostprocessors.GetBugSplatOptions();
		if (options == null)
			return;

		SessionState.SetString(ClientIdSessionKey, options.SymbolUploadClientId ?? string.Empty);
		SessionState.SetString(ClientSecretSessionKey, options.SymbolUploadClientSecret ?? string.Empty);

		if (string.IsNullOrEmpty(options.SymbolUploadClientId) && string.IsNullOrEmpty(options.SymbolUploadClientSecret))
			return;

		SessionState.SetBool(RestorePendingSessionKey, true);

		EditorApplication.update -= RestoreOnEditorUpdate;
		EditorApplication.update += RestoreOnEditorUpdate;
		EditorApplication.quitting -= Restore;
		EditorApplication.quitting += Restore;

		options.SymbolUploadClientId = string.Empty;
		options.SymbolUploadClientSecret = string.Empty;
		EditorUtility.SetDirty(options);
		AssetDatabase.SaveAssetIfDirty(options);
	}

	public void OnPostprocessBuild(BuildReport report)
	{
		Restore();
	}

	/// <summary>
	/// Resolves the symbol upload Client ID. The BUGSPLAT_CLIENT_ID environment variable is the
	/// recommended source and takes precedence; the options asset and the value cached during
	/// build preprocessing are fallbacks.
	/// </summary>
	internal static string GetClientId(BugSplatOptions options)
	{
		var clientId = Environment.GetEnvironmentVariable(ClientIdEnvironmentVariable);
		if (string.IsNullOrEmpty(clientId))
			clientId = options.SymbolUploadClientId;

		if (string.IsNullOrEmpty(clientId))
			clientId = SessionState.GetString(ClientIdSessionKey, string.Empty);

		return clientId;
	}

	/// <summary>
	/// Resolves the symbol upload Client Secret. The BUGSPLAT_CLIENT_SECRET environment variable
	/// is the recommended source and takes precedence; the options asset and the value cached
	/// during build preprocessing are fallbacks.
	/// </summary>
	internal static string GetClientSecret(BugSplatOptions options)
	{
		var clientSecret = Environment.GetEnvironmentVariable(ClientSecretEnvironmentVariable);
		if (string.IsNullOrEmpty(clientSecret))
			clientSecret = options.SymbolUploadClientSecret;

		if (string.IsNullOrEmpty(clientSecret))
			clientSecret = SessionState.GetString(ClientSecretSessionKey, string.Empty);

		return clientSecret;
	}

	internal static bool EnvironmentHasCredentials =>
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ClientIdEnvironmentVariable))
		&& !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ClientSecretEnvironmentVariable));

	[InitializeOnLoadMethod]
	static void RestoreAfterDomainReload()
	{
		Restore();
	}

	static void RestoreOnEditorUpdate()
	{
		EditorApplication.update -= RestoreOnEditorUpdate;
		Restore();
	}

	static void Restore()
	{
		if (!SessionState.GetBool(RestorePendingSessionKey, false))
			return;

		var options = BuildPostprocessors.GetBugSplatOptions();
		if (options == null)
			return;

		options.SymbolUploadClientId = SessionState.GetString(ClientIdSessionKey, string.Empty);
		options.SymbolUploadClientSecret = SessionState.GetString(ClientSecretSessionKey, string.Empty);
		EditorUtility.SetDirty(options);
		AssetDatabase.SaveAssetIfDirty(options);
		SessionState.SetBool(RestorePendingSessionKey, false);
	}
}
