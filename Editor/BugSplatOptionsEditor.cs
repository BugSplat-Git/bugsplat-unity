using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BugSplatOptions))]
public class BugSplatOptionsEditor : Editor
{
    private const string logoPath = "Packages/com.bugsplat.unity/Editor/EditorResources/logo.png";
    private const string integrationsURLFormat = "https://app.bugsplat.com/v2/settings/database/integrations{0}";
    private const string integrationsText = "<color=#040404>To generate a ClientID and Secret, navigate to your <a>Integrations</a>.</color>";
    private const string integrationsQueryString = "?database={0}";
    private const string emptyDatabaseErrorMessage = "Database cannot be null or empty!";

    private const int integrationsPaddingTop = 5;
    private const int integrationsPaddingBottom = 5;

    public override void OnInspectorGUI()
    {
        var options = (target as BugSplatOptions);
        var texture = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
        if (texture != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(texture);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        SerializedProperty iterator = serializedObject.GetIterator();

        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == nameof(options.ClientId))
            {
                var queryString = options.Database != string.Empty
                    ? string.Format(integrationsQueryString, options.Database)
                    : string.Empty;

                var integrationsURL = string.Format(integrationsURLFormat, queryString);
                GUIStyle style = new GUIStyle() { richText = true, wordWrap = true, };
                style.margin = new RectOffset(0, 0, integrationsPaddingTop, integrationsPaddingBottom);
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
