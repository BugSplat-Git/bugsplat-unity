[&larr; BugSplat for Unity](../README.md)

# 🖥 macOS

The bugsplat-unity plugin supports native crash reporting on macOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes via Mach exception handling. Native macOS crash reporting requires the **IL2CPP** scripting backend.

To configure crash reporting for macOS, set the `UseNativeCrashReportingForMac` and `UploadDebugSymbolsForMac` properties to `true` on your `BugSplatOptions` asset. For IL2CPP builds, BugSplat will upload dSYMs and `LineNumberMappings.json` for full symbolication.

`Player.log` is attached to native macOS crash reports when both `UseNativeCrashReportingForMac` and `CapturePlayerLog` are enabled on your `BugSplatOptions` asset. Managed .NET exception reports attach it through the reporter instead, so they are unaffected by the native setting.

A native crash report uploads at the **next launch**, and by then Unity has renamed the crashed session's log to `Player-prev.log` and started a fresh `Player.log`. BugSplat therefore reads `Player-prev.log` for that report; when the SDK provides the crashed session ID it verifies the file identity before attaching, and otherwise it attaches on best effort. Reports that fail to upload keep their copy of the log and retry it later. The one case with no log: the app crashes again before BugSplat initializes, so Unity's rotation replaces the file first — the report then carries no `Player.log` rather than a misleading one.

When `UseNativeCrashReportingForMac` is enabled, the post-build step also copies `bugsplat-logo.png` into the built player's `Contents/Resources`. The crash dialog looks up its banner in the app bundle, so without that file it falls back to a plain drawn logo. Xcode project exports are skipped — add the file to your Xcode target's resources yourself if you want the logo there.

## Attachments

A native crash report is uploaded at the **next launch**, not at crash time, and BugSplat asks for its attachments then — in a fresh process that did not experience the crash. A file registered with `AttachNativeLogFile` part-way through a session is therefore not remembered across the crash and never reaches the report.

Register native attachments during initialization instead. `BugSplatOptions.PersistentDataFileAttachmentPaths` is applied on every launch and is unaffected by this. Each attachment is truncated to its last 10 MB. See [Attaching Files to Native Crash Reports](api.md#attaching-files-to-native-crash-reports).

## Testing on Device

A debugger claims the Mach exception ports before PLCrashReporter does, so **a crash that happens under the Xcode debugger never reaches BugSplat**. Hang detection is suppressed outright while a debugger is attached. Xcode's **Build And Run** leaves the debugger on, so a crash triggered that way produces no report and no explanation.

Two ways to test:

- Uncheck **Product > Scheme > Edit Scheme > Run > Info > Debug executable**, then Run. Only the Run action changes; Test and Profile are unaffected.
- Or launch the app from the Finder instead of from Xcode.

Either way the report uploads on the **next** launch, not at crash time, so relaunch the app after the crash.

## Hang Detection

When `UseNativeCrashReportingForMac` is enabled, BugSplat also detects fatal main-thread hangs. No additional configuration is required. If the main thread stalls past the detection threshold and the app is subsequently terminated without recovering, BugSplat uploads an `App Hang (Fatal)` report on the next launch. Hangs the app recovers from are not reported.

The detection threshold is `MacHangDetectionThresholdSeconds`, 5 seconds by default. That is higher than bugsplat-apple's own 2-second default because a Unity game routinely blocks the main thread for seconds at a time — scene loads, shader warmup, synchronous asset loads — and each of those is a false positive waiting to happen. Lower it if your game genuinely never stalls that long.

By default a hang report is uploaded without asking, because the user never had the chance to consent — the app was frozen, then terminated. Turn off `MacAutoSubmitFatalHangReport` on your `BugSplatOptions` asset to ask them instead: the report then goes through the same dialog a native crash shows, so they can describe what the app was doing when it froze. That also needs `MacAutoSubmitCrashReport` off, since it is what decides whether any dialog appears. Both options map onto bugsplat-apple's `autoSubmitCrashReport` and `autoSubmitFatalHangReport` — the Unity names carry a `Mac` prefix because iOS has its own pair — and require a `BugSplat-macOS.dylib` carrying `autoSubmitFatalHangReport`; against an older one the option logs a notice and hang reports keep uploading without asking.

Unlike iOS, macOS has no OS watchdog that terminates an unresponsive app — it beachballs indefinitely — so the only way a hang becomes fatal is a force quit (Option-Command-Escape, Activity Monitor, or a `kill`). Detection is also suppressed while a debugger is attached, so test hang reporting on a built player run outside Xcode.
