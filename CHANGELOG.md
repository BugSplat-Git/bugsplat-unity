# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [5.0.0] - Unreleased

Upgrading from 4.x? See [Migrating from 4.x](Documentation~/migrating-from-4x.md).

### Added

- **Native Windows crash reporting** via [bugsplat-windows](https://github.com/BugSplat-Git/bugsplat-windows). Unity P/Invokes the SDK's `BugSplat_*` C API exports from `BugSplat.dll`, so native crashes are captured with **both the Mono and IL2CPP** scripting backends, on x86 (32-bit), x64, and ARM64. Enable it with `BugSplatOptions.UseNativeCrashReportingForWindows`. Binaries come from the official signed bugsplat-windows v8.1.0 release.
- `BugSplatOptions.WindowsShowCrashDialog` — show the BugSplat crash dialog when a native crash occurs on Windows. Defaults to `true`; disable it to upload silently.
- `BugSplatOptions.WindowsHangDetectionTimeoutMs` — out-of-process hang detection for Windows. Defaults to `0` (disabled). When a hang is detected, BugSplat uploads a hang report and terminates the process, so choose a timeout longer than your longest expected frame.
- **Windows Error Reporting coverage** for fail-fast terminations — stack buffer overrun (`0xC0000409`), heap corruption (`0xC0000374`), and `__fastfail` — which bypass every in-process exception filter. Capture requires an `HKLM\...\RuntimeExceptionHelperModules` value naming `BugSplatWer.dll`:
  - `BugSplat.WindowsWerEnabled` reports whether the handler actually registered, and init logs what is lost and how to fix it when it hasn't (a warning in development builds, informational otherwise).
  - **BugSplat > Windows > Register WER Handler**, **Unregister WER Handler**, and **Check WER Handler Registration** write and verify that value elevated for a built player, in both registry views.
- Unsent native Windows crash reports are uploaded automatically at startup; init also attaches `Player.log` and syncs attributes, user, email, key, description, and notes to the native reporter.
- **Capture of unhandled exceptions thrown on background threads.** Unity only raises `logMessageReceived` for main-thread logs, so these were previously written to the player log and never reported. Background exceptions are buffered in a bounded (64-slot) thread-safe queue and posted from the main thread on the next frame, with main-thread logs rejected by thread id so nothing reports twice. On by default; opt out via **Capture Exceptions On Background Threads** on `BugSplatManager`.
- Editor menu for symbol upload credentials: **BugSplat > Symbol Upload > Set Credentials**, **Clear Credentials**, and **Check Credentials**.
- `Description`, `Email`, `Key`, `Notes`, and `User` gained getters — they were previously set-only.
- Continuous integration (`.github/workflows/tests.yml`): the test suite runs on StandaloneLinux64, StandaloneWindows64, StandaloneOSX, and WebGL, plus player-script compile checks for iOS and Android that cover the code behind `!UNITY_EDITOR`, which tests cannot link against.
- CI compiles both Objective-C bridges with clang on a macOS runner, under ARC and manual retain/release. Nothing compiled them before: the Unity matrices run on Linux, which never invokes clang, and Unity only builds a `.mm` at player build time on a Mac — so every bridge change shipped verified by inspection alone. The iOS bridge is compiled against the vendored `BugSplat.xcframework` headers, which is what turns "this property does not exist in the pinned framework" from an invisible break into a failed check. No Unity install or license needed, so it runs in parallel with the licensed matrices and finishes in seconds ([#223](https://github.com/BugSplat-Git/bugsplat-unity/issues/223)).
- Test coverage for the `CreateFromOptions` mapping, `CopyLogTailToTempFile`, the background log message queue, and `BugSplatManager` wiring.
- **Multiple native crash report attachments on macOS and iOS.** `AttachNativeLogFile` is additive and idempotent on every supported platform — attaching one file no longer displaces another, and attaching the same file twice attaches it once. Paths are resolved to full paths before comparison, case-insensitively on Windows. iOS gained native attachment support outright; it was previously a no-op stub. Requires bugsplat-apple 3.5.0 or newer.
- `BugSplat.DetachNativeLogFile` removes a single native attachment and leaves the rest in place. `CapturePlayerLog` uses the same mechanism with `Application.consoleLogPath`, so turning it off detaches only `Player.log`.
- `BugSplatOptions.MacHangDetectionThresholdSeconds` and `IosHangDetectionThresholdSeconds` — how long the main thread must be blocked before BugSplat declares a hang. bugsplat-apple has always supported this and Unity never exposed it, so every Apple player ran at the SDK's 2-second default while Windows had a configurable `WindowsHangDetectionTimeoutMs`. Defaults to 5 seconds rather than 2: a Unity game routinely blocks the main thread for seconds on scene loads and shader warmup, and on macOS a false positive the user then force-quits becomes a fatal hang report for something that was not a hang.
- `BugSplatOptions.UploadDebugSymbolsForWindows` — Windows was the only platform with no symbol upload toggle; the other three each have `UploadDebugSymbolsFor*`. Defaults to `true`, deliberately unlike its siblings: Windows has always uploaded symbols automatically, so defaulting it off would silently stop existing projects symbolicating. It still also requires **Copy PDB Files** and a Windows editor.
- `BugSplatOptions` is grouped into Inspector sections — **BugSplat Account**, **Report Metadata**, **Capture**, **Windows**, **macOS**, **iOS**, **Android** — instead of presenting 26 fields as one flat list. Platforms are ordered by how many customers ship on them: Windows, macOS, iOS, Android. Every pre-existing field keeps its name, so options assets carry their values across the reorder — Unity serializes by name, not declaration order.
- `BugSplatOptions.IosAutoSubmitCrashReport`, `IosAutoSubmitFatalHangReport`, `MacAutoSubmitCrashReport` and `MacAutoSubmitFatalHangReport` — control whether Apple crash and fatal hang reports are submitted without asking the user. Split per platform because the conventions differ: `IosAutoSubmitCrashReport` defaults to `true` (mobile submits silently) while `MacAutoSubmitCrashReport` defaults to `false` (desktop prompts), matching bugsplat-apple's own per-platform defaults. Both fatal hang options default to `true`; turning one off routes that platform's hangs through the same dialog a crash shows, and needs the matching crash option off too. Hang dialogs need a `BugSplat-macOS.dylib` carrying `autoSubmitFatalHangReport`; against an older dylib the option logs a notice and does nothing.
- **bugsplat-apple updated to 3.6.0** (from 3.5.0), which brings the macOS crash dialog fixes and the reporting fix behind the auto-submit hang options:
  - Name and Email fields have real placeholder text instead of empty ones.
  - The standard editing shortcuts — Cmd+A, Cmd+C, Cmd+V, Cmd+X, Cmd+Z, Cmd+Shift+Z — work in the dialog's fields. They previously depended on the host application's Edit menu, which a Unity player does not have, so they were silently dead.
  - `autoSubmitFatalHangReport` exists, so `BugSplatOptions.MacAutoSubmitFatalHangReport` and `IosAutoSubmitFatalHangReport` are now live rather than logging a notice and doing nothing.
  - Crash report expiration reads the persisted timestamp correctly. It previously parsed an ISO-8601 string as a number, so any app setting `expirationTimeInterval` treated every report as expired and submitted it silently. BugSplat for Unity never sets that property, so no Unity integration was affected.
- **Fatal main-thread hang detection on macOS.** Enabled automatically when `UseNativeCrashReportingForMac` is set, matching iOS. A hang that never recovers is uploaded as an `App Hang (Fatal)` report on the next launch — on macOS that means after the beachballing app is force-quit, since nothing there terminates an unresponsive app on its own.
- The `my-unity-crasher` sample is now a platform-aware crash scenario menu, grouped by the mechanism expected to capture each row (`MANAGED`, `NATIVE`, `FAIL-FAST`, `HANG`, `FEEDBACK`), with sections compiled per build target. Native rows are inert in the editor, and `FAIL-FAST` rows grey out when the WER handler isn't registered. No crasher DLL is shipped — every trigger is C# plus P/Invokes into system DLLs.

### Changed

- `BugSplatOptions.PersistentDataFileAttachmentPaths` now attaches its files to **native crash reports** as well as managed reports, on every platform whose native crash reporting is enabled. Previously the list reached managed exception reports, feedback, and minidumps only, so files configured there were silently absent from the native crash reports most users expected them on. The files are handed to the native reporter through the constructor, before it starts: on macOS and iOS a report uploads at the next launch and its attachments are gathered once, while the reporter starts, so anything registered after construction would miss it.

- iOS crash auto-submit is now configurable rather than hard-coded. The iOS bridge always set `autoSubmitCrashReport = YES`; it is now driven by `BugSplatOptions.IosAutoSubmitCrashReport`, which still defaults to `true`, so iOS behavior is unchanged. macOS gets its own `MacAutoSubmitCrashReport`, defaulting to `false`. The split is deliberate: silent on mobile and a prompt on desktop is the platform convention, and it is what bugsplat-apple's own per-platform defaults already encode.
- **Breaking:** the `BugSplat` constructor gained `autoSubmitCrashReport`, `autoSubmitFatalHangReport`, `hangDetectionThresholdSeconds` and `nativeAttachments` parameters after `capturePlayerLog`. All four are optional and default to `null`, meaning "leave bugsplat-apple's own per-platform defaults alone", so a caller that passes nothing gets exactly the behavior it got before. Only code passing arguments positionally past `capturePlayerLog` needs changing.
- The iOS and macOS native bridges now export the same symbol names instead of `Ios`/`Mac`-suffixed pairs. The two plugins are gated to mutually exclusive platforms and can never be compiled into the same binary, so the suffixes bought nothing while forcing every Apple call site in `BugSplat.cs` to be written twice; eight duplicated `#if UNITY_IOS … #elif UNITY_STANDALONE_OSX` branches and one whole `DllImport` block collapsed as a result. These symbols are internal P/Invoke targets, so no public API changed.
- **Breaking:** the package's public types no longer sit in the global namespace, where they were injected into every consumer project. `BuildPostprocessors`, `BugSplatOptionsEditor`, and `BugSplatSymbolUploadCredentials` moved to `BugSplatUnity.Editor`, and `BugSplatRef` moved to `BugSplatUnity.Runtime.Manager`. Unity finds the editor types by attribute, and scenes and prefabs reference scripts by file GUID, so no asset needs re-linking — but code that named these types needs a `using`. The `my-unity-crasher` sample's own scripts moved into its existing `Crasher` namespace for the same reason.
- **Breaking:** `BugSplatRef` is now `internal` and exposes its `BugSplat` property as get-only. It is an implementation detail of `BugSplatManager` and appears nowhere in the public API; use `BugSplatManager.BugSplat` instead.
- **Breaking:** `BugSplatOptions.Attributes` is now `List<BugSplatAttribute>` instead of `Dictionary<string, string>`. Unity cannot serialize a dictionary, so the field could never be authored in the inspector. Unity drops the old serialized value silently when a 4.x options asset is opened.
- **Breaking:** the symbol upload environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET` to `SYMBOL_UPLOAD_CLIENT_ID`/`SYMBOL_UPLOAD_CLIENT_SECRET`, the names the `symbol-upload` CLI already reads. The old names are no longer read.
- **Breaking for coroutines that yield on `Post`:** report uploads are now awaited. `yield return Task.Run(...)` waits a single frame rather than for the task, so `yield return bugsplat.Post(ex); Application.Quit();` lost reports nondeterministically. Those coroutines now genuinely wait for the upload.
- Response parsing and callbacks run on the main thread. They previously ran on a threadpool thread, so any callback touching a Unity API threw.
- Symbol upload credentials are machine-local and per database, resolved from the environment first and then from `~/.bugsplat/credentials/<database>.sh`. The generated Xcode build phase sources that path from `$HOME`, so nothing project-local holds a secret.
- `Post(FileInfo)` works on all platforms for posting your own minidump files. It was previously implemented only on Windows and WSA.
- Editor post-build copies `BugSplatMonitor.exe`, `BugSplatRc.dll`, and `BugSplatWer.dll` next to the built Windows player, with the architecture detected from the executable's PE header, and copies `LineNumberMappings.json` for IL2CPP builds when present.
- Log messages are filtered through the reportable-message check before a coroutine is allocated. Every log message of every type previously allocated one, with the `LogType` filter running inside it.

### Removed

- The orphaned `UNITY_WSA` player-log branch in `DotNetStandardExceptionReporter`. It was the only WSA/UWP code in the package — no options, no README claim, no platform-support row, no CI target — so it read as support that did not exist. Removing it in a release that is already breaking avoids either a needless break later or carrying dead code for two more versions ([#196](https://github.com/BugSplat-Git/bugsplat-unity/issues/196)).

- **Breaking:** `WindowsReporter` and `INativeCrashReporter`. Unity's `CrashReporting.crashReportFolder` minidumps are no longer read or uploaded — native Windows crashes are captured by bugsplat-windows instead.
- **Breaking:** `PostAllCrashes`, `PostCrash`, and `PostMostRecentCrash`. Unsent native crash reports upload automatically at startup, so there is nothing to call at launch. Delete any calls to these methods.
- **Breaking:** `BugSplatOptions.SymbolUploadClientId` and `BugSplatOptions.SymbolUploadClientSecret`. Set credentials from **BugSplat > Symbol Upload > Set Credentials**, or with the environment variables in CI.
- Sample-only, no package API affected: the `ErrorGenerator`, `BugSplatLayoutButtons`, and `PlatformDependentObject` scripts and the `Button_ForceCrash` prefab, all superseded by the scenario menu.

### Fixed

- `CapturePlayerLog` never reached the iOS native reporter, so native iOS crash reports carried no `Player.log`. The attachment delegate was installed before `-start`, but the log path was only registered after the constructor returned — by which point `-start` had already processed the previous session's pending reports and asked the delegate for attachments, with nothing tracked. iOS now registers the log before `-start`, as macOS already did.
- The macOS crash dialog showed a drawn placeholder instead of the BugSplat logo. The dialog resolves its banner against the bundle its own class came from, which works for apps linking `BugSplat.framework` but not for Unity, which ships the SDK as a bare dylib with no resources. The macOS post-build step now copies `bugsplat-logo.png` into the player's `Contents/Resources`, where the lookup finds it.
- Android native crash reporting was never actually enabled — the Android init branch never set the flag every native setter guards on, so all of them silently no-oped, including the attribute-sync callback. Runtime attributes, user, email, key, and notes now reach the native reporter.
- `SetNativeKey` had no iOS or macOS branch, so `Key` never reached Apple native reports. Both now assign bugsplat-apple's `appKey` property directly.
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
