using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BugSplatOptions))]
public class BugSplatOptionsEditor : Editor
{
    private const string logoPath = "Packages/com.bugsplat.unity/Editor/EditorResources/logo.png";
    private const string integrationsURLFormat = "https://app.bugsplat.com/v2/settings/database/integrations{0}";
    private const string integrationsText = "<color=#040404>A Client ID and Client Secret pair can be generated on the BugSplat <a>Integrations</a> page.</color>";
    private const string integrationsQueryString = "?database={0}";
    private const string emptyDatabaseErrorMessage = "Database cannot be null or empty!";

    private const int integrationsPaddingTop = 5;
    private const int integrationsPaddingBottom = 5;

    public override void OnInspectorGUI()
    {
        var options = target as BugSplatOptions;
        var texture = AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D)) as Texture2D;
        if (texture != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(texture);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        var iterator = serializedObject.GetIterator();
        var traverseChildren = true;
        while (iterator.NextVisible(traverseChildren))
        {
            traverseChildren = false;
            if (string.Equals(iterator.name, nameof(options.SymbolUploadClientId)))
            {
                var queryString = !string.IsNullOrEmpty(options.Database)
                    ? string.Format(integrationsQueryString, options.Database)
                    : string.Empty;
                var integrationsURL = string.Format(integrationsURLFormat, queryString);

                var style = new GUIStyle()
                {
                    richText = true,
                    wordWrap = true,
                    margin = new RectOffset(0, 0, integrationsPaddingTop, integrationsPaddingBottom)
                };

                if (GUILayout.Button(integrationsText, style))
                {
                    Application.OpenURL(integrationsURL);
                }

                var rect = GUILayoutUtility.GetLastRect();
                rect.width = style.CalcSize(new GUIContent(integrationsText)).x;
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }

            var property = serializedObject.FindProperty(iterator.name);
            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();

        if (string.IsNullOrEmpty(options.Database))
        {
            EditorGUILayout.HelpBox(emptyDatabaseErrorMessage, MessageType.Error);
        }
    }
}
