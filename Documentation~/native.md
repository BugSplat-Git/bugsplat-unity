[&larr; BugSplat for Unity](../README.md)

# 🧱 Native crash reporting

Version 5.0 of `com.bugsplat.unity` is a C# binding over [bugsplat-native](https://github.com/BugSplat-Git/bugsplat-native), BugSplat's cross-platform crash reporter. One library (`bugsplat.h`, loaded as `BugSplat.dll` / `libbugsplat.dylib` / `libbugsplat.so`, or linked statically on iOS) does the same job on Windows, macOS, Linux, Android and iOS, so the options, the behavior and the report contents are the same everywhere.

## What happens in a player

- **Native crashes** — access violations, `SIGSEGV`/`EXC_BAD_ACCESS`, aborts, stack overflows, C++ exceptions, heap corruption — are captured by an out-of-process **`BugSplatMonitor`** that Unity's player launches at startup (on iOS the handler runs in process). The monitor writes the minidump while the player is frozen, so the dump is exactly the state at the crash.
- **Managed exceptions** are captured by the .NET handler as before and posted through the same native SDK as **structured reports** (crash type 21): the Unity stack trace is split into function / file / line frames, and `Player.log`, screenshots and your attachments ride along.
- **Hangs**: with `HangDetectionTimeoutMs` set, `BugSplatManager` sends a heartbeat every frame; when it stops for longer than the timeout, the monitor dumps the live process and uploads a hang report (`reportKind=hang`). `HangPolicy.Report` lets the game continue; `ReportAndTerminate` offers the user Wait / Close.
- **Captures**: `bugsplat.CaptureReport()` dumps the live process and reports it like a crash, without crashing.
- **Feedback**: `bugsplat.PostFeedback(...)` uploads a user feedback report.
- **Upload**: every report goes through BugSplat's presigned-URL upload. On desktop, **`BugSplatReporter`** shows the crash dialog (`UploadPolicy.Dialog`), uploads quietly (`Quiet`), or leaves the report on disk for `PostPendingReportsAsync` (`Manual`). Reports that could not be sent — offline, or the app was killed first — go out on the next launch.
- **Metadata**: `User`, `Email`, `Key`, `Description`, `Notes`, `Environment` and every `Attributes` entry are mirrored into the native SDK as they change, so the value at the instant of a crash is what the report carries. Attachments can be added and removed at any time; the monitor copies them right after the dump.

## The files next to your player

`bugsplat_init` refuses to start without its helpers, so the package copies them next to the built player automatically (`Editor/PostBuild.cs`). **Ship them with your game.**

| Platform | Library (Unity places it) | Helpers (post-build copies them) |
|---|---|---|
| Windows | `<Game>_Data/Plugins/x86_64/BugSplat.dll` | `BugSplatMonitor.exe`, `BugSplatReporter.exe`, `theme/` next to `<Game>.exe`; `BugSplatWer.dll` next to `BugSplat.dll` |
| macOS | `<Game>.app/Contents/PlugIns/libbugsplat.dylib` | `Contents/Helpers/BugSplatMonitor`, `BugSplatReporter.app`, `theme/` |
| Linux | `<Game>_Data/Plugins/x86_64/libbugsplat.so` | `BugSplatMonitor`, `BugSplatReporter`, `theme/` next to `<Game>.x86_64` |
| Android | `lib/<abi>/libbugsplat.so`, `libBugSplatMonitor.so` in the APK | none |
| iOS | linked into `UnityFramework` | none |

If the runtime for a platform is missing from the package, the build logs an error naming the folder and `bugsplat.NativeCrashReportingEnabled` is `false` at runtime with the reason in the player log; managed exception reporting keeps working over HTTP. `Tools~/README.md` describes the layout and the `fetch-native-runtime` scripts that populate it from a bugsplat-native release.

The `theme/` folder is BugSplatReporter's dialog theme (`theme.json`, `strings.<locale>.json`). Edit it to brand the crash dialog; see bugsplat-native's `reporter/docs/THEME.md`.

## Options

All of these live under **Native Crash Reporting** on the `BugSplatOptions` asset and map to `NativeSettings`, which you can also pass to the `BugSplat` constructor in code.

| Option | Default | Meaning |
|---|---|---|
| `UseNativeCrashReporting` | `true` | Start bugsplat-native in players. Off: only the .NET handler runs, posting over HTTP as 4.x did. |
| `UploadPolicy` | `Dialog` | `Dialog` shows the BugSplat dialog on desktop (mobile uploads next launch); `Quiet` uploads silently; `Manual` leaves reports for `PostPendingReportsAsync`. |
| `DumpType` | `Normal` | `Heap` adds the managed heaps (objects resolve in a debugger); `Full` is every readable region. Size is enforced by your database's limit at upload. |
| `HangDetectionTimeoutMs` | `0` (off) | Longer than your longest frame, or loading screens become hang reports. |
| `HangPolicy` | `Report` | `ReportAndTerminate` also offers Wait / Close and ends the game on Close. |
| `OpenSupportUrl` | `true` | Open the support response after an interactive upload on desktop. |
| `ManagedReportFormat` | `Xml` | `Json` needs a server that accepts JSON structured reports. |

Two things are not options: the crash type (Windows players report as UnityNative, 15, so IL2CPP frames symbolicate through `LineNumberMappings.json`; every other platform uses the SDK's Crashpad type), and the `Environment` string, which the SDK detects and you may override on the instance.

## In the editor and on WebGL

bugsplat-native does not run inside the Unity editor, and WebGL has no native code. There, managed exceptions post directly over HTTP through `BugSplatDotNetStandard`, exactly as in 4.x, and every native call is a no-op: `NativeCrashReportingEnabled` is `false`, `CaptureReport()` returns `false`, `AttachNativeLogFile` does nothing. `PostExceptionsInEditor` gates the editor path as before.

## Symbols

Windows keeps uploading `.pdb`, `.dll` and `.exe` files. Every other platform is symbolicated from Breakpad `.sym` files, which `symbol-upload --dumpSyms` produces from dSYMs, ELF debug files and `.so` files, so the macOS, Linux, iOS and Android uploads all pass `--dumpSyms`. IL2CPP's `LineNumberMappings.json` is uploaded on Windows, macOS and iOS as before.
