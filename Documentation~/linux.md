[&larr; BugSplat for Unity](../README.md)

# 🐧 Linux

Native Linux crash reporting comes from [bugsplat-native](native.md): an out-of-process `BugSplatMonitor` built on Crashpad captures the crash (`SIGSEGV`, `SIGABRT`, `SIGBUS`, `SIGFPE`, `SIGILL`, `SIGTRAP`, uncaught C++ exceptions) and writes the minidump while the player is frozen, and `BugSplatReporter` shows the BugSplat dialog (GTK 3, loaded at runtime) and uploads. Without GTK or a display the same process uploads quietly.

`UseNativeCrashReporting` is on by default. When it is on:

- Native crashes are captured at crash time and uploaded immediately, or on the next launch if the user is offline.
- `Player.log` and your attachments are copied into the report by the monitor.
- At build time, BugSplat copies `BugSplatMonitor`, `BugSplatReporter` and `theme/` next to `<Game>.x86_64`. Ship them with your game and keep them executable.
- Managed exceptions post through the same SDK as structured reports.

Unity's Mono and IL2CPP runtimes install their own signal handlers to turn faults in managed code into `NullReferenceException`; bugsplat-native chains to them first, so a managed null dereference stays a managed exception and only real native faults become crash reports.

## Hang detection

Linux has no portable main loop for the SDK to probe, so the heartbeat `BugSplatManager` sends every frame is what the watchdog watches. Set `HangDetectionTimeoutMs` on the options asset; the monitor dumps the live process and uploads a hang report when the heartbeat stops for longer than that.

## Symbols

Set `UploadDebugSymbolsForLinux` to upload the build's `.so`, `.debug` and player binaries as Breakpad `.sym` files (`symbol-upload --dumpSyms`). Build with debug symbols enabled so the `.debug` files exist. See [Symbol Upload](symbol-upload.md) for credentials.
