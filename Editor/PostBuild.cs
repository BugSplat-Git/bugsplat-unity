using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class BuildPostprocessors
{
    static string _platform;

    /// <summary>
    /// Upload Asset/Plugin symbol files to BugSplat. 
    /// We don't upload Unity symbol files because the build output only contains public symbol information.
    /// BugSplat is configured to use the Unity symbol server which has private symbols containing file, function, and line information.
    /// </summary>
    [PostProcessBuild(1)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64: _platform = "x86_64"; break;
            case BuildTarget.StandaloneWindows: _platform = "x86"; break;
            default: return;
        }

        var projectDir = Path.GetDirectoryName(Application.dataPath);
        var bugSplatDir = Path.Combine(projectDir, "Packages", "com.bugsplat.unity", "Editor");
        var pluginsDir = Path.Combine(Path.Combine(projectDir, @"Assets\Plugins"), _platform);

        if (!Directory.Exists(pluginsDir))
        {
            UnityEngine.Debug.Log("Plugins directory doesn't exist, skipping SendPdbs...");
            return;
        }

        RunSendPdbs(pluginsDir, bugSplatDir);
    }

    static void RunSendPdbs(string pluginsDir, string bugSplatDir)
    {
        var sendPdbsPath = Path.Combine(bugSplatDir, "SendPdbs.exe");
        var startInfo = new ProcessStartInfo(sendPdbsPath);
        startInfo.Arguments = "/u fred@bugsplat.com"
            + " /p Flintstone"
            + " /b Fred"
            + " /a " + Application.productName
            + " /v " + Application.version
            + " /d \"" + pluginsDir + "\""
            + " /s";

        using (var process = Process.Start(startInfo))
        {
            process.WaitForExit();
        }
    }
}