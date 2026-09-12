# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [5.0.0] - Unreleased

Upgrading from 4.x? See [Migrating from 4.x](Documentation~/migrating-from-4x.md).

### Native crash reporting is now bugsplat-native

- **One native reporter on every player platform.** The per-platform plugins — bugsplat-windows on Windows, bugsplat-apple on macOS and iOS, bugsplat-android on Android — are replaced by a single C# binding over [bugsplat-native](https://github.com/BugSplat-Git/bugsplat-native)'s C ABI (`bugsplat.h`). An out-of-process `BugSplatMonitor` built on Crashpad writes the dump while the player is frozen on Windows, macOS, Linux and Android (iOS uses an in-process handler), `BugSplatReporter` shows the crash dialog on desktop, and every report uploads through BugSplat's presigned-URL flow. Linux gains native crash reporting for the first time. Works with Mono and IL2CPP. See [Native crash reporting](Documentation~/native.md).
- **Managed exceptions post through the native SDK in a player**, as BugSplat structured reports (crash type 21): Unity's stack trace is split into function / file / line frames (`UnityStackTraceParser`), `Player.log`, screenshots and attachments ride along, and the post gets a crash id and support-response URL like any other report. The editor and WebGL keep posting over HTTP.
- **One set of native options** on `BugSplatOptions` (and `NativeSettings` in code): `UseNativeCrashReporting` (on by default), `UploadPolicy` (`Dialog` / `Quiet` / `Manual`), `DumpType` (`Normal` / `Heap` / `Full`), `HangDetectionTimeoutMs` + `HangPolicy` (`Report` / `ReportAndTerminate`), `OpenSupportUrl`, `ManagedReportFormat`, plus `UploadDebugSymbolsForLinux`. They replace `UseNativeCrashReportingFor{Windows,Mac,Ios,Android}`, `WindowsShowCrashDialog`, `WindowsHangDetectionTimeoutMs`, and the Apple `*AutoSubmit*` / `*HangDetectionThresholdSeconds` fields.
- **Hang detection everywhere.** `BugSplatManager` sends a heartbeat every frame; when it stops for longer than `HangDetectionTimeoutMs`, the monitor dumps the live process and uploads a hang report. `bugsplat.Heartbeat()`, `WatchThread(name)` and `UnwatchThread()` cover players that do not use the manager or want extra threads watched.
- `bugsplat.CaptureReport()` dumps the live process and reports it like a crash without crashing; `bugsplat.PostPendingReportsAsync()` drains reports left on disk; `bugsplat.Environment` is the OS/hardware string sent as a first-class report property; `NativeCrashReportingEnabled` and `NativeVersion` say whether the runtime started.
- **Attachments can be added at any point in the session on every platform.** `AttachNativeLogFile` / `DetachNativeLogFile` are unchanged in shape, but the monitor now copies the files right after the dump — including on macOS and iOS, which used to need attachments registered before startup.
- **Shipped files.** Windows: `BugSplatMonitor.exe`, `BugSplatReporter.exe`, `BugSplatWer.dll`, `theme/` next to the executable (`BugSplatRc.dll` is gone). macOS: `Contents/Helpers/BugSplatMonitor`, `BugSplatReporter.app`, `theme/`. Linux: the same three next to the player. Android: `libbugsplat.so` + `libBugSplatMonitor.so`. iOS: `libbugsplat.a`. `Editor/PostBuild.cs` places the desktop helpers; `Tools~/fetch-native-runtime.{ps1,sh}` populate the package from a bugsplat-native release. This repository carries the Windows x64 runtime; the other platforms are fetched.
- Symbols on macOS, Linux, iOS and Android upload as Breakpad `.sym` files (`symbol-upload --dumpSyms`). Windows keeps PDBs.
- **Breaking:** `new BugSplat(database, application, version, useNativeCrashReporting = true, capturePlayerLog = true, NativeSettings nativeSettings = null, IEnumerable<string> nativeAttachments = null)` replaces the constructor with per-platform booleans and Apple submission parameters. `SetWindowsCrashDialogEnabled` is `SetCrashDialogEnabled`; `SetWindowsHangDetectionTimeout` is gone (the timeout is a startup option). `AutoSubmitCrashReportSetting` / `AutoSubmitFatalHangReportSetting` are gone.
- The `my-unity-crasher` sample offers the same NATIVE (capture without crashing, null pointer write), HANG and FEEDBACK sections on every platform, plus Windows' FAIL-FAST rows.
- CI no longer compiles Objective-C bridges; there are none. bugsplat-native is built and tested in its own repository.

