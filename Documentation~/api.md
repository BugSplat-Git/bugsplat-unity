[&larr; BugSplat for Unity](../README.md)

# 🧩 API

The following API methods are available to help you customize BugSplat to fit your needs.

## BugSplatManager

| Setting | Description |
| --------------- | --------------- |
| DontDestroyManagerOnSceneLoad | Should the BugSplat Manager persist through scene loads? | 
| RegisterLogMessageReceived | Register a callback function and allow BugSplat to capture instances of LogType.Exception.|
| CaptureExceptionsOnBackgroundThreads | Also capture unhandled exceptions thrown on background threads (default). Requires RegisterLogMessageReceived. See [Background thread exceptions](usage.md#background-thread-exceptions).|
| CaptureUnobservedTaskExceptions | Also capture exceptions from Tasks that faulted and were never awaited (default). These never reach Unity's log at all. Requires RegisterLogMessageReceived. See [Unobserved task exceptions](usage.md#unobserved-task-exceptions).|

## BugSplat Options

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
| PersistentDataFileAttachmentPaths |  Paths to files (relative to Application.persistentDataPath) to attach to managed reports, and to native crash reports on platforms where native crash reporting is enabled |
| UseNativeCrashReporting | Start [bugsplat-native](native.md) in players (default). Native crashes, hangs and captures are reported and managed exceptions post through the same SDK. No effect in the editor or on WebGL |
| UploadPolicy | `Dialog` (default): the BugSplat dialog on desktop, next-launch upload on mobile. `Quiet`: upload silently. `Manual`: leave reports for `PostPendingReportsAsync` |
| DumpType | `Normal` (default), `Heap` (adds the managed heaps), `Full` (every readable region). Enforced against your database's size limit at upload |
| HangDetectionTimeoutMs | Hang detection timeout in milliseconds; `0` (default) disables it. `BugSplatManager` sends the heartbeat every frame |
| HangPolicy | `Report` (default): dump the live process, upload, continue. `ReportAndTerminate`: also offer Wait / Close |
| OpenSupportUrl | Open the support-response URL after an interactive upload on desktop (default `true`) |
| ManagedReportFormat | `Xml` (default) or `Json` for managed exception reports posted through the native SDK |
| UploadDebugSymbolsForWindows | Upload `.pdb`, `.dll` and `.exe` symbols for Windows builds. `true` by default. Also needs **Copy PDB Files** and a Windows editor |
| UploadDebugSymbolsForMac | Upload dSYMs as Breakpad `.sym` files for macOS builds |
| UploadDebugSymbolsForLinux | Upload the player's `.so` and `.debug` files as Breakpad `.sym` files for Linux builds |
| UploadDebugSymbolsForIos | Add an Xcode build phase that uploads dSYMs as Breakpad `.sym` files |
| UploadDebugSymbolsForAndroid | Upload the `.so` files from Unity's `symbols.zip` as Breakpad `.sym` files |

> [!NOTE]
> `ShouldPostException` is not a field on the `BugSplatOptions` asset. It is a runtime-only property you assign on your `BugSplat` instance in code — see [Preventing Repeated Reports](usage.md#preventing-repeated-reports).

> [!NOTE]
> `PersistentDataFileAttachmentPaths` entries are relative to `Application.persistentDataPath`, so write `logs/session.log`, not `/Users/you/Desktop/session.log`. An absolute path is skipped with a warning that quotes the entry as you wrote it: a path from the machine that authored the options asset would not exist on a teammate's machine, in CI, or on a player's device, and the sandboxed platforms cannot read outside their own container at all.

## Player.log and privacy

`CapturePlayerLog` is **enabled by default** on both construction paths — a new `BugSplatOptions` asset and a `BugSplat` created in code both start with it on — because `Player.log` is the most useful attachment on a crash report. WebGL is the exception: the platform has no `Player.log`, so a `BugSplat` created in code there defaults to off and the setting has no effect. Be aware that Unity writes it under the user's profile directory on every desktop platform, and it records file system paths that contain the operating system username. If you would rather not collect that, uncheck **Capture Player Log** on your options asset, or set the property in code:

```cs
bugsplat.CapturePlayerLog = false;
```

> **Upgrading from 4.x:** `BugSplatOptions` assets created before 5.0.0 keep whatever value is already serialized in the asset file; only newly created assets pick up the new default. Check the field on your existing asset if you want the new behavior.

## BugSplat instance

Beyond the properties mirrored from `BugSplatOptions`, a `BugSplat` instance exposes:

| Member | Description |
| --- | --- |
| `NativeCrashReportingEnabled` | `true` when bugsplat-native started for this process. `false` in the editor, on WebGL, or when the runtime is missing next to the player (the reason is in the player log) |
| `NativeVersion`, `NativeSettings` | The bugsplat-native version and the settings it was started with |
| `Environment` | The OS/hardware string the SDK detected, sent as a first-class report property. Set it to override, or to `null` to restore detection |
| `CaptureReport()` | Dump the live process out of process and report it like a crash, without crashing |
| `Heartbeat()`, `WatchThread(name)`, `UnwatchThread()` | Feed the hang detector without `BugSplatManager`, or watch an extra thread |
| `PostPendingReportsAsync()` | Upload reports left on disk (offline, or `UploadPolicy.Manual`) |
| `SetCrashDialogEnabled(bool)` | Show or suppress the crash dialog on desktop from now on |
| `AttachNativeLogFile(path)`, `DetachNativeLogFile(path)` | Native crash report attachments, below |
| `WindowsWerEnabled` | Whether `BugSplatWer.dll` is armed; see [Windows Error Reporting](windows.md#windows-error-reporting) |

## Attaching Files to Native Crash Reports

`Attachments` adds files to managed posts only. A native crash is captured and uploaded by bugsplat-native, which never sees that list. Two things do reach native reports when native crash reporting is running: `PersistentDataFileAttachmentPaths` on your options asset, which is applied to both mechanisms at startup, and `AttachNativeLogFile` in code:

```cs
bugsplat.AttachNativeLogFile("/path/to/support.log");
bugsplat.DetachNativeLogFile("/path/to/support.log");
```

Attaching is **additive and idempotent**. Every attached file is included in a native report, attaching one file never displaces another, and attaching the same file twice attaches it once. Paths are resolved to full paths before they are compared — and compared case-insensitively on Windows — so `"logs/support.log"` and `"C:\Game\Logs\Support.log"` are recognized as the file they name rather than as new attachments. `DetachNativeLogFile` removes one file and leaves the rest attached.

Attachments are copied into the report by the monitor right after the dump, on every platform, so a file attached at any point in the session reaches a later crash — including on macOS and iOS, where the report itself uploads on the next launch. Up to 24 files can be attached.

`Player.log` still ships with managed posts on every platform, including Android.

`CapturePlayerLog` uses the same mechanism with `Application.consoleLogPath`, so the two cooperate: setting it `false` detaches only `Player.log`, and attaching your own file leaves `Player.log` alone. Prefer `CapturePlayerLog` for that file rather than attaching `Application.consoleLogPath` yourself — see [Player.log and privacy](#playerlog-and-privacy).
