[&larr; BugSplat for Unity](../README.md)

# 🍎 iOS

The bugsplat-unity plugin supports native crash reporting on iOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes via Mach exception handling. To configure crash reporting for iOS, set the `UseNativeCrashReportingForIos` and `UploadDebugSymbolsForIos` properties to `true` on the BugSplatManager instance.

When native crash reporting is enabled, BugSplat automatically disables Unity's built-in crash reporter during the build to prevent conflicts with PLCrashReporter. Crashes are captured at crash time and uploaded on the next app launch.

For IL2CPP builds, BugSplat will also upload `LineNumberMappings.json` alongside dSYMs. This enables BugSplat to map IL2CPP-generated C++ symbols back to original C# method names, file names, and line numbers.

`Player.log` is attached to native iOS crash reports when both `UseNativeCrashReportingForIos` and `CapturePlayerLog` are enabled on your `BugSplatOptions` asset. Managed .NET exception reports attach it through the reporter instead, so they are unaffected by the native setting.

## Attachments

A native crash report is uploaded at the **next launch**, not at crash time, and BugSplat asks for its attachments then — in a fresh process that did not experience the crash. A file registered with `AttachNativeLogFile` part-way through a session is therefore not remembered across the crash and never reaches the report.

Register native attachments during initialization instead. `BugSplatOptions.PersistentDataFileAttachmentPaths` is applied on every launch and is unaffected by this. Each attachment is truncated to its last 10 MB. See [Attaching Files to Native Crash Reports](api.md#attaching-files-to-native-crash-reports).

## Hang Detection

When `UseNativeCrashReportingForIos` is enabled, BugSplat also detects fatal main-thread hangs. No additional configuration is required. If the main thread stalls past the detection threshold and the app is subsequently terminated without recovering — by the OS watchdog at launch/resume, or by the user force-quitting — BugSplat uploads an `App Hang (Fatal)` report on the next launch. Hangs the app recovers from are not reported. Detection is suppressed while a debugger is attached, so test hang reporting on a build run without the Xcode debugger.
