# my-unity-crasher

A sample scene that demonstrates BugSplat crash, exception, hang, and feedback reporting. The scene is a single **Crash Scenarios** menu listing every way BugSplat can capture a failure on the current platform, grouped by the mechanism expected to catch it — because a report arriving is only meaningful if it arrived by the path you were testing.

The menu UI is built entirely in code (`CrashScenarioMenu.cs`); the scenario table lives in `CrashScenarios.cs`. The scene carries only the component and its two button sprites, so adding a scenario never means editing scene YAML.

## Setup

1. Select the `BugSplatOptions` asset and set **Database** to your BugSplat database name (Application and Version are optional and default to your project's product name and version).
2. **Import TMP Essentials.** The sample's UI labels use TextMeshPro. If the text is blank, open **Window > TextMeshPro > Import TMP Essential Resources**. TextMeshPro can't render without its default font asset, which is imported per-project and can't be bundled inside a sample.

## Dependencies

The sample uses the built-in UI package (`com.unity.ugui`, which also provides TextMeshPro) and the **Input System package** (`com.unity.inputsystem`) for UI input — the EventSystem uses an `InputSystemUIInputModule`. It does **not** require the Universal Render Pipeline.

## Scenarios by platform

The menu is platform-aware: sections compile for the active build target, so each player only offers the scenarios its platform can capture.

| Section | Captured by | Platforms |
| --- | --- | --- |
| `MANAGED` | The .NET handler: unhandled exception, coroutine exception, background-thread exception, unobserved `Task` exception, caught & posted manually | All |
| `NATIVE` | The platform's native crash reporter. Windows: access violations (write, read, background thread), custom SEH exception, and stack overflow, dumped out of process by `BugSplatMonitor`. macOS, iOS, and Android: a native crash raised in the platform bridge | Windows, macOS, iOS, Android |
| `FAIL-FAST` | `BugSplatWer.dll` via Windows Error Reporting: fail-fast `0xC0000602`, stack buffer overrun `0xC0000409`, heap corruption `0xC0000374`. These bypass every in-process handler | Windows |
| `HANG` | Windows: `BugSplatMonitor`'s watchdog. macOS: BugSplat's main-thread watchdog, fatal only once you force-quit the beachballing app (reported next launch). iOS: the OS watchdog (reported next launch). Android: an ANR raised by the OS | Windows, macOS, iOS, Android |
| `FEEDBACK` | An explicit `bugsplat.PostFeedback`, via the feedback dialog | All |

Linux and WebGL players get the `MANAGED` and `FEEDBACK` sections only; native crash reporting is not yet supported there, and the menu's status line says so.

## Things worth knowing before you run these

- **Native scenarios are disabled in the editor.** The native reporters are excluded from the editor, so there is no reporter there, and the crash would take the editor down with any unsaved work. The rows shown follow the active build target; build a player to run them. Managed and feedback scenarios run fine in play mode.
- **The sample opts in to play mode uploads.** `PostExceptionsInEditor` defaults to false as of 5.0.0, so a fresh integration doesn't upload play mode exceptions — the sample's `BugSplatOptions` asset enables it explicitly so the managed scenarios report from the editor. Check the box on your own options asset (or set `bugsplat.PostExceptionsInEditor = true`) to do the same.
- **The status line tells you what to expect.** On Windows it reports whether the WER handler is armed. When it isn't, the `FAIL-FAST` rows are greyed out and say so, because running them would terminate the player and report nothing. Register the handler with **BugSplat > Windows > Register WER Handler**, or see [Windows Error Reporting](../../Documentation~/windows.md#windows-error-reporting).
- **Turn off Error Pause in the Console before running these in the editor.** Every managed scenario logs an exception on purpose, and Error Pause halts play mode on each one — which looks like the player crashing rather than surviving, and with **Maximize on Play** it can drop the Game view back to its docked size so this menu appears to vanish. The Play button stays lit the whole time, so it is easy to misread as a bug in the SDK.
- **Run without a debugger attached.** A fail-fast breaks into an attached debugger instead of reporting.
- **Some Windows scenarios behave differently on Mono.** The access violations deliberately fault inside `RtlMoveMemory` rather than dereferencing null from C#: Mono's exception handler claims faults that occur in JIT'd managed code and converts them to a managed `NullReferenceException`, so a plain null write is a caught exception rather than a crash. Stack overflow has no such workaround — Mono guards the stack and raises a managed `StackOverflowException`, so on Mono expect a managed report (or nothing) rather than a native crash. The row says which to expect for the backend you built with. All of these produce real native crashes under IL2CPP, which is the recommended backend.
- **The sample throttles reports to one per 7 seconds.** `BugSplatSettings.cs` sets `ShouldPostException` to demonstrate [preventing repeated reports](../../Documentation~/usage.md#preventing-repeated-reports). Clicking several managed scenarios in a row drops the later ones with a warning in the log — easy to mistake for a missing report.
- **The background-thread exception should report once.** Seeing it twice means the main-thread deduplication in `BackgroundLogMessageQueue` regressed.
- **Every `NATIVE`, `FAIL-FAST`, and `HANG` scenario kills the player** — the section subtitles say so. Relaunching is part of the test: unsent reports upload on the next launch. macOS's hang row is the one exception: it freezes the player instead of killing it, and nothing is reported until you force-quit it yourself.
- **The spinning cube is the liveness indicator.** After a `MANAGED` scenario, the cube still turning is the proof the player survived.
- Windows scenarios set a distinct `Key` before crashing, since the fail-fast rows all fault at the same address and would otherwise group into one bucket in the dashboard.
- `UnityEngine.Diagnostics.Utils.ForceCrash` is deliberately not used anywhere in this sample. It routes through Unity's own diagnostics pipeline rather than raising a clean fault, so what the crash handler sees is inconsistent across Unity versions and scripting backends.
