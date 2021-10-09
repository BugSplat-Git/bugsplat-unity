using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BugSplatConfigurationOptions))]
public class BugSplatConfigurationEditor : Editor
{
	public override void OnInspectorGUI()
	{
		var texture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/com.bugsplat.unity/Editor/EditorResources/logo.png", typeof(Texture2D));
		if (texture != null)
		{
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label(texture);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		base.OnInspectorGUI();

		var t = (target as BugSplatConfigurationOptions);

		var errorMessage = string.Empty;
		if (string.IsNullOrEmpty(t.Database))
		{
			errorMessage = "Database cannot be null or empty!";
		} 
		else if (string.IsNullOrEmpty(t.Application))
		{
			errorMessage = "Application cannot be null or empty!";
		} 
		else if (string.IsNullOrEmpty(t.Version))
		{
			errorMessage = "Version cannot be null or empty!";
		}

		if (!string.IsNullOrEmpty(errorMessage))
		{
			EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
		}
	}
}
