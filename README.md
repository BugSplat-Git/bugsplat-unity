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

After installing `com.bugsplat.unity`, you can import a sample project to help you get started with BugSplat. Click here if you'd like to skip the sample project and get straight to the [usage](Documentation~/usage.md) instructions.

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

## 🧭 Platform Support

What BugSplat captures on each platform. Setup for each one is covered in [Android](Documentation~/android.md), [iOS](Documentation~/ios.md), [macOS](Documentation~/macos.md), and [Windows](Documentation~/windows.md).

| Capability | Windows | macOS | iOS | Android | Linux | WebGL |
| --- | --- | --- | --- | --- | --- | --- |
| Managed C# exceptions | Yes | Yes | Yes | Yes | Yes | Yes |
| Native crashes | Yes (Mono or IL2CPP) | Yes (IL2CPP only) | Yes | Yes | No | No |
| Hang / ANR reporting | Yes (opt-in) | No | Yes | Yes (Android 11+) | No | No |
| Offline retry of native reports | Yes | Yes | Yes | Yes | n/a | n/a |
| User feedback (`PostFeedback`) | Yes | Yes | Yes | Yes | Yes | No |
| Automatic symbol upload | Yes (from a Windows editor) | Yes | Yes | Yes | No | No |

- **Managed C# exceptions** are captured on every platform through Unity's log callbacks — including [background threads](Documentation~/usage.md#background-thread-exceptions) — and posted over HTTPS. WebGL uses a separate reporter that cannot attach log files or screenshots.
- **Native crashes** require the matching option on your `BugSplatOptions` asset: `UseNativeCrashReportingForWindows`, `UseNativeCrashReportingForMac`, `UseNativeCrashReportingForIos`, or `UseNativeCrashReportingForAndroid`. Linux and WebGL have no native reporter and fall back to managed exception reporting alone. Every native reporter is compiled out of the editor, so play mode exercises the managed rows only.
- **Hang / ANR reporting** is opt-in on Windows through `WindowsHangDetectionTimeoutMs` (`0`, disabled, by default) and automatic on iOS and Android once native crash reporting is enabled. Android ANRs additionally need Android 11 (API level 30) at runtime. macOS has no hang detection.
- **Offline retry** covers native reports only: they are written to disk when the crash happens and uploaded on a later launch, so being offline at crash time does not lose the report. Managed exception posts are never persisted — if that upload fails, the report is gone.
- **User feedback** is posted with `bugsplat.PostFeedback`. WebGL has no feedback client and logs an error instead.
- **Automatic symbol upload** runs as a post-build step and needs [symbol upload credentials](Documentation~/symbol-upload.md#symbol-upload-credentials). Windows uploads `.pdb`, `.dll`, and `.exe` files only when the player is built from a **Windows editor** with **Copy PDB files** enabled. macOS uploads dSYMs when `UploadDebugSymbolsForMac` is set, unless the build is an Xcode project export. iOS adds an Xcode build phase that uploads dSYMs during the Xcode build when `UploadDebugSymbolsForIos` is set. Android uploads the generated symbols archive when `UploadDebugSymbolsForAndroid` is set, and skips it when **Export Project** is enabled or **Debug Symbols** is **None**. Linux and WebGL have no symbol upload step.

Two things that don't fit the table: `Post(FileInfo minidump)` works on every platform except WebGL, where it logs that it isn't implemented and returns without uploading; and IL2CPP's `LineNumberMappings.json`, which maps generated C++ frames back to C# names, files, and line numbers, is uploaded on Windows, macOS, and iOS only — the Android symbol upload sends native `.so` symbols alone.

## 📚 Documentation

Everything above gets you reporting. These pages cover the rest.

| Page | What's in it |
| --- | --- |
| [Usage](Documentation~/usage.md) | Adding metadata, try/catch reporting, throttling, background thread and unobserved task exceptions, support responses |
| [Android](Documentation~/android.md) | Native crash reporting via Crashpad, player settings, `symbols.zip`, ANR reporting |
| [iOS](Documentation~/ios.md) | Native crash reporting via PLCrashReporter, dSYM upload, hang detection |
| [macOS](Documentation~/macos.md) | Native crash reporting via PLCrashReporter, dSYM upload |
| [Windows](Documentation~/windows.md) | Native crash reporting, plugin and IL2CPP symbols, hang detection, Windows Error Reporting |
| [API](Documentation~/api.md) | `BugSplatManager` settings, every `BugSplatOptions` field, `Player.log` and privacy |
| [Symbol Upload](Documentation~/symbol-upload.md) | Credentials, where they resolve from, environment variables |
| [Migrating from 4.x](Documentation~/migrating-from-4x.md) | What 5.0.0 removed and renamed, and what to change |

## 🧑‍💻 Contributing

BugSplat ❤️s open source! If you feel that this package can be improved, please open an [Issue](https://github.com/BugSplat-Git/bugsplat-unity/issues). If you have an awesome new feature you'd like to implement, we'd love to merge your [Pull Request](https://github.com/BugSplat-Git/bugsplat-unity/pulls). You can also send us an [email](mailto:support@bugsplat.com), join us on [Discord](https://discord.gg/K4KjjRV5ve), or message us via the in-app chat on [bugsplat.com](https://bugsplat.com).
