using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;

public class BuildPostprocessor
{
    const string UNITY_ASSEMBLY_FILENAME = "UnityEngine.dll";
    const string UNITY_ASSEMBLY_MDB_FILENAME = UNITY_ASSEMBLY_FILENAME + ".mdb";
    const string MAIN_ASSEMBLY_FILENAME = "Assembly-CSharp.dll";
    const string MAIN_ASSEMBLY_MDB_NAME_FILENAME = MAIN_ASSEMBLY_FILENAME + ".mdb";

    static string _platform;

    [PostProcessBuild(1)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64: _platform = "x86_64"; break;
            case BuildTarget.StandaloneWindows: _platform = "x86"; break;
            default: _platform = string.Empty; break;
        }

        if (!string.IsNullOrEmpty(_platform))
        {
            var builtProjectDir = Path.GetDirectoryName(pathToBuiltProject);
            var projectDir = Path.GetDirectoryName(Application.dataPath);
            var tempSymbolsDir = Path.Combine(builtProjectDir, "Symbols");
            var bugSplatDir = Path.Combine(projectDir, @"..\..\bin");

            if (!Directory.Exists(tempSymbolsDir))
            {
                Directory.CreateDirectory(tempSymbolsDir);
            }

            CopyPluginSymbols(projectDir, tempSymbolsDir);
            CopyRuntimeSymbols(builtProjectDir, tempSymbolsDir);
            RunSendPdbs(builtProjectDir, bugSplatDir);

            Directory.Delete(tempSymbolsDir, true);
        }
    }

    static void CopyPluginSymbols(string projectDir, string symbolsDir)
    {
        var pluginsDir = Path.Combine(Path.Combine(projectDir, @"Assets\Plugins"), _platform);

        if (!Directory.Exists(pluginsDir))
        {
            UnityEngine.Debug.Log($"Directory {pluginsDir} does not exist, skipping...");
            return;
        }

        var pluginFilePaths = new List<string>();
        pluginFilePaths.AddRange(Directory.GetFiles(pluginsDir, "*.pdb", SearchOption.AllDirectories));
        pluginFilePaths.AddRange(Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories));

        foreach (var pluginFilePath in pluginFilePaths)
        {
            var fileName = Path.GetFileName(pluginFilePath);
            var dstFilePath = Path.Combine(symbolsDir, fileName);

            File.Copy(pluginFilePath, dstFilePath, true);
        }
    }

    static void CopyRuntimeSymbols(string builtProjectDir, string symbolsDir)
    {
        var dataDir = Path.Combine(builtProjectDir, string.Format("{0}_Data", Application.productName));
        var managedDllsDir = Path.Combine(dataDir, "Managed");
        // TODO BG all dlls? (except maybe system dlls?)
        // TODO BG mono dlls and pdbs!
        var mainAssembly = Path.Combine(managedDllsDir, MAIN_ASSEMBLY_FILENAME);
        var unityAssembly = Path.Combine(managedDllsDir, UNITY_ASSEMBLY_FILENAME);
        var unityMdb = Path.Combine(managedDllsDir, UNITY_ASSEMBLY_MDB_FILENAME);

        File.Copy(mainAssembly, Path.Combine(symbolsDir, MAIN_ASSEMBLY_FILENAME), true);
        File.Copy(unityAssembly, Path.Combine(symbolsDir, UNITY_ASSEMBLY_FILENAME), true);

        if (File.Exists(unityMdb))
        {
            File.Copy(unityMdb, Path.Combine(symbolsDir, UNITY_ASSEMBLY_MDB_FILENAME), true);
        }
    }

    static void RunSendPdbs(string builtProjectDir, string bugSplatDir)
    {
        var sendPdbsPath = Path.Combine(bugSplatDir, "SendPdbs.exe");
        var startInfo = new ProcessStartInfo(sendPdbsPath);
        startInfo.Arguments = "/u Fred"
            + " /p Flintstone"
            + " /b Fred"
            + " /a " + Application.productName
            + " /v " + Application.version
            + " /d \"" + builtProjectDir + "\""
            + " /f \"Symbols/*;*.exe;*.pdb";
        using (var process = Process.Start(startInfo))
        {
            process.WaitForExit();
        }
    }
}