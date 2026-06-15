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
| Crash Native | `Event_CrashNative` | Triggers a real native crash captured by the platform's native reporter. On Windows this calls `Utils.ForceCrash(ForcedCrashCategory.AccessViolation)`, which BugSplat's native handler captures. |
| Hang Native | `Event_HangNative` | Blocks the main thread. On Windows this is reported only when `WindowsHangDetectionTimeoutMs` is set to a non-zero value (a detected hang uploads a report and terminates the process). |
| Throw Exception | `Event_ThrowException` | Throws an unhandled managed C# exception, captured via `Application.logMessageReceived`. |
| Catch & Post | `Event_CatchExceptionThenPostNewBugSplat` | Catches an exception and posts it explicitly with custom options via `bugsplat.Post`. |
| Leave Feedback | `Event_LeaveFeedback` | Opens a popup for submitting non-crash user feedback. |
