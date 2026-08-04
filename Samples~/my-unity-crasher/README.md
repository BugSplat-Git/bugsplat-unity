# my-unity-crasher

A sample scene that demonstrates BugSplat crash, exception, hang, and feedback reporting. Each button triggers a different kind of report.

## Setup

1. Select the `BugSplatOptions` asset and set **Database** to your BugSplat database name (Application and Version are optional and default to your project's product name and version).
2. **Import TMP Essentials.** The sample's UI labels use TextMeshPro. If the button text is blank, open **Window > TextMeshPro > Import TMP Essential Resources**. TextMeshPro can't render without its default font asset, which is imported per-project and can't be bundled inside a sample.

## Dependencies

The sample uses the built-in UI package (`com.unity.ugui`, which also provides TextMeshPro) and the **Input System package** (`com.unity.inputsystem`) for UI input — the EventSystem uses an `InputSystemUIInputModule`. It does **not** require the Universal Render Pipeline.

## Buttons

| Button | Method | What it does |
| --- | --- | --- |
| Crash Native | `Event_CrashNative` | Triggers a real native crash captured by the platform's native reporter. On Windows this writes through a null pointer from a chain of C# frames, producing a hardware access violation rather than a managed `NullReferenceException`. |
| Hang Native | `Event_HangNative` | Blocks the main thread. On Windows this is reported only when `WindowsHangDetectionTimeoutMs` is set to a non-zero value (a detected hang uploads a report and terminates the process). |
| Throw Exception | `Event_ThrowException` | Throws an unhandled managed C# exception, captured via `Application.logMessageReceived`. |
| Catch & Post | `Event_CatchExceptionThenPostNewBugSplat` | Catches an exception and posts it explicitly with custom options via `bugsplat.Post`. |
| Leave Feedback | `Event_LeaveFeedback` | Opens a popup for submitting non-crash user feedback. |

## Crash Scenarios menu (Windows)

The **Crash Scenarios** button in the bottom-right opens a scrollable list covering every way BugSplat can capture a failure. Each row is tagged with the mechanism expected to catch it, because a report arriving is only meaningful if it arrived by the path you were testing:

| Tag | Captured by | Scenarios |
| --- | --- | --- |
| `NATIVE` | BugSplat's crash handler, dumped out of process by `BugSplatMonitor` | Access violation (write, read, background thread), custom SEH exception, stack overflow |
| `WER` | `BugSplatWer.dll` via Windows Error Reporting | Fail-fast `0xC0000602`, fail-fast as stack buffer overrun `0xC0000409`, heap corruption `0xC0000374` |
| `MANAGED` | The .NET handler, via Unity's log callbacks | Unhandled exception, exception inside a coroutine, exception on a background thread |
| `POST` | An explicit `bugsplat.Post` call | Caught exception posted manually |
| `HANG` | `BugSplatMonitor`'s hang watchdog | Main-thread hang |
| `NONE` | Not captured by the SDK | Unobserved `Task` exception |

A few things worth knowing before you run these:

- **Native scenarios are disabled in the editor.** `BugSplat.dll` is excluded from the editor, so there is no reporter there, and the crash would take the editor down with any unsaved work. Build a Windows player. Managed scenarios run fine in play mode.
- **`WER` scenarios need the handler registered** or they produce no report at all — which is itself worth testing. The menu's status line tells you which state you're in. See [Windows Error Reporting](../../../README.md#windows-error-reporting).
- **Run without a debugger attached.** A fail-fast breaks into an attached debugger instead of reporting.
- **Anything marked `(terminates)` kills the player.** Relaunching is part of the test: unsent reports upload on the next launch.
- Scenarios set a distinct `Key` before crashing, since the fail-fast rows all fault at the same address and would otherwise group into one bucket in the dashboard.
- The background-thread exception should report **once**. Seeing it twice means the main-thread deduplication in `BackgroundLogMessageQueue` regressed.
- `UnityEngine.Diagnostics.Utils.ForceCrash` is deliberately not used anywhere in this sample. It routes through Unity's own diagnostics pipeline rather than raising a clean fault, so what the crash handler sees is inconsistent across Unity versions and scripting backends.
