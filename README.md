[![bugsplat-github-banner-basic-outline](https://user-images.githubusercontent.com/20464226/149019306-3186103c-5315-4dad-a499-4fd1df408475.png)](https://bugsplat.com)
<br/>
# <div align="center">BugSplat</div> 
### **<div align="center">Crash and error reporting built for busy developers.</div>**
<div align="center">
    <a href="https://twitter.com/BugSplatCo">
        <img alt="Follow @bugsplatco on Twitter" src="https://img.shields.io/twitter/follow/bugsplatco?label=Follow%20BugSplat&style=social">
    </a>
    <a href="https://discord.gg/K4KjjRV5ve">
        <img alt="Join BugSplat on Discord" src="https://img.shields.io/discord/664965194799251487?label=Join%20Discord&logo=Discord&style=social">
    </a>
    <br/>
    <a href="https://openupm.com/packages/com.bugsplat.unity/">
        <img alt="BugSplatUnity on OpenUPM" src="https://img.shields.io/npm/v/com.bugsplat.unity?label=openupm&registry_uri=https://package.openupm.com">
    </a>
</div>

## 👋 Introduction

BugSplat's `com.bugsplat.unity` package provides crash and exception reporting for Unity projects. BugSplat provides you with invaluable insight into the issues tripping up your users. Our Unity integration collects screenshots, log files, exceptions, and Windows minidumps so that you can fix bugs and deliver a better user experience.

Before you proceed, please make sure you have completed the following checklist:
* [Sign Up](https://app.bugsplat.com/v2/sign-up) as a new BugSplat user
* [Log In](https://app.bugsplat.com/cognito/login) to your account

## 🏗 Installation

BugSplat's `com.bugsplat.unity` package can be added to your project via [OpenUPM](https://openupm.com/packages/com.bugsplat.unity/) or a URL to our git [repository](https://github.com/BugSplat-Git/bugsplat-unity.git).

### OpenUPM
Information on installing OpenUPM can be found [here](https://openupm.com). After installing OpenUPM, run the following command to add BugSplat to your project.

```sh
openupm add com.bugsplat.unity
```

### Git
Information on adding a Unity package via a git URL can be found [here](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

```sh
https://github.com/BugSplat-Git/bugsplat-unity.git
```

## 🧑‍🏫 Sample

> [!TIP]
> BugSplat recommends building with the IL2CPP backend for the best crash reporting experience. For more information please see the [Player Settings](#-player-settings) section.

After installing `com.bugsplat.unity`, you can import a sample project to help you get started with BugSplat. Click here if you'd like to skip the sample project and get straight to the [usage](#-usage) instructions.

To import the sample, click the caret next to **Samples** to reveal the **my-unity-crasher** sample. Click **Import** to add the sample to your project.

![Importing the Sample](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/b7a39388-eb76-413a-a92f-72fd39c9a7d6)


In the Project Assets browser, open the **Sample** scene from `Samples > BugSplat > Version > my-unity-crasher > Scenes`.

Next, select `Samples > BugSplat > Version > my-unity-crasher` to reveal the **BugSplatOptions** object. Click BugSplatOptions and replace the database value with your BugSplat database.

![Finding the Sample](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/ba9aa64a-1d85-45a8-b11f-565520c30bcf)

![Configuring BugSplat](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/a6250cea-a4da-44a8-b6cb-ff2467b0d978)

> [!NOTE]
> The sample's UI labels use TextMeshPro. If the button text appears blank, import TMP Essentials via **Window > TextMeshPro > Import TMP Essential Resources**. TextMeshPro can't render without its default font asset, which is imported per-project and can't be bundled in the sample.

Click **Play** and run a scenario from the **Crash Scenarios** menu to send an error report to BugSplat. Scenarios are grouped by the mechanism that captures them, and the menu is platform-aware — each platform lists only what it can capture. Native scenarios are disabled in the editor and need a built player; see the [sample README](https://github.com/BugSplat-Git/bugsplat-unity/blob/main/Samples~/my-unity-crasher/README.md) for the full scenario matrix. To view the error report, navigate to the BugSplat [Dashboard](https://app.bugsplat.com/v2/dashboard) and ensure you have selected the correct database.

![Running the Sample](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/4418b736-dc88-496a-ada6-a27ad19032f1)

Navigate to the [Crashes](https://app.bugsplat.com/v2/crashes) page, and click the value in the ID column to see the details of your report, including the call stack, log file, and screenshot of your app when the error occurred.

![BugSplat Crash Page](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/f108d7e9-ee90-4a09-a7b4-8a9b5d764942)

## 🧰 Player Settings

For best results, BugSplat recommends building with the `IL2CPP` backend. The `Mono` backend is supported, but has several limitations. With `IL2CPP`, BugSplat can capture fully symbolicated C# exception traces in production, as well as native crashes that contain call stacks mapped back to their original C# function names, file names, and line numbers.

To optimize your game for crash reporting, open `Player Settings` (`Edit > Player Settings`). Navigate to the `Configuration` section. For `Scripting Backend` choose `IL2CPP` and for `IL2CPP StackTrace Information` choose `Method Name, File Name, and Line Number`.

![Unity Player Settings](https://github.com/user-attachments/assets/ed459d7e-8580-4e8d-b6aa-386ecaa51a56)

## ⚙️ Configuration

BugSplat's Unity integration is flexible and can be used in various ways. The easiest way to get started is to attach the `BugSplatManager` MonoBehaviour to a GameObject.

![BugSplat Manager](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/ef5240a6-9676-43c6-a482-51216cb34401)

`BugSplatManager` needs to be initialized with a `BugSplatOptions` serialized object. A new instance of `BugSplatOptions` can be created through the Asset Create menu.

![BugSplat Create Options](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec402d1-4b8a-49cf-96e9-00d951717771)

Configure fields as appropriate. Note that if Application or Version are left empty, `BugSplat` will default these values to `Application.productName` and `Application.version`, respectively.

Exceptions thrown in the editor are not uploaded by default, so play mode errors never reach the database you ship with. Check **PostExceptionsInEditor** on the options asset (or set `bugsplat.PostExceptionsInEditor = true` in code) while you verify your integration.

![BugSplat Options](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/be7ee217-9170-48b4-b780-fcb47e221f77)

Finally, provide a valid `BugSplatOptions` to `BugSplatManager`. 

![BugSplat Manager Configured](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/67bed7b5-e2a9-4f52-b5bb-bdc8eebd35a0)

## ⌨️ Usage

If you're using `BugSplatOptions` and `BugSplatManager`, BugSplat automatically configures an `Application.logMessageReceived` handler that will post reports when it encounters a log message of type `Exception`. You can also extend your BugSplat integration and [customize report metadata](#adding-metadata), [report exceptions in try/catch blocks](#trycatch-reporting), [prevent repeated reports](#preventing-repeated-reports), and [upload windows minidumps](#-windows) from native crashes.

### Adding Metadata

First, find your instance of `BugSplat`. The following is an example of how to find an instance of `BugSplat` via `BugSplatManager`:

```cs
var bugsplat = FindAnyObjectByType<BugSplatManager>().BugSplat;
```

You can extend `BugSplat` by setting the following properties:

```cs
bugsplat.Attachments.Add(new FileInfo("/path/to/attachment.txt"));
bugsplat.Description = "description!";
bugsplat.Email = "fred@bugsplat.com";
bugsplat.Key = "key!";
bugsplat.Notes = "notes!";
bugsplat.User = "Fred";
bugsplat.CaptureEditorLog = true;
bugsplat.CapturePlayerLog = false;
bugsplat.CaptureScreenshots = true;
```

You can use the `Notes` field to capture arbitrary data such as system information:

```cs
void Start()
{
    bugsplat = FindAnyObjectByType<BugSplatManager>().BugSplat;
    bugsplat.Notes = GetSystemInfo();
}

private string GetSystemInfo()
{
    var info = new Dictionary<string, string>();
    info.Add("OS", SystemInfo.operatingSystem);
    info.Add("CPU", SystemInfo.processorType);
    info.Add("MEMORY", $"{SystemInfo.systemMemorySize} MB");
    info.Add("GPU", SystemInfo.graphicsDeviceName);
    info.Add("GPU MEMORY", $"{SystemInfo.graphicsMemorySize} MB");

    var sections = info.Select(section => $"{section.Key}: {section.Value}");
    return string.Join(Environment.NewLine, sections);
}
```

### Try/Catch Reporting

Exceptions can be sent to BugSplat in a try/catch block by calling `Post`.

```cs
try
{
    throw new Exception("BugSplat rocks!");
}
catch (Exception ex)
{
    StartCoroutine(bugsplat.Post(ex));
}
```

The default values specified on the instance of `BugSplat` can be overridden in the call to `Post`. Additionally, you can provide a `callback` to `Post` that will be invoked with the result once the upload is complete.

```cs
var options = new ReportPostOptions()
{
    Description = "a new description",
    Email = "barney@bugsplat.com",
    Key = "a new key!",
    Notes = "some new notes!",
    User = "Barney"
};

options.AdditionalAttachments.Add(new FileInfo("/path/to/additional.txt"));

static void callback()
{
    Debug.Log($"Exception post callback!");
};

StartCoroutine(bugsplat.Post(ex, options, callback));
```

### Preventing Repeated Reports

By default, BugSplat prevents reports from being sent at a rate greater than 1 per every 3 seconds. You can override the default crash report throttling implementation by setting `ShouldPostException` on your BugSplat instance. To override `ShouldPostException`, assign the property a new `Func<Exception, bool>` value. Be sure your new implementation can handle a null value for `Exception`!

The following example demonstrates how you could implement your own time-based report throttling mechanism:

```cs
var lastPost = new DateTime(0);

bugsplat.ShouldPostException = (ex) =>
{
    var now = DateTime.Now;

    if (now - lastPost < TimeSpan.FromSeconds(3))
    {
        Debug.LogWarning("ShouldPostException returns false. Skipping BugSplat report...");
        return false;
    }

    Debug.Log("ShouldPostException returns true. Posting BugSplat report...");
    lastPost = now;

    return true;
};
```

### Background Thread Exceptions

Unity raises `Application.logMessageReceived` only for logs written on the main thread. An unhandled exception on a background thread is written to the player log but never reaches that callback, so BugSplat captures these through `Application.logMessageReceivedThreaded` instead, buffers them, and posts them from the main thread on the next frame.

This is on by default. Uncheck **Capture Exceptions On Background Threads** on your `BugSplatManager` to restore the previous behavior of reporting only main-thread exceptions.

Because the threaded callback also fires for main-thread logs that `logMessageReceived` already delivered, BugSplat ignores anything raised on the main thread there — main-thread exceptions are reported exactly once either way.

At most 64 background exceptions are buffered at a time. A thread failing in a tight loop can produce them faster than they can be uploaded, so the excess is dropped and a single warning is logged rather than queueing unbounded work.

### Unobserved Task Exceptions

A `Task` that faults with nobody awaiting it never writes to the Unity log at all, so neither log callback sees it. BugSplat subscribes to `TaskScheduler.UnobservedTaskException` and posts these through the same main-thread queue as background thread exceptions. Each exception inside the `AggregateException` is reported separately, so unrelated failures land in separate buckets rather than one.

This is on by default. Uncheck **Capture Unobserved Task Exceptions** on your `BugSplatManager` to disable it.

Two things are worth knowing about the timing. The runtime raises this event only when a garbage collection notices the faulted `Task`, so reports arrive well after the failure and a `Task` that is never collected is never reported. And BugSplat deliberately does **not** call `SetObserved()` on these — marking the exception observed would suppress whatever your project does with it next, and reporting a failure must not change whether that failure happens.

### Windows Crashes

BugSplat captures native Windows crashes via [BugSplat for Windows](https://docs.bugsplat.com/introduction/getting-started/integrations/desktop/cplusplus). See the [Windows](#-windows) section for setup details.

> [!IMPORTANT]
> `Utils.ForceCrash` goes through Unity's internal crash pipeline and will **not** be captured by native crash reporters on iOS, macOS, or Android. On those platforms, use a real native crash (such as a null pointer dereference in native code) to test crash reporting. The BugSplat sample app uses real native crashes to test native crash reporting.

### Windows Symbols

To enable the uploading of plugin symbols, generate an OAuth2 Client ID and Client Secret on the BugSplat [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page and provide them as described in [Symbol Upload Credentials](#symbol-upload-credentials). If your game contains Native Windows C++ plugins, `.dll` and `.pdb` files in the `Assets/Plugins/x86` and `Assets/Plugins/x86_64` folders will be uploaded by BugSplat's PostBuild script and used in symbolication.

For IL2CPP builds, BugSplat will also upload `LineNumberMappings.json`. Line mappings allow BugSplat to replace generated C++ function names, file names, and line numbers with their original C# equivalents.

### Support Response

BugSplat has the ability to display a support response to users who encounter a crash. You can show your users a generalized support response for all crashes, or a custom support response that corresponds to the type of crash that occurred. Defining a support response allows you to alert users that bug has been fixed in a new version, or that they need to update their graphics drivers.

Next, pass a callback to `bugsplat.Post`. In the callback handler add code to open the support response in the user's browser:

```cs
private string infoUrl = "";

public void CatchExceptionThenPostNewBugSplat()
{
    try
    {
        MethodThatThrows();
    }
    catch (Exception ex)
    {
        var options = new ReportPostOptions()
        {
            Description = "a new description"
        };

        StartCoroutine(bugsplat.Post(ex, options, ExceptionCallback));
    }
}

void ExceptionCallback(ExceptionReporterPostResult result)
{
    UnityEngine.Debug.Log($"Exception post callback result: {result.Message}");

    if (result.Response == null) {
        return;
    }

    UnityEngine.Debug.Log($"BugSplat Status: {result.Response.status}");
    UnityEngine.Debug.Log($"BugSplat Crash ID: {result.Response.crashId}");
    UnityEngine.Debug.Log($"BugSplat Support URL: {result.Response.infoUrl}");

    infoUrl = result.Response.infoUrl;
}

private void OpenUrl(string url)
{
    var escaped = url.Replace("?", "\\?").Replace("&", "\\&").Replace(" ", "%20").Replace("!", "\\!");

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
    Process.Start(url);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    Process.Start("open", escaped);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    Process.Start("xdg-open", escaped);
#else
    UnityEngine.Debug.Log($"OpenUrl unsupported platform: {Application.platform}");
#endif
}
```

When an exception occurs, a page similar to the following will open in the user's browser on Windows, macOS, and Linux.

<img width="1086" alt="image" src="https://github.com/user-attachments/assets/3a3d6f82-e3bf-42bc-ae7f-582ba35cd499">

More information on support responses can be found [here](https://docs.bugsplat.com/introduction/production/setting-up-custom-support-responses).

## 🧭 Platform Support

What BugSplat captures on each platform. Setup for each one is covered in [Android](#-android), [iOS](#-ios), [macOS](#-macos), and [Windows](#-windows).

| Capability | Windows | macOS | iOS | Android | Linux | WebGL |
| --- | --- | --- | --- | --- | --- | --- |
| Managed C# exceptions | Yes | Yes | Yes | Yes | Yes | Yes |
| Native crashes | Yes (Mono or IL2CPP) | Yes (IL2CPP only) | Yes | Yes | No | No |
| Hang / ANR reporting | Yes (opt-in) | No | Yes | Yes (Android 11+) | No | No |
| Offline retry of native reports | Yes | Yes | Yes | Yes | n/a | n/a |
| User feedback (`PostFeedback`) | Yes | Yes | Yes | Yes | Yes | No |
| Automatic symbol upload | Yes (from a Windows editor) | Yes | Yes | Yes | No | No |

- **Managed C# exceptions** are captured on every platform through Unity's log callbacks — including [background threads](#background-thread-exceptions) — and posted over HTTPS. WebGL uses a separate reporter that cannot attach log files or screenshots.
- **Native crashes** require the matching option on your `BugSplatOptions` asset: `UseNativeCrashReportingForWindows`, `UseNativeCrashReportingForMac`, `UseNativeCrashReportingForIos`, or `UseNativeCrashReportingForAndroid`. Linux and WebGL have no native reporter and fall back to managed exception reporting alone. Every native reporter is compiled out of the editor, so play mode exercises the managed rows only.
- **Hang / ANR reporting** is opt-in on Windows through `WindowsHangDetectionTimeoutMs` (`0`, disabled, by default) and automatic on iOS and Android once native crash reporting is enabled. Android ANRs additionally need Android 11 (API level 30) at runtime. macOS has no hang detection.
- **Offline retry** covers native reports only: they are written to disk when the crash happens and uploaded on a later launch, so being offline at crash time does not lose the report. Managed exception posts are never persisted — if that upload fails, the report is gone.
- **User feedback** is posted with `bugsplat.PostFeedback`. WebGL has no feedback client and logs an error instead.
- **Automatic symbol upload** runs as a post-build step and needs [symbol upload credentials](#symbol-upload-credentials). Windows uploads `.pdb`, `.dll`, and `.exe` files only when the player is built from a **Windows editor** with **Copy PDB files** enabled. macOS uploads dSYMs when `UploadDebugSymbolsForMac` is set, unless the build is an Xcode project export. iOS adds an Xcode build phase that uploads dSYMs during the Xcode build when `UploadDebugSymbolsForIos` is set. Android uploads the generated symbols archive when `UploadDebugSymbolsForAndroid` is set, and skips it when **Export Project** is enabled or **Debug Symbols** is **None**. Linux and WebGL have no symbol upload step.

Two things that don't fit the table: `Post(FileInfo minidump)` works on every platform except WebGL, where it logs that it isn't implemented and returns without uploading; and IL2CPP's `LineNumberMappings.json`, which maps generated C++ frames back to C# names, files, and line numbers, is uploaded on Windows, macOS, and iOS only — the Android symbol upload sends native `.so` symbols alone.

## 🤖 Android

The bugsplat-unity plugin supports crash reporting for native C++ crashes on Android via Crashpad. To configure crash reporting for Android, set the `UseNativeCrashReportingForAndroid` and `UploadDebugSymbolsForAndroid` properties to `true` on the BugSplatManager instance.

You'll also need to configure the scripting backend to use IL2CPP, target **ARM64**, and set the Minimum API Level to **Android 8.0 (API level 26)** or higher. ARM64 is the only configuration BugSplat tests; the bundled `bugsplat-android-release.aar` also ships `armeabi-v7a` and `x86_64` native libraries, but those ABIs are untested and unsupported.

![Android Player Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec8f5b7-8dfd-43db-84e0-7e7d1229324a)

When you build your app for Android, be sure to set `Create symbols.zip` to `Debugging`

![Android Build Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/0181f2a8-8fb2-4745-b336-3e7f210aa55e)

### ANR Reporting

When `UseNativeCrashReportingForAndroid` is enabled, BugSplat also reports ANRs (Application Not Responding events). No additional configuration is required. On the next launch, BugSplat queries the OS for ANRs that occurred during the previous session and uploads them as `Android.ANR` reports. ANR reporting requires **Android 11 (API level 30)** or higher at runtime; on older OS versions it is silently skipped while native crash reporting continues to work.

## 🍎 iOS

The bugsplat-unity plugin supports native crash reporting on iOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes via Mach exception handling. To configure crash reporting for iOS, set the `UseNativeCrashReportingForIos` and `UploadDebugSymbolsForIos` properties to `true` on the BugSplatManager instance.

When native crash reporting is enabled, BugSplat automatically disables Unity's built-in crash reporter during the build to prevent conflicts with PLCrashReporter. Crashes are captured at crash time and uploaded on the next app launch.

For IL2CPP builds, BugSplat will also upload `LineNumberMappings.json` alongside dSYMs. This enables BugSplat to map IL2CPP-generated C++ symbols back to original C# method names, file names, and line numbers.

### Hang Detection

When `UseNativeCrashReportingForIos` is enabled, BugSplat also detects fatal main-thread hangs. No additional configuration is required. If the main thread stalls past the detection threshold and the app is subsequently terminated without recovering — by the OS watchdog at launch/resume, or by the user force-quitting — BugSplat uploads an `App Hang (Fatal)` report on the next launch. Hangs the app recovers from are not reported. Detection is suppressed while a debugger is attached, so test hang reporting on a build run without the Xcode debugger.

## 🖥 macOS

The bugsplat-unity plugin supports native crash reporting on macOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes via Mach exception handling. Native macOS crash reporting requires the **IL2CPP** scripting backend.

To configure crash reporting for macOS, set the `UseNativeCrashReportingForMac` and `UploadDebugSymbolsForMac` properties to `true` on your `BugSplatOptions` asset. For IL2CPP builds, BugSplat will upload dSYMs and `LineNumberMappings.json` for full symbolication.

`Player.log` is attached to native macOS crash reports when `CapturePlayerLog` is enabled on your `BugSplatOptions` asset.

## 🪟 Windows

The bugsplat-unity plugin supports native crash reporting on Windows via [BugSplat for Windows](https://docs.bugsplat.com/introduction/getting-started/integrations/desktop/cplusplus). Native Windows crash reporting works with both the **Mono** and **IL2CPP** scripting backends, on x86 (32-bit), x64, and Windows-on-ARM (ARM64) players.

To configure native crash reporting for Windows, set the `UseNativeCrashReportingForWindows` property to `true` on your `BugSplatOptions` asset.

When native crash reporting is enabled:

- Native crashes are captured at crash time and uploaded immediately. Reports that can't be uploaded (for example, when the user is offline) are uploaded automatically on the next launch.
- `Player.log` is attached to native crash reports when `CapturePlayerLog` is enabled on your `BugSplatOptions` asset. Setting the `CapturePlayerLog` property at runtime adds or removes the attachment.
- The BugSplat crash dialog is shown by default. Set `WindowsShowCrashDialog` to `false` to send reports silently instead.
- At build time, BugSplat copies `BugSplatMonitor.exe`, `BugSplatRc.dll`, and `BugSplatWer.dll` next to your game's executable. These files are required for crash reporting and must be shipped alongside your game's executable in your installer.
- Fail-fast crashes — stack buffer overruns and heap corruption — bypass BugSplat's crash handler entirely and need one extra install-time step. See [Windows Error Reporting](#windows-error-reporting).

The native library is a standard `/MD` binary and depends on the Microsoft Visual C++ Redistributable (`vcruntime140.dll`, `msvcp140.dll`), which Unity Windows players already require. If the redistributable is missing on an end user's machine, native crash reporting fails to initialize with an error in the log, and .NET exception reporting continues to work.

For IL2CPP builds, BugSplat copies `LineNumberMappings.json` into the build directory and uploads it with your symbols so IL2CPP-generated C++ frames symbolicate back to C# method names, file names, and line numbers. See [Windows Symbols](#windows-symbols) for symbol upload configuration.

### Windows Hang Detection

Set `WindowsHangDetectionTimeoutMs` to a non-zero value to report hangs when your game's main thread stops responding for longer than the configured timeout. When a hang is detected, BugSplat captures a hang report, uploads it, and **terminates the hung process**.

Hang detection is **disabled by default** (`0`) because long frames — such as loading screens or synchronous asset operations — can be falsely reported as hangs, and a false positive terminates your game. If you enable hang detection, choose a timeout comfortably longer than your game's longest expected frame.

### Windows Error Reporting

A few crash types terminate a process without giving any in-process code a chance to run. The most common are stack buffer overruns (`0xC0000409`, which is also what `__fastfail` produces) and heap corruption (`0xC0000374`). BugSplat's crash handler never sees these, so Windows Error Reporting has to hand them over instead — that is what `BugSplatWer.dll` is for.

Two things must be true for it to work:

1. **`BugSplatWer.dll` sits next to your game's executable.** The post-build step already does this when `UseNativeCrashReportingForWindows` is enabled.
2. **A machine-wide registry value names that DLL's full path.** Under `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules`, add a `REG_DWORD` whose **name** is the absolute path to the installed `BugSplatWer.dll` (the data is ignored). This lives in `HKLM`, so writing it requires administrator rights.

**Your installer is responsible for step 2**, and for removing the value on uninstall:

```bat
reg add "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules" /v "C:\Program Files\MyGame\BugSplatWer.dll" /t REG_DWORD /d 0 /f
reg delete "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules" /v "C:\Program Files\MyGame\BugSplatWer.dll" /f
```

The value name must match the installed path exactly, using backslashes. Moving or reinstalling the game to a different folder silently disarms it.

For local builds, use **BugSplat > Windows > Register WER Handler** in the editor. It asks for your built player, writes the value elevated, and reads it back to confirm. **Check WER Handler Registration** reports the current state.

At runtime, `bugsplat.WindowsWerEnabled` tells you whether the handler registered. When it hasn't, BugSplat logs what will be missed and how to fix it — as a warning in development builds and an informational message otherwise, since end users can't act on it. All other crashes are reported normally either way.

If registration appears to succeed but `WindowsWerEnabled` stays `false`, check your endpoint-protection software: `RuntimeExceptionHelperModules` is a known persistence location and some products monitor or block writes to it.

Two places to look when a report doesn't arrive: `%TEMP%\BugSplat\<Application>-<Version>\<GUID>\` holds the SDK's own logs including `BugSplatWer.log`, and `%LOCALAPPDATA%\CrashDumps` collects dumps Windows wrote because nothing claimed the crash.

> [!NOTE]
> BugSplat installs its handler with `SetUnhandledExceptionFilter` and then prevents that filter from being replaced, so other middleware in your project cannot install a top-level exception filter after BugSplat initializes.

### Migrating from 4.x

Version 5.0.0 replaces the Unity crash-folder minidump flow with native crash reporting:

- `PostAllCrashes`, `PostCrash`, and `PostMostRecentCrash` have been removed. Unsent native crash reports are uploaded automatically at startup — you no longer need to call anything at launch. Delete any calls to these methods.
- Unity's `CrashReporting.crashReportFolder` minidumps are no longer read or uploaded.
- `Post(FileInfo minidump)` still works for posting your own minidump files on every platform except WebGL, where it logs that it isn't implemented and returns without uploading.
- `SymbolUploadClientId` and `SymbolUploadClientSecret` have been removed from `BugSplatOptions`. Storing them there put the secret in version control and inside shipped builds. Set them per database from **BugSplat > Symbol Upload > Set Credentials**, or with environment variables in CI.
- Those environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET` to `SYMBOL_UPLOAD_CLIENT_ID`/`SYMBOL_UPLOAD_CLIENT_SECRET`, matching the names the `symbol-upload` CLI already reads. The old names are no longer read. See [Symbol Upload Credentials](#symbol-upload-credentials).
- **iOS projects exported with Append, or checked into version control, keep their old "Upload dSYM files to BugSplat" build phase.** Unity matches an existing phase on its script body, so the rewritten phase is not recognised as the same one. Delete the old phase and build again, or re-export with Replace. A phase generated before 5.0.0 contains your Client ID and Secret in plain text — **rotate them**.

## 🧩 API

The following API methods are available to help you customize BugSplat to fit your needs.

### BugSplatManager

| Setting | Description |
| --------------- | --------------- |
| DontDestroyManagerOnSceneLoad | Should the BugSplat Manager persist through scene loads? | 
| RegisterLogMessageReceived | Register a callback function and allow BugSplat to capture instances of LogType.Exception.|
| CaptureExceptionsOnBackgroundThreads | Also capture unhandled exceptions thrown on background threads (default). Requires RegisterLogMessageReceived. See [Background thread exceptions](#background-thread-exceptions).|

### BugSplat Options

| Option | Description |
| --------------- | --------------- |
| Database  | The name of your BugSplat database. | 
| Application| The name of your BugSplat application. Defaults to Application.productName if no value is set.|
| Version | The version of your BugSplat application. Defaults to Application.version if no value is set.|
| Description | A default description that can be overridden by call to Post.|
| Email | A default email that can be overridden by call to Post.|
| Key | A default key that can be overridden by call to Post.|
| Notes | A default general purpose field that can be overridden by call to post |
| User | A default user that can be overridden by call to Post |
| CaptureEditorLog| Should BugSplat upload Editor.log when Post is called|
| CapturePlayerLog| Should BugSplat upload Player.log when Post is called. Enabled by default — see [Player.log and privacy](#playerlog-and-privacy) |
| CaptureScreenshots | Should BugSplat a screenshot and upload it when Post is called |
| PostExceptionsInEditor | Should BugSplat upload exceptions when in editor. Defaults to false so play mode exceptions stay out of your database |
| PersistentDataFileAttachmentPaths |  Paths to files (relative to Application.persistentDataPath) to upload with each report |
| UseNativeCrashReportingForWindows | Use native crash reporting library (bugsplat-windows) for Windows builds. Works with both Mono and IL2CPP |
| WindowsShowCrashDialog | Show the BugSplat crash dialog when a native crash occurs on Windows (default). When disabled, crash reports are sent silently |
| WindowsHangDetectionTimeoutMs | Native hang detection timeout in milliseconds for Windows. 0 (default) disables hang detection |

> [!NOTE]
> `ShouldPostException` is not a field on the `BugSplatOptions` asset. It is a runtime-only property you assign on your `BugSplat` instance in code — see [Preventing Repeated Reports](#preventing-repeated-reports).

### Player.log and privacy

`CapturePlayerLog` is **enabled by default** on both construction paths — a new `BugSplatOptions` asset and a `BugSplat` created in code both start with it on — because `Player.log` is the most useful attachment on a crash report. WebGL is the exception: the platform has no `Player.log`, so a `BugSplat` created in code there defaults to off and the setting has no effect. Be aware that Unity writes it under the user's profile directory on every desktop platform, and it records file system paths that contain the operating system username. If you would rather not collect that, uncheck **Capture Player Log** on your options asset, or set the property in code:

```cs
bugsplat.CapturePlayerLog = false;
```

> **Upgrading from 4.x:** `BugSplatOptions` assets created before 5.0.0 keep whatever value is already serialized in the asset file; only newly created assets pick up the new default. Check the field on your existing asset if you want the new behavior.

### Attaching Files to Native Crash Reports

`Attachments` adds files to managed posts. A native crash is captured and uploaded by the platform's crash reporter, which never sees that list, so files for native reports are attached with `AttachNativeLogFile`:

```cs
bugsplat.AttachNativeLogFile("/path/to/support.log");
```

Windows attaches every file passed to it. On macOS the native bridge holds a single log file, so a second call replaces the first — including the `Player.log` that `CapturePlayerLog` attaches. The call is a no-op on iOS and Android, where the native reporters take no attachments; `Player.log` still ships with managed posts on those platforms.

Passing `Application.consoleLogPath` is treated as the player log rather than as an extra file: it is attached at most once, and setting `CapturePlayerLog = false` afterwards still removes it. Prefer `CapturePlayerLog` for that file — see [Player.log and privacy](#playerlog-and-privacy).

### BugSplat Environment Variables

| Variable | Description |
|----------| --------------- |
| SYMBOL_UPLOAD_CLIENT_ID | An OAuth2 Client ID value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page.<br>Takes precedence over `~/.bugsplat/credentials/<database>.sh` — see [Symbol Upload Credentials](#symbol-upload-credentials) |
| SYMBOL_UPLOAD_CLIENT_SECRET | An OAuth2 Client Secret value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page.<br>Takes precedence over `~/.bugsplat/credentials/<database>.sh` — see [Symbol Upload Credentials](#symbol-upload-credentials) |

### Symbol Upload Credentials

Credentials are generated on BugSplat's [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page and are **specific to one database**. They are never stored in your project — an asset carrying them ends up in version control and inside shipped builds. They resolve in this order:

1. **`SYMBOL_UPLOAD_CLIENT_ID` / `SYMBOL_UPLOAD_CLIENT_SECRET` environment variables** — use these in CI. They are the names the `symbol-upload` CLI reads, so the same pair works whether Unity runs the upload or your CI runs `xcodebuild` itself.
2. **`~/.bugsplat/credentials/<database>.sh`** — for local development. Set it from **BugSplat > Symbol Upload > Set Credentials**, which writes one file per database, so a machine can hold credentials for as many databases as you work with.

`Clear Credentials` deletes the current project's file; `Check Credentials` reports which source a build would use. When neither source supplies both values, symbol upload is skipped with a warning and the build still succeeds — on iOS as an Xcode build warning, since that upload runs during the Xcode build rather than the Unity one.

Because the file lives in your home directory rather than the project, there is nothing to add to `.gitignore` and nothing to strip out of a build.

> **Upgrading from 4.x:** `SymbolUploadClientId` and `SymbolUploadClientSecret` have been removed from `BugSplatOptions`, and the environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET`. Move your credentials to the menu or the new variables. **If an options asset holding credentials has ever been committed, rotate them** — prior versions serialized both values into player builds and into the generated `project.pbxproj`.

## 🧑‍💻 Contributing

BugSplat ❤️s open source! If you feel that this package can be improved, please open an [Issue](https://github.com/BugSplat-Git/bugsplat-unity/issues). If you have an awesome new feature you'd like to implement, we'd love to merge your [Pull Request](https://github.com/BugSplat-Git/bugsplat-unity/pulls). You can also send us an [email](mailto:support@bugsplat.com), join us on [Discord](https://discord.gg/K4KjjRV5ve), or message us via the in-app chat on [bugsplat.com](https://bugsplat.com).
