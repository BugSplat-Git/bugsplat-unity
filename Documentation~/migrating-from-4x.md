[&larr; BugSplat for Unity](../README.md)

# 🚚 Migrating from 4.x (and from the 5.0 pre-releases)

Version 5.0.0 replaces the per-platform native plugins — bugsplat-windows on Windows, bugsplat-apple on macOS and iOS, bugsplat-android on Android — with one C# binding over [bugsplat-native](native.md). The public surface you call from game code (`BugSplatManager`, `BugSplat`, `Post`, `PostFeedback`, `Attributes`, `Attachments`, `AttachNativeLogFile`) is unchanged; the options and a few platform-specific calls are not.

## Options asset

Open every `BugSplatOptions` asset once after upgrading. Unity keeps the fields it still knows and drops the rest silently, so check the **Native Crash Reporting** section:

| 4.x / 5.0 pre-release field | 5.0.0 |
| --- | --- |
| `UseNativeCrashReportingForWindows`, `UseNativeCrashReportingForMac`, `UseNativeCrashReportingForIos`, `UseNativeCrashReportingForAndroid` | One `UseNativeCrashReporting` (default **on**). Native crash reporting no longer needs opting in per platform; turn it off only for platforms you cannot ship the runtime on. |
| `WindowsShowCrashDialog` | `UploadPolicy` (`Dialog` / `Quiet` / `Manual`), on every desktop platform. |
| `WindowsHangDetectionTimeoutMs`, `MacHangDetectionThresholdSeconds`, `IosHangDetectionThresholdSeconds` | `HangDetectionTimeoutMs` (milliseconds, `0` = off) and `HangPolicy`, on every platform. |
| `MacAutoSubmitCrashReport`, `IosAutoSubmitCrashReport`, `MacAutoSubmitFatalHangReport`, `IosAutoSubmitFatalHangReport` | Gone. Desktop shows the dialog at crash time (`UploadPolicy`); mobile uploads on the next launch without asking. |
| — | New: `DumpType`, `OpenSupportUrl`, `ManagedReportFormat`, `UploadDebugSymbolsForLinux`. |

## Code

- `new BugSplat(database, application, version, useNativeLibIos, useNativeLibAndroid, useNativeLibMac, useNativeLibWin, capturePlayerLog, autoSubmitCrashReport, autoSubmitFatalHangReport, hangDetectionThresholdSeconds, nativeAttachments)` is now `new BugSplat(database, application, version, useNativeCrashReporting = true, capturePlayerLog = true, NativeSettings nativeSettings = null, IEnumerable<string> nativeAttachments = null)`.
- `SetWindowsCrashDialogEnabled(bool)` is `SetCrashDialogEnabled(bool)` and works on every desktop platform.
- `SetWindowsHangDetectionTimeout(int)` is gone: the timeout is read once at startup from `HangDetectionTimeoutMs` / `NativeSettings`. Call `bugsplat.Heartbeat()` yourself only if you do not use `BugSplatManager`.
- New: `NativeCrashReportingEnabled`, `NativeVersion`, `NativeSettings`, `Environment`, `CaptureReport()`, `Heartbeat()`, `WatchThread(name)` / `UnwatchThread()`, `PostPendingReportsAsync()`.
- Managed exceptions in a player are now posted through the native SDK as structured reports (crash type 21, XML) instead of the Unity exception type over HTTP. They carry the same frames, `Player.log`, screenshot and attachments, and get a crash id and support-response URL like every other report. In the editor and on WebGL nothing changed.
- The `ExceptionReporterPostResult.Response` your callbacks read is populated from the native upload the same way; `HttpResponseMessage` callbacks (`PostFeedback`, `Post(FileInfo)`) still receive a status code and a JSON body with `status`, `crashId` and `infoUrl`.

## Shipping

- **Windows** ships `BugSplatMonitor.exe`, `BugSplatReporter.exe`, `BugSplatWer.dll` and `theme/` next to the executable. `BugSplatRc.dll` is gone. The WER registry value still names `BugSplatWer.dll`.
- **macOS** ships `Contents/Helpers/BugSplatMonitor`, `BugSplatReporter.app` and `theme/`; sign them with the app. The `BugSplat-macOS.dylib` and `bugsplat-logo.png` are gone.
- **Linux** gains native crash reporting: `BugSplatMonitor`, `BugSplatReporter` and `theme/` next to the player.
- **Android** ships `libbugsplat.so` and `libBugSplatMonitor.so` instead of the `bugsplat-android` AAR; there is no Java API to call.
- **iOS** links `libbugsplat.a` instead of `BugSplat.xcframework`.
- Symbols for macOS, Linux, iOS and Android are uploaded as Breakpad `.sym` files (`symbol-upload --dumpSyms`); Windows keeps PDBs.

## From 4.x specifically

- `PostAllCrashes`, `PostCrash`, and `PostMostRecentCrash` have been removed. Unsent native crash reports are uploaded automatically at startup — you no longer need to call anything at launch. Delete any calls to these methods.
- Unity's `CrashReporting.crashReportFolder` minidumps are no longer read or uploaded.
- `Post(FileInfo minidump)` still works for posting your own minidump files on every platform except WebGL, where it logs that it isn't implemented and returns without uploading.
- `SymbolUploadClientId` and `SymbolUploadClientSecret` have been removed from `BugSplatOptions`. Storing them there put the secret in version control and inside shipped builds. Set them per database from **BugSplat > Symbol Upload > Set Credentials**, or with environment variables in CI.
- Those environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET` to `SYMBOL_UPLOAD_CLIENT_ID`/`SYMBOL_UPLOAD_CLIENT_SECRET`, matching the names the `symbol-upload` CLI already reads. The old names are no longer read. See [Symbol Upload Credentials](symbol-upload.md#symbol-upload-credentials).
- **Delete any sample you imported under 4.x, then re-import it.** Package Manager copies samples into `Assets/`, and the folder it copies them to is version stamped, so an old copy stays in your project after the upgrade and keeps getting compiled. The 4.x sample calls methods 5.0.0 removed, so it will fail to compile until you delete it. Remove `Assets/Samples/BugSplat/<old version>/` and import the sample again from Package Manager. Deleting that folder discards everything in it, so copy out anything you changed and want to keep first — most often the `BugSplatOptions` asset, but the scene and scripts too if you edited them.
- **iOS projects exported with Append, or checked into version control, keep their old "Upload dSYM files to BugSplat" build phase.** Unity matches an existing phase on its script body, so the rewritten phase is not recognised as the same one. Delete the old phase and build again, or re-export with Replace. A phase generated before 5.0.0 contains your Client ID and Secret in plain text — **rotate them**.
