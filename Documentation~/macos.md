[&larr; BugSplat for Unity](../README.md)

# 🖥 macOS

The bugsplat-unity plugin supports native crash reporting on macOS via [bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple), which uses PLCrashReporter to capture crashes. It works with both the **Mono** and **IL2CPP** scripting backends, on Apple silicon and Intel, and with **CoreCLR** players (Unity 7.0 alpha). IL2CPP gives better reports: see [Mono players](#mono-players) and [CoreCLR players](#coreclr-players).

`UseNativeCrashReportingForMac` is on by default. To upload symbols as well, set `UploadDebugSymbolsForMac` to `true` on your `BugSplatOptions` asset. BugSplat then uploads the build's dSYMs, plus the dSYM of its own prebuilt bridge, `BugSplatBridgeMac.dylib`, which comes from the package. For IL2CPP builds it also uploads Unity's `LineNumberMappings.json` for full symbolication. The mappings file is uploaded as `LineNumberMappings-macos.json`, so a macOS build never overwrites the mappings of an iOS or Windows build that shares its version; the file Unity wrote is left as it is. Unity only generates `LineNumberMappings.json` when **Edit > Project Settings > Player > Other Settings > IL2CPP Stacktrace Information** is set to **Method Name, File Name, and Line Number**; without it, the build log warns and names the setting.

`Player.log` is attached to native macOS crash reports when both `UseNativeCrashReportingForMac` and `CapturePlayerLog` are enabled on your `BugSplatOptions` asset. Managed .NET exception reports attach it through the reporter instead, so they are unaffected by the native setting.

A native crash report uploads at the **next launch**, and by then Unity has renamed the crashed session's log to `Player-prev.log` and started a fresh `Player.log`. BugSplat therefore reads `Player-prev.log` for that report; when the SDK provides the crashed session ID it verifies the file identity before attaching, and otherwise it attaches on best effort. Reports that fail to upload keep their copy of the log and retry it later. The one case with no log: the app crashes again before BugSplat initializes, so Unity's rotation replaces the file first — the report then carries no `Player.log` rather than a misleading one.

When `UseNativeCrashReportingForMac` is enabled, the post-build step also copies `bugsplat-logo.png` into the built player's `Contents/Resources`. The crash dialog looks up its banner in the app bundle, so without that file it falls back to a plain drawn logo. Xcode project exports are skipped — add the file to your Xcode target's resources yourself if you want the logo there.

## Mono players

Mono implements `NullReferenceException` with a signal handler: a null dereference faults, and Mono's handler turns the fault into the managed exception. bugsplat-apple's default Mach exception handler would see each of those faults first and write a crash report for it, so in a Mono player BugSplat sets bugsplat-apple to use BSD signal handlers and installs Mono's handlers again on top of them. Mono then handles its own faults and passes every other crash on to BugSplat. Caught `NullReferenceException`s are not reported, and an uncaught one is reported once, as a C# exception. IL2CPP players keep the Mach exception handlers.

What differs from IL2CPP:

- **C# frames are addresses.** Mono compiles C# at run time and has no `LineNumberMappings.json`, so the C# frames of a native crash show as `???` with an address, without method names, files or lines. Native frames (Unity, the OS, your native plugins) are symbolicated as usual. For C# method names, file names and line numbers in native crashes, build with IL2CPP.
- **No Mono crash dump in Player.log.** Before passing a crash on, Mono normally writes its own crash dump, with a managed stack trace, to `Player.log`. On a thread Mono does not know about, such as Unity's job, render and audio threads, that dump fails and the process exits before any crash report is written. BugSplat therefore has Mono pass native crashes on straight away, and `Player.log` no longer gets Mono's dump.
- **Stack overflows in C# are not reported.** Mono's signal handlers don't run on an alternate signal stack, so when C# recursion exhausts the stack, macOS has nowhere to run any crash handler and ends the process at once. A Mono player without BugSplat dies the same way. IL2CPP players report these crashes.
- **A few milliseconds at startup.** Between BugSplat taking over Mono's signals and Mono getting them back, while BugSplat starts, a null dereference in C# on another thread would crash the player instead of throwing.
- **Other signal handlers.** Under Mono, crashes reach BugSplat through signal handlers, so a native plugin that installs its own crash signal handlers after BugSplat initializes gets each crash first. Unless it passes the signals it does not handle on to the handler it replaced, those crashes never reach BugSplat. IL2CPP players are less exposed: there BugSplat catches hardware faults such as `SIGSEGV` and `SIGBUS` with a Mach exception handler, which runs before any signal handler. If you write such a plugin, chain unhandled signals to the previous handler.

## CoreCLR players

Unity 7.0 alpha builds macOS players with the CoreCLR scripting backend. BugSplat reports their native crashes and main-thread hangs with bugsplat-apple's Mach exception handlers, as in IL2CPP players. The CoreCLR runtime handles a null dereference in C# before bugsplat-apple sees the fault, so a `NullReferenceException` is not reported as a native crash.

What differs from IL2CPP:

- **C# frames are addresses.** CoreCLR compiles C# at run time, so the C# frames of a native crash or hang show as `???` with an address, without method names, files or lines. Native frames (Unity, the OS, your native plugins) are symbolicated as usual.
- **`Environment.FailFast` and stack overflows in C# are not reported.** The runtime ends the process itself in both cases, and writes the C# stack trace to `Player.log`.

Tested with Unity 7000.0.0a20 on Apple silicon.

## Attachments

A native crash report is uploaded at the **next launch**, not at crash time, and BugSplat asks for its attachments then — in a fresh process that did not experience the crash. A file registered with `AttachNativeLogFile` part-way through a session is therefore not remembered across the crash and never reaches the report.

Register native attachments during initialization instead. `BugSplatOptions.PersistentDataFileAttachmentPaths` is applied on every launch and is unaffected by this. Each attachment is truncated to its last 10 MB. See [Attaching Files to Native Crash Reports](api.md#attaching-files-to-native-crash-reports).

## Testing Without the Debugger

A debugger claims the Mach exception ports before PLCrashReporter does, so **a crash that happens under the Xcode debugger never reaches BugSplat**. Hang detection is suppressed outright while a debugger is attached. Xcode's **Build And Run** leaves the debugger on, so a crash triggered that way produces no report and no explanation.

Two ways to test:

- Uncheck **Product > Scheme > Edit Scheme > Run > Info > Debug executable**, then Run. Only the Run action changes; Test and Profile are unaffected.
- Or launch the app from the Finder instead of from Xcode.

Either way the report uploads on the **next** launch, not at crash time, so relaunch the app after the crash.

## Hang Detection

When `UseNativeCrashReportingForMac` is enabled, BugSplat also detects fatal main-thread hangs. No additional configuration is required. If the main thread stalls past the detection threshold and the app is subsequently terminated without recovering, BugSplat uploads an `App Hang (Fatal)` report on the next launch. Hangs the app recovers from are not reported.

The detection threshold is `MacHangDetectionThresholdSeconds`, 5 seconds by default. That is higher than bugsplat-apple's own 2-second default because a Unity game routinely blocks the main thread for seconds at a time — scene loads, shader warmup, synchronous asset loads — and each of those is a false positive waiting to happen. Lower it if your game genuinely never stalls that long.

By default a hang report is uploaded without asking, because the user never had the chance to consent — the app was frozen, then terminated. Turn off `MacAutoSubmitFatalHangReport` on your `BugSplatOptions` asset to ask them instead: the report then goes through the same dialog a native crash shows, so they can describe what the app was doing when it froze. That also needs `MacAutoSubmitCrashReport` off, since it is what decides whether any dialog appears. Both options map onto bugsplat-apple's `autoSubmitCrashReport` and `autoSubmitFatalHangReport` — the Unity names carry a `Mac` prefix because iOS has its own pair — and require a `BugSplat-macOS.dylib` carrying `autoSubmitFatalHangReport`; against an older one the option logs a notice and hang reports keep uploading without asking.

Unlike iOS, macOS has no OS watchdog that terminates an unresponsive app — it beachballs indefinitely — so the only way a hang becomes fatal is a force quit (Option-Command-Escape, Activity Monitor, or a `kill`). Detection is also suppressed while a debugger is attached, so test hang reporting on a built player run outside Xcode.
