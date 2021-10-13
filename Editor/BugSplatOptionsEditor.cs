using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BugSplatOptions))]
public class BugSplatOptionsEditor : Editor
{
	private const string logoPath = "Packages/com.bugsplat.unity/Editor/EditorResources/logo.png";

	private const string emptyDatabaseErrorMessage = "Database cannot be null or empty!";

	public override void OnInspectorGUI()
	{
		var texture = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
		if (texture != null)
		{
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label(texture);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		base.OnInspectorGUI();

		var options = (target as BugSplatOptions);

		if (string.IsNullOrEmpty(options.Database))
		{
			EditorGUILayout.HelpBox(emptyDatabaseErrorMessage, MessageType.Error);
		} 
	}
}
