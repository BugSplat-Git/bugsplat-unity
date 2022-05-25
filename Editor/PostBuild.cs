using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using BugSplatUnity.Runtime.Client;

public class BuildPostprocessors
{
    static string _platform;

    /// <summary>
    /// Upload Asset/Plugin symbol files to BugSplat. 
    /// We don't upload Unity symbol files because the build output only contains public symbol information.
    /// BugSplat is configured to use the Unity symbol server which has private symbols containing file, function, and line information.
    /// </summary>
    [PostProcessBuild(1)]
    public static async Task OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64: _platform = "x86_64"; break;
            case BuildTarget.StandaloneWindows: _platform = "x86"; break;
            default: return;
        }

        var projectDir = Path.GetDirectoryName(Application.dataPath);
        var pluginsDir = Path.Combine(Path.Combine(projectDir, "Assets", "Plugins"), _platform);

        if (!Directory.Exists(pluginsDir))
        {
            UnityEngine.Debug.Log("Plugins directory doesn't exist, skipping symbol uploads...");
            return;
        }

        await UploadSymbolFiles(pluginsDir);
    }

    static async Task UploadSymbolFiles(string pluginsDir)
    {
        var options = GetBugSplatOptions();

        if (options == null)
        {
            UnityEngine.Debug.LogWarning("No BugSplatOptions ScriptableObject found! Skipping symbol uploads...");
            return;
        }

        var database = options.Database;
        var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;
        var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
        var clientId = options.SymbolUploadClientId;
        var clientSecret = options.SymbolUploadClientSecret;

        if (string.IsNullOrEmpty(database))
        {
            UnityEngine.Debug.LogWarning("BugSplatOptions Database was not set! Skipping symbol uploads...");
            return;
        }

        if (string.IsNullOrEmpty(clientId))
        {
            UnityEngine.Debug.LogWarning("BugSplatOptions ClientID was not set! Skipping symbol uploads...");
            return;
        }

        if (string.IsNullOrEmpty(clientSecret))
        {
            UnityEngine.Debug.LogWarning("BugSplatOptions ClientSecret was not set! Skipping symbol uploads...");
            return;
        }

        UnityEngine.Debug.Log($"BugSplat Database: {database}");
        UnityEngine.Debug.Log($"BugSplat Application: ${application}");
        UnityEngine.Debug.Log($"BugSplat Version: ${version}");

        var fileExtensions = new List<string>() {
            ".dll",
            ".pdb"
        };
        var symbolFiles = Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories)
            .Select(file => new FileInfo(file))
            .Where(fileInfo => fileExtensions.Any(ext => ext.Equals(fileInfo.Extension)));

        foreach (var symbolFile in symbolFiles)
        {
            UnityEngine.Debug.Log($"BugSplat found symbol file: {symbolFile.FullName}");
        }

        UnityEngine.Debug.Log("About to upload symbol files to BugSplat...");

        try
        {
            using (var symbolUploader = SymbolUploader.CreateOAuth2SymbolUploader(clientId, clientSecret))
            {
                var response = await symbolUploader.UploadSymbolFiles(
                    database,
                    application,
                    version,
                    symbolFiles
                );
            }

            UnityEngine.Debug.Log("BugSplat symbol upload completed successfully!");
        }
        catch(Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
        }
    }

    static BugSplatOptions GetBugSplatOptions()
    {
        var guids = AssetDatabase.FindAssets("t:BugSplatOptions");

        if (guids.Length == 0)
        {
            return null;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<BugSplatOptions>(path);
    }
}