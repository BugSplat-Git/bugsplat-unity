using UnityEditor;
using UnityEngine;
using BugSplatUnity.Runtime.Client;

namespace BugSplatUnity.Editor
{
    public static class BugSplatMenu
    {
        private const string AssetPath = "Assets/BugSplat/Resources/BugSplatOptions.asset";

        [MenuItem("Tools/BugSplat/Options", priority = 100)]
        public static void OpenOptions()
        {
            var options = AssetDatabase.LoadAssetAtPath<BugSplatOptions>(AssetPath);
            if (options == null)
            {
                CreateOptionsAsset();
                options = AssetDatabase.LoadAssetAtPath<BugSplatOptions>(AssetPath);
            }

            Selection.activeObject = options;
            EditorGUIUtility.PingObject(options);
        }

        private static void CreateOptionsAsset()
        {
            var dir = System.IO.Path.GetDirectoryName(AssetPath);
            if (!AssetDatabase.IsValidFolder("Assets/BugSplat"))
            {
                AssetDatabase.CreateFolder("Assets", "BugSplat");
            }
            if (!AssetDatabase.IsValidFolder("Assets/BugSplat/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/BugSplat", "Resources");
            }

            var options = ScriptableObject.CreateInstance<BugSplatOptions>();
            AssetDatabase.CreateAsset(options, AssetPath);
            AssetDatabase.SaveAssets();
        }
    }
}
