[&larr; BugSplat for Unity](../README.md)

# 🍎 macOS

Native macOS crash reporting comes from [bugsplat-native](native.md): an out-of-process `BugSplatMonitor` built on Crashpad captures the crash and writes the minidump while the player is frozen, and `BugSplatReporter.app` shows the BugSplat dialog and uploads. Works with the **Mono** and **IL2CPP** scripting backends; IL2CPP is recommended, and is what produces `LineNumberMappings.json` so C# frames in native stacks symbolicate.

`UseNativeCrashReporting` is on by default. When it is on:

- Native crashes — `EXC_BAD_ACCESS`, aborts, uncaught C++ exceptions, stack overflows — are captured at crash time and, with `UploadPolicy.Dialog`, the BugSplat dialog appears immediately, as on Windows. Reports that can't be uploaded are sent on the next launch.
- `Player.log` is attached when `CapturePlayerLog` is enabled, and attachments can be added at any point in the session: the monitor copies them right after the dump.
- At build time, BugSplat copies `BugSplatMonitor`, `BugSplatReporter.app` and `theme/` into `<Game>.app/Contents/Helpers/`. For an Xcode project export, add them to the target yourself.
- Managed exceptions post through the same SDK as structured reports.

## Signing and notarization

The helpers are executables inside your bundle, so they must be signed with your Developer ID (hardened runtime) along with the app before notarization — `codesign --deep` or an explicit pass over `Contents/Helpers` both work. `BugSplatMonitor` needs no entitlements of its own.

**Mac App Store / App Sandbox:** the sandbox denies the Mach handshake the out-of-process monitor uses. Sandboxed builds are not supported by this version; leave `UseNativeCrashReporting` off for them and rely on managed exception reporting.

## Hang detection

Set `HangDetectionTimeoutMs` on the options asset. The SDK pings the main dispatch queue itself and `BugSplatManager` sends a heartbeat every frame; when both stop for longer than the timeout, the monitor dumps the live process and uploads a hang report. With `HangPolicy.Report` the game continues; with `ReportAndTerminate` the dialog offers Wait / Close.

## Symbols

Set `UploadDebugSymbolsForMac` to upload the build's dSYMs as Breakpad `.sym` files (`symbol-upload --dumpSyms`) plus `LineNumberMappings.json` for IL2CPP builds. Xcode project exports are skipped, since the dSYMs do not exist until Xcode builds. See [Symbol Upload](symbol-upload.md) for credentials.
