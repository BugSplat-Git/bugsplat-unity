[&larr; BugSplat for Unity](../README.md)

# 🖥 macOS

The bugsplat-unity plugin supports native crash reporting on macOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes via Mach exception handling. Native macOS crash reporting requires the **IL2CPP** scripting backend.

To configure crash reporting for macOS, set the `UseNativeCrashReportingForMac` and `UploadDebugSymbolsForMac` properties to `true` on your `BugSplatOptions` asset. For IL2CPP builds, BugSplat will upload dSYMs and `LineNumberMappings.json` for full symbolication.

`Player.log` is attached to native macOS crash reports when both `UseNativeCrashReportingForMac` and `CapturePlayerLog` are enabled on your `BugSplatOptions` asset. Managed .NET exception reports attach it through the reporter instead, so they are unaffected by the native setting.

When `UseNativeCrashReportingForMac` is enabled, the post-build step also copies `bugsplat-logo.png` into the built player's `Contents/Resources`. The crash dialog looks up its banner in the app bundle, so without that file it falls back to a plain drawn logo. Xcode project exports are skipped — add the file to your Xcode target's resources yourself if you want the logo there.

## Hang Detection

When `UseNativeCrashReportingForMac` is enabled, BugSplat also detects fatal main-thread hangs. No additional configuration is required. If the main thread stalls past the detection threshold and the app is subsequently terminated without recovering, BugSplat uploads an `App Hang (Fatal)` report on the next launch. Hangs the app recovers from are not reported.

By default a hang report is uploaded without asking, because the user never had the chance to consent — the app was frozen, then terminated. Turn off `AppleAutoSubmitFatalHangReport` on your `BugSplatOptions` asset to ask them instead: the report then goes through the same dialog a native crash shows, so they can describe what the app was doing when it froze. That also needs `AppleAutoSubmitCrashReport` off, since it is what decides whether any dialog appears. Both options mirror the bugsplat-apple properties of the same name, and require a `BugSplat-macOS.dylib` carrying `autoSubmitFatalHangReport`; against an older one the option logs a notice and hang reports keep uploading without asking.

Unlike iOS, macOS has no OS watchdog that terminates an unresponsive app — it beachballs indefinitely — so the only way a hang becomes fatal is a force quit (Option-Command-Escape, Activity Monitor, or a `kill`). Detection is also suppressed while a debugger is attached, so test hang reporting on a built player run outside Xcode.
