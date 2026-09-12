[&larr; BugSplat for Unity](../README.md)

# 🤖 Android

Native Android crash reporting comes from [bugsplat-native](native.md): Crashpad's at-crash handler, shipped as `libBugSplatMonitor.so`, is executed when a native crash happens and writes the minidump; the report uploads through the SDK on the next launch, without a dialog.

Configure the scripting backend to **IL2CPP**, target **ARM64**, and set the Minimum API Level to **Android 8.0 (API level 26)** or higher. `UseNativeCrashReporting` is on by default.

![Android Player Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec8f5b7-8dfd-43db-84e0-7e7d1229324a)

When you build your app for Android, be sure to set `Create symbols.zip` to `Debugging` and enable `UploadDebugSymbolsForAndroid`.

![Android Build Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/0181f2a8-8fb2-4745-b336-3e7f210aa55e)

## Attachments

Paths listed in `BugSplatOptions.PersistentDataFileAttachmentPaths` are resolved against `Application.persistentDataPath` and registered with the native reporter at startup; `AttachNativeLogFile` and `DetachNativeLogFile` work at any point in the session. Files are copied into the report at crash time.

Unity does not write a `Player.log` on Android — log output goes to logcat and `Application.consoleLogPath` is empty — so `CapturePlayerLog` has no effect on Android reports.

## Hang detection and ANRs

Set `HangDetectionTimeoutMs` to have the SDK's watchdog report a stalled Unity main thread (the heartbeat `BugSplatManager` sends every frame stops). Application Not Responding events raised by the OS are imported on the next launch on Android 11 (API level 30) and higher.

## Symbols

`UploadDebugSymbolsForAndroid` uploads the `.so` files from Unity's `symbols.zip` as Breakpad `.sym` files (`symbol-upload --dumpSyms`). It is skipped when **Export Project** is enabled or **Debug Symbols** is **None**. See [Symbol Upload](symbol-upload.md) for credentials.
