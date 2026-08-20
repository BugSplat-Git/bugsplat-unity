[&larr; BugSplat for Unity](../README.md)

# 🤖 Android

The bugsplat-unity plugin supports crash reporting for native C++ crashes on Android via Crashpad. To configure crash reporting for Android, set the `UseNativeCrashReportingForAndroid` and `UploadDebugSymbolsForAndroid` properties to `true` on the BugSplatManager instance.

You'll also need to configure the scripting backend to use IL2CPP, target **ARM64**, and set the Minimum API Level to **Android 8.0 (API level 26)** or higher. ARM64 is the only configuration BugSplat tests; the bundled `bugsplat-android-release.aar` also ships `armeabi-v7a` and `x86_64` native libraries, but those ABIs are untested and unsupported.

![Android Player Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec8f5b7-8dfd-43db-84e0-7e7d1229324a)

When you build your app for Android, be sure to set `Create symbols.zip` to `Debugging`

![Android Build Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/0181f2a8-8fb2-4745-b336-3e7f210aa55e)

## ANR Reporting

When `UseNativeCrashReportingForAndroid` is enabled, BugSplat also reports ANRs (Application Not Responding events). No additional configuration is required. On the next launch, BugSplat queries the OS for ANRs that occurred during the previous session and uploads them as `Android.ANR` reports. ANR reporting requires **Android 11 (API level 30)** or higher at runtime; on older OS versions it is silently skipped while native crash reporting continues to work.
