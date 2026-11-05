# my-unity-crasher

A sample scene that demonstrates BugSplat crash, exception, hang, capture, and feedback reporting. The scene is a single **Crash Scenarios** menu listing every way BugSplat can capture a failure on the current platform, grouped by the mechanism expected to catch it — because a report arriving is only meaningful if it arrived by the path you were testing.

The menu UI is built entirely in code (`CrashScenarioMenu.cs`); the scenario table lives in `CrashScenarios.cs`. The scene carries only the component and its two button sprites, so adding a scenario never means editing scene YAML.

## Setup

1. [Sign up](https://app.bugsplat.com/v2/sign-up) for BugSplat if you haven't already. Open **Edit > Project Settings > BugSplat**, click **Create** (or pick your project's existing asset), and set **Database** to the name of a database from your BugSplat account. Application and Version are optional and default to your project's product name and version. The sample deliberately ships no options asset of its own, so it reports to your database and never adds a second asset to your project.
2. On that asset, check **Post Exceptions In Editor** so the managed scenarios report from play mode, and every platform under **Upload Debug Symbols For** so player builds produce symbolicated stacks. Both are explained below.
3. **Import TMP Essentials.** The sample's UI labels use TextMeshPro. If the text is blank, open **Window > TextMeshPro > Import TMP Essential Resources**. TextMeshPro can't render without its default font asset, which is imported per-project and can't be bundled inside a sample.

## Dependencies

The sample uses the built-in UI package (`com.unity.ugui`, which also provides TextMeshPro) and the **Input System package** (`com.unity.inputsystem`) for UI input — the EventSystem uses an `InputSystemUIInputModule`. It does **not** require the Universal Render Pipeline.

## Scenarios by platform

The menu is platform-aware: sections compile for the active build target. Because every player platform runs the same native reporter, the `NATIVE`, `HANG` and `FEEDBACK` sections are the same everywhere; Windows adds the fail-fast rows that only Windows Error Reporting can deliver.

| Section | Captured by | Platforms |
| --- | --- | --- |
| `MANAGED` | The .NET handler: unhandled exception, coroutine exception, background-thread exception, unobserved `Task` exception, caught & posted manually. In a player these post through bugsplat-native as structured reports | All |
| `NATIVE` | bugsplat-native. `Capture report (no crash)` dumps the live process and keeps running. Windows: access violations (write, read, background thread), custom SEH exception, stack overflow. Other platforms: a null pointer write (`SIGSEGV` / `EXC_BAD_ACCESS`), on the main thread and on a background thread | Windows, macOS, Linux, Android, iOS |
| `FAIL-FAST` | `BugSplatWer.dll` via Windows Error Reporting: fail-fast `0xC0000602` and stack buffer overrun `0xC0000409`, which bypass every in-process handler. Heap corruption `0xC0000374` is caught in process by bugsplat-native and reports with or without WER | Windows |
| `HANG` | bugsplat-native's watchdog: blocks the main thread for 15 s; with `HangDetectionTimeoutMs` set (5000, say) the monitor dumps the live process and uploads a hang report while the thread is still blocked | Windows, macOS, Linux, Android, iOS |
| `FEEDBACK` | An explicit `bugsplat.PostFeedback`, via the feedback dialog | All but WebGL |

WebGL players get the `MANAGED` section only, without the background-thread and unobserved `Task` rows that a single-threaded player can't run, and the menu's status line says so.

## Things worth knowing before you run these

- **Native scenarios are disabled in the editor.** bugsplat-native does not run inside the editor, so there is no reporter there, and the crash would take the editor down with any unsaved work. The rows shown follow the active build target; build a player to run them. Managed and feedback scenarios run fine in play mode.
- **Play mode uploads are off until you opt in.** `PostExceptionsInEditor` defaults to false as of 5.0.0, so a fresh integration doesn't upload play mode exceptions. Without it the managed scenarios run but report nothing from the editor; check the box on your options asset (or set `bugsplat.PostExceptionsInEditor = true`).
- **Turn on symbol upload for every platform.** With `UploadDebugSymbolsFor` Windows, macOS, Linux, iOS and Android checked on your options asset, a player build uploads symbols and the dashboard shows symbolicated C# and native stacks — which is most of the point of running these scenarios. Symbol upload needs credentials, and they are deliberately not stored on the asset: set them once per database with **BugSplat > Symbol Upload > Set Credentials**, or with the `SYMBOL_UPLOAD_CLIENT_ID` / `SYMBOL_UPLOAD_CLIENT_SECRET` environment variables. Without them the build **warns and still succeeds**, so you get a player either way — just an unsymbolicated one. Windows additionally needs **Copy PDB Files** in Build Settings and a Windows editor. See [Symbol Upload](../../Documentation~/symbol-upload.md).
- **Set Hang Detection Timeout Ms for the `HANG` row.** It defaults to 0 (off), so nothing notices the hang; 5000 suits the sample's 15 s block.
- **The status line tells you what to expect.** It reports whether native crash reporting started (and the bugsplat-native version), and on Windows whether the WER handler is armed. When native reporting did not start, the `NATIVE`, `FAIL-FAST`, and `HANG` rows are greyed out, because they would otherwise terminate the player and report nothing; the reason is in the player log — almost always Use Native Crash Reporting unchecked, or `BugSplatMonitor` or `BugSplatReporter` missing next to the player. When WER isn't armed, the `FAIL-FAST` rows are greyed out and say so; register the handler with **BugSplat > Windows > Register WER Handler**, or see [Windows Error Reporting](../../Documentation~/windows.md#windows-error-reporting).
- **Turn off Error Pause in the Console before running these in the editor.** Every managed scenario logs an exception on purpose, and Error Pause halts play mode on each one — which looks like the player crashing rather than surviving, and with **Maximize on Play** it can drop the Game view back to its docked size so this menu appears to vanish. The Play button stays lit the whole time, so it is easy to misread as a bug in the SDK.
- **Run without a debugger attached.** A fail-fast breaks into an attached debugger instead of reporting.
- **Some scenarios behave differently on Mono.** The Windows access violations deliberately fault inside `RtlMoveMemory`, and the POSIX null pointer write inside the marshaling layer's native copy: Mono's fault handler claims faults that occur in JIT'd managed code and converts them to a managed `NullReferenceException`, so a plain null write from C# is a caught exception rather than a crash. Stack overflow has no such workaround — Mono guards the stack and raises a managed `StackOverflowException`. All of these produce real native crashes under IL2CPP, which is the recommended backend.
- **The sample throttles reports to one per 7 seconds.** `BugSplatSettings.cs` sets `ShouldPostException` to demonstrate [preventing repeated reports](../../Documentation~/usage.md#preventing-repeated-reports).
- **The background-thread exception should report once.** Seeing it twice means the main-thread deduplication in `BackgroundLogMessageQueue` regressed.
- **Every `NATIVE` crash and `FAIL-FAST` scenario kills the player** — the section subtitles say so. On desktop the BugSplat dialog appears at crash time; on Android and iOS the report uploads on the next launch, so relaunching is part of the test. `Capture report` and `HANG` (with the default `HangPolicy.Report`) leave the player running.
- **The spinning cube is the liveness indicator.** After a `MANAGED`, capture, or hang scenario, the cube still turning is the proof the player survived.
- Scenarios set a distinct `Key` before crashing, since the fail-fast rows all fault at the same address and would otherwise group into one bucket in the dashboard.
- `UnityEngine.Diagnostics.Utils.ForceCrash` is deliberately not used anywhere in this sample. It routes through Unity's own diagnostics pipeline rather than raising a clean fault, so what the crash handler sees is inconsistent across Unity versions and scripting backends.
