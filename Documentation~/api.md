[&larr; BugSplat for Unity](../README.md)

# 🧩 API

The following API methods are available to help you customize BugSplat to fit your needs.

## BugSplatManager

| Setting | Description |
| --------------- | --------------- |
| DontDestroyManagerOnSceneLoad | Should the BugSplat Manager persist through scene loads? | 
| RegisterLogMessageReceived | Register a callback function and allow BugSplat to capture instances of LogType.Exception.|
| CaptureExceptionsOnBackgroundThreads | Also capture unhandled exceptions thrown on background threads (default). Requires RegisterLogMessageReceived. See [Background thread exceptions](usage.md#background-thread-exceptions).|

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
| PersistentDataFileAttachmentPaths |  Paths to files (relative to Application.persistentDataPath) to upload with each report |
| UseNativeCrashReportingForWindows | Use native crash reporting library (bugsplat-windows) for Windows builds. Works with both Mono and IL2CPP |
| WindowsShowCrashDialog | Show the BugSplat crash dialog when a native crash occurs on Windows (default). When disabled, crash reports are sent silently |
| WindowsHangDetectionTimeoutMs | Native hang detection timeout in milliseconds for Windows. 0 (default) disables hang detection |

> [!NOTE]
> `ShouldPostException` is not a field on the `BugSplatOptions` asset. It is a runtime-only property you assign on your `BugSplat` instance in code — see [Preventing Repeated Reports](usage.md#preventing-repeated-reports).

## Player.log and privacy

`CapturePlayerLog` is **enabled by default** on both construction paths — a new `BugSplatOptions` asset and a `BugSplat` created in code both start with it on — because `Player.log` is the most useful attachment on a crash report. WebGL is the exception: the platform has no `Player.log`, so a `BugSplat` created in code there defaults to off and the setting has no effect. Be aware that Unity writes it under the user's profile directory on every desktop platform, and it records file system paths that contain the operating system username. If you would rather not collect that, uncheck **Capture Player Log** on your options asset, or set the property in code:

```cs
bugsplat.CapturePlayerLog = false;
```

> **Upgrading from 4.x:** `BugSplatOptions` assets created before 5.0.0 keep whatever value is already serialized in the asset file; only newly created assets pick up the new default. Check the field on your existing asset if you want the new behavior.

## Attaching Files to Native Crash Reports

`Attachments` adds files to managed posts. A native crash is captured and uploaded by the platform's crash reporter, which never sees that list, so files for native reports are attached with `AttachNativeLogFile`:

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
| iOS | Multiple, once `BugSplat.xcframework` is updated to a build that includes [bugsplat-apple#70](https://github.com/BugSplat-Git/bugsplat-apple/pull/70) |
| Android | Not supported — the call is a no-op |

`Player.log` still ships with managed posts on every platform, including Android.

`CapturePlayerLog` uses the same mechanism with `Application.consoleLogPath`, so the two cooperate: setting it `false` detaches only `Player.log`, and attaching your own file leaves `Player.log` alone. Prefer `CapturePlayerLog` for that file rather than attaching `Application.consoleLogPath` yourself — see [Player.log and privacy](#playerlog-and-privacy).
