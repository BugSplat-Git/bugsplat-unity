using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.Editor
{
	/// <summary>
	/// Sets the machine-local symbol upload credentials for a BugSplat database. Credentials are
	/// per-database rather than per-project, so one entry serves every project using that database.
	/// </summary>
	public class BugSplatSymbolUploadCredentialsWindow : EditorWindow
	{
		string database = string.Empty;
		string clientId = string.Empty;
		string clientSecret = string.Empty;

		[MenuItem("BugSplat/Symbol Upload/Set Credentials...", false, 100)]
		static void Open()
		{
			var window = GetWindow<BugSplatSymbolUploadCredentialsWindow>(true, "BugSplat Symbol Upload Credentials", true);
			window.minSize = new Vector2(460, 260);
			window.database = BuildPostprocessors.GetBugSplatOptions()?.Database ?? string.Empty;

			if (!string.IsNullOrEmpty(window.database))
			{
				var existing = BugSplatSymbolUploadCredentials.Read(window.database);
				window.clientId = existing.clientId;
				window.clientSecret = existing.clientSecret;
			}

			window.ShowUtility();
		}

		[MenuItem("BugSplat/Symbol Upload/Clear Credentials", false, 101)]
		static void ClearCredentials()
		{
			var database = BuildPostprocessors.GetBugSplatOptions()?.Database;
			if (string.IsNullOrEmpty(database))
			{
				EditorUtility.DisplayDialog("BugSplat", "No BugSplatOptions asset with a database was found in this project.", "OK");
				return;
			}

			if (!EditorUtility.DisplayDialog("BugSplat", $"Delete the stored symbol upload credentials for '{database}'?", "Delete", "Cancel"))
			{
				return;
			}

			var message = BugSplatSymbolUploadCredentials.Clear(database)
				? $"Deleted the credentials for '{database}'."
				: $"No stored credentials were found for '{database}'.";
			EditorUtility.DisplayDialog("BugSplat", message, "OK");
		}

		[MenuItem("BugSplat/Symbol Upload/Check Credentials", false, 120)]
		static void CheckCredentials()
		{
			var database = BuildPostprocessors.GetBugSplatOptions()?.Database;
			if (string.IsNullOrEmpty(database))
			{
				EditorUtility.DisplayDialog("BugSplat", "No BugSplatOptions asset with a database was found in this project.", "OK");
				return;
			}

			var resolved = BugSplatSymbolUploadCredentials.TryResolve(database, out _, out _);
			var source = System.Environment.GetEnvironmentVariable(BugSplatSymbolUploadCredentials.ClientIdEnvironmentVariable) != null
				? "the environment"
				: BugSplatSymbolUploadCredentials.GetCredentialsPath(database);

			EditorUtility.DisplayDialog(
				"BugSplat",
				resolved
					? $"Symbol upload credentials for '{database}' resolve from {source}."
					: $"No symbol upload credentials found for '{database}'.\n\nSet {BugSplatSymbolUploadCredentials.ClientIdEnvironmentVariable} and {BugSplatSymbolUploadCredentials.ClientSecretEnvironmentVariable}, or use BugSplat > Symbol Upload > Set Credentials.",
				"OK");
		}

		void OnGUI()
		{
			EditorGUILayout.HelpBox(
				"Credentials are stored per database in your home directory, outside this project — " +
				"never in the project or in a build. Generate them on BugSplat's Integrations page.",
				MessageType.Info);

			EditorGUILayout.Space();
			database = EditorGUILayout.TextField("Database", database);
			clientId = EditorGUILayout.TextField("Client ID", clientId);
			clientSecret = EditorGUILayout.PasswordField("Client Secret", clientSecret);

			EditorGUILayout.Space();
			if (!string.IsNullOrWhiteSpace(database))
			{
				EditorGUILayout.LabelField("Saves to", BugSplatSymbolUploadCredentials.GetCredentialsPath(database), EditorStyles.wordWrappedMiniLabel);
			}

			GUILayout.FlexibleSpace();

			using (new EditorGUI.DisabledScope(
				string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)))
			{
				if (GUILayout.Button("Save"))
				{
					BugSplatSymbolUploadCredentials.Write(database.Trim(), clientId.Trim(), clientSecret.Trim());
					Debug.Log($"BugSplat: saved symbol upload credentials for '{database.Trim()}' to {BugSplatSymbolUploadCredentials.GetCredentialsPath(database.Trim())}");
					Close();
				}
			}
		}
	}
}
