[&larr; BugSplat for Unity](../README.md)

# 🪟 Windows

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

## Windows Symbols

To enable the uploading of plugin symbols, generate an OAuth2 Client ID and Client Secret on the BugSplat [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page and provide them as described in [Symbol Upload Credentials](symbol-upload.md#symbol-upload-credentials). If your game contains Native Windows C++ plugins, `.dll` and `.pdb` files in the `Assets/Plugins/x86` and `Assets/Plugins/x86_64` folders will be uploaded by BugSplat's PostBuild script and used in symbolication.

For IL2CPP builds, BugSplat will also upload `LineNumberMappings.json`. Line mappings allow BugSplat to replace generated C++ function names, file names, and line numbers with their original C# equivalents.

## Windows Hang Detection

Set `WindowsHangDetectionTimeoutMs` to a non-zero value to report hangs when your game's main thread stops responding for longer than the configured timeout. When a hang is detected, BugSplat captures a hang report, uploads it, and **terminates the hung process**.

Hang detection is **disabled by default** (`0`) because long frames — such as loading screens or synchronous asset operations — can be falsely reported as hangs, and a false positive terminates your game. If you enable hang detection, choose a timeout comfortably longer than your game's longest expected frame.

## Windows Error Reporting

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
