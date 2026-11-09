[&larr; BugSplat for Unity](../README.md)

# 🤖 Android

The bugsplat-unity plugin supports crash reporting for native C++ crashes on Android via Crashpad. Native crash reporting (`UseNativeCrashReportingForAndroid`) is on by default; to upload symbols as well, set `UploadDebugSymbolsForAndroid` to `true` on your `BugSplatOptions` asset (**Edit > Project Settings > BugSplat**).

You'll also need to configure the scripting backend to use IL2CPP, target **ARM64**, and set the Minimum API Level to **Android 8.0 (API level 26)** or higher. ARM64 is the only configuration BugSplat tests; the bundled `bugsplat-android-release.aar` also ships `armeabi-v7a` and `x86_64` native libraries, but those ABIs are untested and unsupported.

![Android Player Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec8f5b7-8dfd-43db-84e0-7e7d1229324a)

When you build your app for Android, set **Debug Symbols** to **Full** in the Android build settings, so Unity writes a `*.symbols.zip` next to the APK or AAB; that archive is what BugSplat uploads. **Full** carries file and line information, while **Public** carries function names only. **Symbol file extension** can be either setting: BugSplat renames Unity 6's `libil2cpp.so.dbg` and `libil2cpp.so.sym` files to `libil2cpp.so`, the module name crashes report, before uploading.

![Android Build Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/0181f2a8-8fb2-4745-b336-3e7f210aa55e)

For IL2CPP builds, BugSplat also uploads Unity's `LineNumberMappings.json`, as `LineNumberMappings-android.json`, so native crash reports map IL2CPP-generated C++ frames back to C# method names, file names, and line numbers. The name keeps an Android build from overwriting the mappings of another platform's build that shares its version. BugSplat reads the file from the `<name>_BackUpThisFolder_ButDontShipItWithYourGame/il2cppOutput/Symbols/` folder Unity writes next to the APK or AAB, falling back to `Library/Bee/artifacts/Android/il2cppOutput/cpp/Symbols/`, and leaves it as it is. Unity only writes it when **Edit > Project Settings > Player > Other Settings > IL2CPP Stacktrace Information** is set to **Method Name, File Name, and Line Number**; without it, the build log warns and names the setting. The mappings only help when `libil2cpp.so`'s symbols carry file and line information, so set **Debug Symbols** to **Full**: with **Public**, the build log warns that C# line numbers will be missing.

With **Export Project** enabled nothing is uploaded, since the symbols archive doesn't exist until you build the exported project. The mappings are in the exported project at `unityLibrary/src/main/Il2CppOutputProject/Source/il2cppOutput/Symbols/LineNumberMappings.json`; to upload them yourself, rename that file to `LineNumberMappings-android.json` and upload it with `symbol-upload` 11.2.0 or later.

## Attachments

With `UseNativeCrashReportingForAndroid` enabled, paths listed in `BugSplatOptions.PersistentDataFileAttachmentPaths` are resolved against `Application.persistentDataPath` and registered with the native crash reporter at startup, so they ride along with native Android crash reports as well as managed ones. With it disabled they still reach managed reports, and nothing is registered natively.

`AttachNativeLogFile` and `DetachNativeLogFile` work on Android as they do on Windows, macOS, and iOS. Files are read when a crash is uploaded, so a log can be attached before anything has written to it.

Unity does not write a `Player.log` on Android — log output goes to logcat and `Application.consoleLogPath` is empty — so `CapturePlayerLog` has no effect on Android reports.

## ANR Reporting

When `UseNativeCrashReportingForAndroid` is enabled, BugSplat also reports ANRs (Application Not Responding events). No additional configuration is required. On the next launch, BugSplat queries the OS for ANRs that occurred during the previous session and uploads them as `Android.ANR` reports. ANR reporting requires **Android 11 (API level 30)** or higher at runtime; on older OS versions it is silently skipped while native crash reporting continues to work.