### Added

- **Windows Error Reporting coverage** for fail-fast terminations — stack buffer overrun (`0xC0000409`), heap corruption (`0xC0000374`), and `__fastfail` — which bypass every in-process exception filter. Capture requires an `HKLM\...\RuntimeExceptionHelperModules` value naming `BugSplatWer.dll`:
  - `BugSplat.WindowsWerEnabled` reports whether the handler actually registered, and init logs what is lost and how to fix it when it hasn't (a warning in development builds, informational otherwise).
  - **BugSplat > Windows > Register WER Handler**, **Unregister WER Handler**, and **Check WER Handler Registration** write and verify that value elevated for a built player, in both registry views.
- **Capture of unhandled exceptions thrown on background threads.** Unity only raises `logMessageReceived` for main-thread logs, so these were previously written to the player log and never reported. Background exceptions are buffered in a bounded (64-slot) thread-safe queue and posted from the main thread on the next frame, with main-thread logs rejected by thread id so nothing reports twice. On by default; opt out via **Capture Exceptions On Background Threads** on `BugSplatManager`.
- **Capture of exceptions from `Task`s that were never awaited.** A faulted `Task` nobody awaits never writes to Unity's log at all, so neither log callback sees it and the failure is invisible. BugSplat now subscribes to `TaskScheduler.UnobservedTaskException` directly. Two things are worth knowing about the timing: the runtime raises this only when a garbage collection notices the faulted `Task`, so reports arrive well after the failure and a `Task` that is never collected is never reported; and BugSplat deliberately does not call `SetObserved()`, since marking the exception observed would suppress whatever your project does with it next. On by default; opt out via **Capture Unobserved Task Exceptions** on `BugSplatManager`.
- Editor menu for symbol upload credentials: **BugSplat > Symbol Upload > Set Credentials**, **Clear Credentials**, and **Check Credentials**.
- `Description`, `Email`, `Key`, `Notes`, and `User` gained getters — they were previously set-only.
- Continuous integration (`.github/workflows/tests.yml`): the test suite runs on StandaloneLinux64, StandaloneWindows64, StandaloneOSX, and WebGL, plus player-script compile checks for iOS and Android that cover the code behind `!UNITY_EDITOR`, which tests cannot link against.
- Test coverage for the `CreateFromOptions` mapping, `CopyLogTailToTempFile`, the background log message queue, and `BugSplatManager` wiring.
- `BugSplat.DetachNativeLogFile` removes a single native attachment and leaves the rest in place. `CapturePlayerLog` uses the same mechanism with `Application.consoleLogPath`, so turning it off detaches only `Player.log`.
- `BugSplatOptions.UploadDebugSymbolsForWindows` — Windows was the only platform with no symbol upload toggle; the other three each have `UploadDebugSymbolsFor*`. Defaults to `true`, deliberately unlike its siblings: Windows has always uploaded symbols automatically, so defaulting it off would silently stop existing projects symbolicating. It still also requires **Copy PDB Files** and a Windows editor.
- The `my-unity-crasher` sample is now a platform-aware crash scenario menu, grouped by the mechanism expected to capture each row (`MANAGED`, `NATIVE`, `FAIL-FAST`, `HANG`, `FEEDBACK`), with sections compiled per build target. Native rows are inert in the editor, and `FAIL-FAST` rows grey out when the WER handler isn't registered. No crasher DLL is shipped — every trigger is C# plus P/Invokes into system DLLs.

