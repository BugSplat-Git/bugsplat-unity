using BugSplatUnity.Runtime.Client;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.Editor
{
    [CustomEditor(typeof(BugSplatOptions))]
    public class BugSplatOptionsEditor : UnityEditor.Editor
    {
        private const string logoPath = "Packages/com.bugsplat.unity/Editor/EditorResources/logo.png";
        private const string integrationsURLFormat = "https://app.bugsplat.com/v2/settings/database/integrations{0}";
        private const string integrationsText = "<color=#040404>A Client ID and Client Secret pair can be generated on the BugSplat <a>Integrations</a> page.</color>";
        private const string integrationsQueryString = "?database={0}";
        private const string emptyDatabaseErrorMessage = "Database cannot be null or empty!";
        private const string hangDialogConflictFormat = "{0} fatal hang reports are set not to auto-submit, but {0} crash reports still are, so fatal hangs will keep uploading without asking. Turn off auto-submit for {0} crash reports as well to be prompted.";
        private const string credentialsInfoMessage = "Symbol upload credentials are not stored here — they would end up in version control and in your builds. Set them per database via BugSplat > Symbol Upload > Set Credentials, or with the SYMBOL_UPLOAD_CLIENT_ID and SYMBOL_UPLOAD_CLIENT_SECRET environment variables.";

        private const int integrationsPaddingTop = 5;
        private const int integrationsPaddingBottom = 5;

        private void DrawSymbolUploadCredentialsSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(credentialsInfoMessage, MessageType.Info);

            // Read through the serialized property rather than the target: this runs before
            // ApplyModifiedProperties, so the target still holds the pre-edit value.
            var database = serializedObject.FindProperty(nameof(BugSplatOptions.Database))?.stringValue;
            var queryString = !string.IsNullOrEmpty(database)
                ? string.Format(integrationsQueryString, database)
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

            // Make the generated link read as clickable.
            var rect = GUILayoutUtility.GetLastRect();
            rect.width = style.CalcSize(new GUIContent(integrationsText)).x;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            EditorGUILayout.Space();
        }

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

                // The credentials notice belongs next to the symbol upload toggles it is about,
                // not stranded at the bottom of the asset. Windows is the first platform section,
                // so emit it just before that section opens.
                if (iterator.name == nameof(BugSplatOptions.UseNativeCrashReportingForWindows))
                {
                    DrawSymbolUploadCredentialsSection();
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty(iterator.name), true);
            }

            serializedObject.ApplyModifiedProperties();

            if (string.IsNullOrEmpty(options.Database))
            {
                EditorGUILayout.HelpBox(emptyDatabaseErrorMessage, MessageType.Error);
            }

            // Caught here as well as at runtime: this pair is configured in the Inspector, so the
            // Inspector is where noticing it costs nothing. The runtime warning only surfaces on a
            // device, after a build, which is a slow way to learn the option did nothing.
            if (!options.IosAutoSubmitFatalHangReport && options.IosAutoSubmitCrashReport)
            {
                EditorGUILayout.HelpBox(string.Format(hangDialogConflictFormat, "iOS"), MessageType.Warning);
            }

            if (!options.MacAutoSubmitFatalHangReport && options.MacAutoSubmitCrashReport)
            {
                EditorGUILayout.HelpBox(string.Format(hangDialogConflictFormat, "macOS"), MessageType.Warning);
            }

        }
    }
}
