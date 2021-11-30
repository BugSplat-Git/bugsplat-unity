using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEditor.Callbacks;
using UnityEngine;

public class BuildPostprocessors
{
    static readonly string packageName = "com.bugsplat.unity";
    static readonly string iOSFrameworkName = "Bugsplat.framework";
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
            case BuildTarget.StandaloneWindows64: WindowsPostprocessBuild(target, pathToBuiltProject, "x86_64"); break;
            case BuildTarget.StandaloneWindows: WindowsPostprocessBuild(target, pathToBuiltProject, "x86"); break;
            case BuildTarget.iOS: iOSPostprocessBuild(target, pathToBuiltProject); break;
            default: return;
        } 
    }

    private static void iOSPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        var bugsplatFrameworkRelativePath = Path.Combine("Frameworks", iOSFrameworkName);
        var bugsplatFrameworkOutputPath = Path.Combine(pathToBuiltProject, bugsplatFrameworkRelativePath);

        if (Directory.Exists(bugsplatFrameworkOutputPath)) 
        {
            UnityEngine.Debug.Log($"{iOSFrameworkName} already exists at {bugsplatFrameworkOutputPath}, skipping...");
            return;
        }

        var frameworkType = PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK ? "Device" : "Simulator";
        var bugsplatFrameworkSourcePath = Path.Combine("Packages", "com.bugsplat.unity", "Plugins", "iOS", frameworkType, iOSFrameworkName);

        if (!Directory.Exists(bugsplatFrameworkSourcePath))
        {
            UnityEngine.Debug.LogError($"{iOSFrameworkName} does not exist at {bugsplatFrameworkSourcePath}, please re-add {packageName} and try again...");
            return;
        }

        CopyAll(bugsplatFrameworkSourcePath, bugsplatFrameworkOutputPath);

        var pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var pbxProject = new PBXProject();
        
        if (!File.Exists(pbxProjectPath))
        {
            UnityEngine.Debug.LogError($"Xcode project does not exist at {pbxProjectPath}");
            return;
        }

        pbxProject.ReadFromString(File.ReadAllText(pbxProjectPath));

        var unityTargetGuid = pbxProject.GetUnityMainTargetGuid();
        pbxProject.AddFrameworkToProject(unityTargetGuid, iOSFrameworkName, false);
        pbxProject.SetBuildProperty(unityTargetGuid, "ENABLE_BITCODE", "NO");
        pbxProject.SetBuildProperty(unityTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "13.0");
        pbxProject.SetBuildProperty(unityTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
        pbxProject.AddBuildProperty(unityTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/");
        pbxProject.AddBuildProperty(unityTargetGuid, "OTHER_LDFLAGS", "-ObjC");

        var unityFrameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();
        pbxProject.AddFrameworkToProject(unityFrameworkTargetGuid, iOSFrameworkName, false);
        pbxProject.SetBuildProperty(unityFrameworkTargetGuid, "ENABLE_BITCODE", "NO");
        pbxProject.SetBuildProperty(unityFrameworkTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "13.0");
        pbxProject.SetBuildProperty(unityFrameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
        pbxProject.AddBuildProperty(unityFrameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/");

        var bugsplatFrameworkGuid = pbxProject.AddFile(bugsplatFrameworkOutputPath, bugsplatFrameworkOutputPath);
        pbxProject.AddFileToEmbedFrameworks(unityTargetGuid, bugsplatFrameworkGuid);
        
        pbxProject.WriteToFile(pbxProjectPath);
        
        // TODO BG Add code snippet to main (?)
    }

    private static void WindowsPostprocessBuild(BuildTarget target, string pathToBuiltProject, string platform)
    {
        var bugSplatDir = Path.Combine("Packages", "com.bugsplat.unity", "Editor");
        var pluginsDir = Path.Combine("Assets", "Plugins", platform);

        if (!Directory.Exists(pluginsDir))
        {
            UnityEngine.Debug.Log("Plugins directory doesn't exist, skipping SendPdbs...");
            return;
        }

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

    private static void CopyAll(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        var directories = Directory.GetDirectories(sourcePath, "*.*", SearchOption.AllDirectories);
        foreach (var directory in directories) {
            Directory.CreateDirectory(directory.Replace(sourcePath, destinationPath));
        }

        var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            File.Copy(file, file.Replace(sourcePath, destinationPath));
        }
    }
}