### Changed

- `BugSplatOptions.PersistentDataFileAttachmentPaths` now attaches its files to **native crash reports** as well as managed reports, on every platform whose native crash reporting is enabled. Previously the list reached managed exception reports, feedback, and minidumps only, so files configured there were silently absent from the native crash reports most users expected them on. The files are handed to the native reporter through the constructor, before it starts: on macOS and iOS a report uploads at the next launch and its attachments are gathered once, while the reporter starts, so anything registered after construction would miss it.

- **Breaking:** the package's public types no longer sit in the global namespace, where they were injected into every consumer project. `BuildPostprocessors`, `BugSplatOptionsEditor`, and `BugSplatSymbolUploadCredentials` moved to `BugSplatUnity.Editor`, and `BugSplatRef` moved to `BugSplatUnity.Runtime.Manager`. Unity finds the editor types by attribute, and scenes and prefabs reference scripts by file GUID, so no asset needs re-linking — but code that named these types needs a `using`. The `my-unity-crasher` sample's own scripts moved into its existing `Crasher` namespace for the same reason.
- **Breaking:** `BugSplatRef` is now `internal` and exposes its `BugSplat` property as get-only. It is an implementation detail of `BugSplatManager` and appears nowhere in the public API; use `BugSplatManager.BugSplat` instead.
- **Breaking:** `BugSplatOptions.Attributes` is now `List<BugSplatAttribute>` instead of `Dictionary<string, string>`. Unity cannot serialize a dictionary, so the field could never be authored in the inspector. Unity drops the old serialized value silently when a 4.x options asset is opened.
- **Breaking:** the symbol upload environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET` to `SYMBOL_UPLOAD_CLIENT_ID`/`SYMBOL_UPLOAD_CLIENT_SECRET`, the names the `symbol-upload` CLI already reads. The old names are no longer read.
- **Breaking for coroutines that yield on `Post`:** report uploads are now awaited. `yield return Task.Run(...)` waits a single frame rather than for the task, so `yield return bugsplat.Post(ex); Application.Quit();` lost reports nondeterministically. Those coroutines now genuinely wait for the upload.
- Response parsing and callbacks run on the main thread. They previously ran on a threadpool thread, so any callback touching a Unity API threw.
- Symbol upload credentials are machine-local and per database, resolved from the environment first and then from `~/.bugsplat/credentials/<database>.sh`. The generated Xcode build phase sources that path from `$HOME`, so nothing project-local holds a secret.
- `Post(FileInfo)` works on all platforms for posting your own minidump files. It was previously implemented only on Windows and WSA.
- Log messages are filtered through the reportable-message check before a coroutine is allocated. Every log message of every type previously allocated one, with the `LogType` filter running inside it.

### Removed

- The orphaned `UNITY_WSA` player-log branch in `DotNetStandardExceptionReporter`. It was the only WSA/UWP code in the package — no options, no README claim, no platform-support row, no CI target — so it read as support that did not exist. Removing it in a release that is already breaking avoids either a needless break later or carrying dead code for two more versions ([#196](https://github.com/BugSplat-Git/bugsplat-unity/issues/196)).

- **Breaking:** `WindowsReporter` and `INativeCrashReporter`. Unity's `CrashReporting.crashReportFolder` minidumps are no longer read or uploaded — native Windows crashes are captured by bugsplat-windows instead.
- **Breaking:** `PostAllCrashes`, `PostCrash`, and `PostMostRecentCrash`. Unsent native crash reports upload automatically at startup, so there is nothing to call at launch. Delete any calls to these methods.
- **Breaking:** `BugSplatOptions.SymbolUploadClientId` and `BugSplatOptions.SymbolUploadClientSecret`. Set credentials from **BugSplat > Symbol Upload > Set Credentials**, or with the environment variables in CI.
- Sample-only, no package API affected: the `ErrorGenerator`, `BugSplatLayoutButtons`, and `PlatformDependentObject` scripts and the `Button_ForceCrash` prefab, all superseded by the scenario menu.

### Fixed

- `BugSplatOptions.Attributes` were never read by `CreateFromOptions`, so attributes authored on an options asset never reached a report.
- The test assemblies did not compile on any build target: `IClientSettingsRepository.LogFileMaxSizeMB` sat behind `#if !UNITY_WEBGL` while being used unconditionally, and `WebGLClientSettingsRepository` sat behind `#if UNITY_WEBGL` while three test files referenced it unguarded.
- `CopyLogTailToTempFile`'s null guard dereferenced the argument it was checking.
- `BugSplatOptions.PersistentDataFileAttachmentPaths` silently mangled absolute paths. Every entry had its leading separators stripped before being combined with `Application.persistentDataPath`, so `/Users/you/Desktop/test.log` resolved to `<persistentDataPath>/Users/you/Desktop/test.log` — and the warning named *that* path, pointing the reader at a directory they had never typed. An absolute entry is now skipped with a warning quoting it as written and stating that paths are relative to `Application.persistentDataPath`, and every warning in that loop now names both the entry and the path it resolved to. Absolute paths are rejected rather than accepted because they belong to the machine that authored the options asset: they would not exist on a teammate's machine, in CI, or on a player's device, and the sandboxed platforms cannot read outside their own container. Blank entries — the row Unity leaves behind when you click **+** — are skipped instead of warning about a missing file, and a `null` entry no longer throws ([#241](https://github.com/BugSplat-Git/bugsplat-unity/issues/241)).

### Security

- Symbol upload credentials no longer leave the developer's machine. They previously had three exits: inlined into the `/bin/sh` build phase inside `project.pbxproj`, serialized onto the `BugSplatOptions` asset and therefore into version control and shipped builds, and passed as a `--clientSecret` command-line argument visible in process listings.
- **An iOS build phase generated before 5.0.0 contains your Client ID and Secret in plain text — rotate them.** Projects exported with Append, or checked into version control, keep the old "Upload dSYM files to BugSplat" phase; delete it and build again, or re-export with Replace.

## [4.1.0] - 2026-05-22

### Added

- ANR reporting on Android and hang detection on iOS ([#122](https://github.com/BugSplat-Git/bugsplat-unity/pull/122)).

## [4.0.1] - 2026-05-22

### Added

- Native macOS crash reporting via bugsplat-apple ([#117](https://github.com/BugSplat-Git/bugsplat-unity/pull/117)).

### Fixed

- App Store and Google Play compatible SDKs ([#116](https://github.com/BugSplat-Git/bugsplat-unity/pull/116)).
- Android native crash attributes are set after init ([#118](https://github.com/BugSplat-Git/bugsplat-unity/pull/118)).

## [4.0.0] - 2026-03-23

### Added

- `PostFeedback` API for user feedback submission ([#114](https://github.com/BugSplat-Git/bugsplat-unity/pull/114)).

### Changed

- **Breaking:** the minimum supported Unity version is raised to 6000.0 (Unity 6), from 2021.3.

### Fixed

- Log truncation on macOS, Linux, and WSA ([#113](https://github.com/BugSplat-Git/bugsplat-unity/pull/113)).
- README typos ([#104](https://github.com/BugSplat-Git/bugsplat-unity/pull/104)).

## Earlier releases

Releases before 4.0.0 predate this changelog. See the [GitHub releases page](https://github.com/BugSplat-Git/bugsplat-unity/releases) for their notes.

[5.0.0]: https://github.com/BugSplat-Git/bugsplat-unity/compare/v4.1.0...main
[4.1.0]: https://github.com/BugSplat-Git/bugsplat-unity/compare/v4.0.1...v4.1.0
[4.0.1]: https://github.com/BugSplat-Git/bugsplat-unity/compare/v4.0.0...v4.0.1
[4.0.0]: https://github.com/BugSplat-Git/bugsplat-unity/compare/v3.2.2...v4.0.0
