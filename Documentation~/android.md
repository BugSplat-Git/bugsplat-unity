[&larr; BugSplat for Unity](../README.md)

# 🤖 Android

The bugsplat-unity plugin supports crash reporting for native C++ crashes on Android via Crashpad. To configure crash reporting for Android, set the `UseNativeCrashReportingForAndroid` and `UploadDebugSymbolsForAndroid` properties to `true` on your `BugSplatOptions` asset (**Edit > Project Settings > BugSplat**).

You'll also need to configure the scripting backend to use IL2CPP, target **ARM64**, and set the Minimum API Level to **Android 8.0 (API level 26)** or higher. ARM64 is the only configuration BugSplat tests; the bundled `bugsplat-android-release.aar` also ships `armeabi-v7a` and `x86_64` native libraries, but those ABIs are untested and unsupported.

![Android Player Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec8f5b7-8dfd-43db-84e0-7e7d1229324a)

When you build your app for Android, be sure to set `Create symbols.zip` to `Debugging`

![Android Build Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/0181f2a8-8fb2-4745-b336-3e7f210aa55e)

## Attachments

With `UseNativeCrashReportingForAndroid` enabled, paths listed in `BugSplatOptions.PersistentDataFileAttachmentPaths` are resolved against `Application.persistentDataPath` and registered with the native crash reporter at startup, so they ride along with native Android crash reports as well as managed ones. With it disabled they still reach managed reports, and nothing is registered natively.

`AttachNativeLogFile` and `DetachNativeLogFile` work on Android as they do on Windows, macOS, and iOS. Files are read when a crash is uploaded, so a log can be attached before anything has written to it.

Unity does not write a `Player.log` on Android — log output goes to logcat and `Application.consoleLogPath` is empty — so `CapturePlayerLog` has no effect on Android reports.

## ANR Reporting

When `UseNativeCrashReportingForAndroid` is enabled, BugSplat also reports ANRs (Application Not Responding events). No additional configuration is required. On the next launch, BugSplat queries the OS for ANRs that occurred during the previous session and uploads them as `Android.ANR` reports. ANR reporting requires **Android 11 (API level 30)** or higher at runtime; on older OS versions it is silently skipped while native crash reporting continues to work.
