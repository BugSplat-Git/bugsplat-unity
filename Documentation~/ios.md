[&larr; BugSplat for Unity](../README.md)

# 🍎 iOS

The bugsplat-unity plugin supports native crash reporting on iOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes via Mach exception handling. To configure crash reporting for iOS, set the `UseNativeCrashReportingForIos` and `UploadDebugSymbolsForIos` properties to `true` on the BugSplatManager instance.

When native crash reporting is enabled, BugSplat automatically disables Unity's built-in crash reporter during the build to prevent conflicts with PLCrashReporter. Crashes are captured at crash time and uploaded on the next app launch.

For IL2CPP builds, BugSplat will also upload `LineNumberMappings.json` alongside dSYMs. This enables BugSplat to map IL2CPP-generated C++ symbols back to original C# method names, file names, and line numbers.

## Hang Detection

When `UseNativeCrashReportingForIos` is enabled, BugSplat also detects fatal main-thread hangs. No additional configuration is required. If the main thread stalls past the detection threshold and the app is subsequently terminated without recovering — by the OS watchdog at launch/resume, or by the user force-quitting — BugSplat uploads an `App Hang (Fatal)` report on the next launch. Hangs the app recovers from are not reported. Detection is suppressed while a debugger is attached, so test hang reporting on a build run without the Xcode debugger.
