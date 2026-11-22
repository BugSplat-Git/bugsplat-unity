[&larr; BugSplat for Unity](../README.md)

# 🐧 Linux

Native Linux crash reporting comes from [bugsplat-native](native.md): an out-of-process `BugSplatMonitor` built on Crashpad captures the crash (`SIGSEGV`, `SIGABRT`, `SIGBUS`, `SIGFPE`, `SIGILL`, `SIGTRAP`, uncaught C++ exceptions) and writes the minidump while the player is frozen, and `BugSplatReporter` shows the BugSplat dialog (GTK 3, loaded at runtime) and uploads. Without GTK or a display the same process uploads quietly.

`UseNativeCrashReporting` is on by default. When it is on:

- Native crashes are captured at crash time and uploaded immediately, or on the next launch if the user is offline.
- `Player.log` and your attachments are copied into the report by the monitor.
- At build time, BugSplat copies `BugSplatMonitor`, `BugSplatReporter`, `bugsplat-logo.png`, `bugsplat-logo-dark.png` and `theme/` next to `<Game>.x86_64`. Ship them with your game. A build made on Windows has no execute bits, and a zip made there stores none; `bugsplat_init` sets them again on the helpers when the game's folder is writable.
- Managed exceptions post through the same SDK as structured reports.

Mono turns faults in managed code into `NullReferenceException` from its signal handlers, so on Mono bugsplat-native passes each fault to Mono first, and only real native faults become crash reports. IL2CPP null-checks in generated code instead, and the handler before bugsplat-native's is the player's own crash handler, which exits without returning, so IL2CPP players don't chain. Both runtimes' garbage collector uses `SIGXCPU` to restart threads; bugsplat-native leaves that signal, and `SIGXFSZ`, to the runtime.

## Requirements

- glibc 2.34 or newer (Ubuntu 22.04, Debian 12, RHEL 9).
- `libcurl4` and `ca-certificates` for uploads; `libgtk-3-0` for the dialog. Without GTK or a display the report uploads without one.
- A Unity player that isn't a Dedicated Server build crashes inside Unity at startup when there's no display at all (`InputProcess`, Unity 6000.5), even with `-batchmode -nographics`. Run servers as Dedicated Server builds, or under a virtual display.

## Hang detection

Linux has no portable main loop for the SDK to probe, so the heartbeat BugSplat sends every frame is what the watchdog watches. Set `HangDetectionTimeoutMs` on the options asset; the monitor dumps the live process and uploads a hang report when the heartbeat stops for longer than that.

## Symbols

Set `UploadDebugSymbolsForLinux` to upload the build's `.so` and player binaries as Breakpad `.sym` files (`symbol-upload --dumpSyms`). The shipped binaries are stripped, so each one's `.sym` is merged with its split debug file before upload: `GameAssembly.debug`, `UnityPlayer_s.debug` and `<Game>_s.debug` from `<Game>_BackUpThisFolder_ButDontShipItWithYourGame` on IL2CPP, and Unity's own `UnityPlayer_s.debug` from the editor's Linux support on Mono. IL2CPP builds also upload `LineNumberMappings.json` as `LineNumberMappings-linux.json`, so frames in your C# code show its file and line. See [Symbol Upload](symbol-upload.md) for credentials.
