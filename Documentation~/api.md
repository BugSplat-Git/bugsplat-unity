[&larr; BugSplat for Unity](../README.md)

# 🧩 API

The following API methods are available to help you customize BugSplat to fit your needs.

## Initialization

BugSplat initializes itself from the `BugSplatOptions` asset selected in **Edit > Project Settings > BugSplat**, before the first scene loads. Nothing needs to be placed in a scene.

| Member | Description |
| --------------- | --------------- |
| `BugSplat.Instance` | The live client, or `null` before initialization. Set before the first scene loads when **Initialize Automatically** is on, so it is ready inside any `Awake`. |
| `BugSplat.IsInitialized` | Whether `Instance` is set. |
| `BugSplat.Initialize(BugSplatOptions)` | Initializes from the given options and returns the client. For projects that turn **Initialize Automatically** off — to wait for a consent screen, say. Calling it again logs a warning and returns the existing instance. |
| `BUGSPLAT_MANUAL_INITIALIZE` | Scripting define. The project owns initialization: BugSplat does not initialize itself, does not warn at startup, and does not fail a build for a missing asset. See [Automation](automation.md#3-initialize-from-code-only). |

> [!NOTE]
> `BugSplatManager` is obsolete. A 4.x scene that still has one keeps working: the component adopts the instance created at startup, or initializes from its own asset when **Initialize Automatically** is off. Remove it when convenient — see [Migrating from 4.x](migrating-from-4x.md).

## BugSplat Options

| Option | Description |
| --------------- | --------------- |
| Database  | The name of your BugSplat database. | 
| Application| The name of your BugSplat application. Defaults to Application.productName if no value is set.|
| Version | The version of your BugSplat application. Defaults to Application.version if no value is set.|
| InitializeAutomatically | Initialize BugSplat from this asset before the first scene loads. `true` by default. Turn it off to call `BugSplat.Initialize` yourself, for example after a consent screen |
| RegisterLogMessageReceived | Register a callback and report `LogType.Exception` log messages as they happen. `true` by default |
| CaptureExceptionsOnBackgroundThreads | Also capture unhandled exceptions thrown on background threads. `true` by default. Requires RegisterLogMessageReceived. See [Background thread exceptions](usage.md#background-thread-exceptions) |
| CaptureUnobservedTaskExceptions | Also capture exceptions from Tasks that faulted and were never awaited — these never reach Unity's log at all. `true` by default. Requires RegisterLogMessageReceived. See [Unobserved task exceptions](usage.md#unobserved-task-exceptions) |
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
| UseNativeCrashReportingForWindows | Use native crash reporting library (bugsplat-windows) for Windows builds. Works with both Mono and IL2CPP |
| UploadDebugSymbolsForWindows | Upload `.pdb`, `.dll` and `.exe` symbols to BugSplat for Windows builds. `true` by default — Windows has always uploaded automatically, so defaulting it off would silently stop existing projects symbolicating. Also needs **Copy PDB Files** and a Windows editor |
| WindowsShowCrashDialog | Show the BugSplat crash dialog when a native crash occurs on Windows (default). When disabled, crash reports are sent silently |
| WindowsHangDetectionTimeoutMs | Native hang detection timeout in milliseconds for Windows. 0 (default) disables hang detection |
| MacAutoSubmitCrashReport | Submit macOS crash reports without asking the user. `false` by default — the convention on desktop, and bugsplat-apple's own macOS default |
| MacAutoSubmitFatalHangReport | Submit macOS fatal hang reports without asking the user. `true` by default. Needs `MacAutoSubmitCrashReport` off too before a dialog can appear |
| MacHangDetectionThresholdSeconds | Seconds the macOS main thread must be blocked before BugSplat declares a hang. `5` by default, above bugsplat-apple's own 2 because Unity blocks the main thread for seconds on scene loads and shader warmup. Positive values below 0.1 are clamped to 0.1; zero or less is not usable and falls back to bugsplat-apple's own default with a warning |
| IosAutoSubmitCrashReport | Submit iOS crash reports without asking the user. `true` by default — the convention on mobile, and bugsplat-apple's own iOS default |
| IosAutoSubmitFatalHangReport | Submit iOS fatal hang reports without asking the user. `true` by default. Needs `IosAutoSubmitCrashReport` off too before a dialog can appear |
| IosHangDetectionThresholdSeconds | Seconds the iOS main thread must be blocked before BugSplat declares a hang. `5` by default, above bugsplat-apple's own 2 because Unity blocks the main thread for seconds on scene loads and shader warmup. Positive values below 0.1 are clamped to 0.1; zero or less is not usable and falls back to bugsplat-apple's own default with a warning |

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

## Attaching Files to Native Crash Reports

`Attachments` adds files to managed posts only. A native crash is captured and uploaded by the platform's crash reporter, which never sees that list. Two things do reach native reports, both only when native crash reporting is enabled for the platform: `PersistentDataFileAttachmentPaths` on your options asset, which is applied to both mechanisms at startup, and `AttachNativeLogFile` in code:

```cs
bugsplat.AttachNativeLogFile("/path/to/support.log");
bugsplat.DetachNativeLogFile("/path/to/support.log");
```

Attaching is **additive and idempotent**. Every attached file is included in a native report, attaching one file never displaces another, and attaching the same file twice attaches it once. Paths are resolved to full paths before they are compared — and compared case-insensitively on Windows — so `"logs/support.log"` and `"C:\Game\Logs\Support.log"` are recognized as the file they name rather than as new attachments. `DetachNativeLogFile` removes one file and leaves the rest attached.

Support by platform:

| Platform | Native attachments |
|---|---|
| Windows | Multiple |
| macOS | Multiple |
| iOS | Multiple |
| Android | Multiple |

On macOS and iOS each attachment is truncated to its last 10 MB.

> **Register native attachments during initialization.** On macOS and iOS a crash report is uploaded at the *next* launch, and BugSplat asks for that report's attachments then, in a fresh process. A path registered part-way through a session is not remembered across the crash, so it never reaches the report. `PersistentDataFileAttachmentPaths` is applied on every launch and is unaffected by this. On Windows and Android the handler captures attachments at crash time, so `AttachNativeLogFile` takes effect whenever you call it.

`Player.log` still ships with managed posts on every platform, including Android.

`CapturePlayerLog` uses the same mechanism with `Application.consoleLogPath`, so the two cooperate: setting it `false` detaches only `Player.log`, and attaching your own file leaves `Player.log` alone. Prefer `CapturePlayerLog` for that file rather than attaching `Application.consoleLogPath` yourself — see [Player.log and privacy](#playerlog-and-privacy).
