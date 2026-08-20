[&larr; BugSplat for Unity](../README.md)

# 🖥 macOS

The bugsplat-unity plugin supports native crash reporting on macOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes via Mach exception handling. Native macOS crash reporting requires the **IL2CPP** scripting backend.

To configure crash reporting for macOS, set the `UseNativeCrashReportingForMac` and `UploadDebugSymbolsForMac` properties to `true` on your `BugSplatOptions` asset. For IL2CPP builds, BugSplat will upload dSYMs and `LineNumberMappings.json` for full symbolication.

`Player.log` is attached to native macOS crash reports when `CapturePlayerLog` is enabled on your `BugSplatOptions` asset.
