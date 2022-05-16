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
using BugSplatDotNetStandard;
using System.Collections.Generic;
using System.Linq;
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
        BugSplatOptions options = GetBugSplatOptions();

        if (options == null)
        {
            UnityEngine.Debug.LogWarning("No BugSplatOptions ScriptableObject found! Plugin Symbol Files will not be sent.");
            return;
        }

        var fileExtensions = new List<string>() { ".dll", ".pdb" };
        var symbolFiles = Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories)
            .Select(file => new FileInfo(file))
            .Where(fileInfo => fileExtensions.Any(ext => ext.Equals(fileInfo.Extension)));

        foreach(var symbolFile in symbolFiles)
        {
            UnityEngine.Debug.Log("About to upload symbol file: " + symbolFile.FullName);
        }

        UnityEngine.Debug.Log("Product Name: " + options.Application);
        UnityEngine.Debug.Log("Version: " + options.Version);

        using var symbolUploader = SymbolUploader.CreateSymbolUploader(options.Email, options.Key);
        var response = await symbolUploader.UploadSymbolFiles(
            options.Database,
            options.Application,
            options.Version,
            symbolFiles
        );
    }

    static BugSplatOptions GetBugSplatOptions()
    {
        string[] guids;

        guids = AssetDatabase.FindAssets("t:BugSplatOptions");

        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<BugSplatOptions>(path);
    }
}