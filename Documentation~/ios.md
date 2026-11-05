[&larr; BugSplat for Unity](../README.md)

# 🍎 iOS

Native iOS crash reporting comes from [bugsplat-native](native.md). iOS allows no out-of-process helper, so Crashpad's in-process handler captures the crash and writes an intermediate dump; the SDK converts it and uploads it on the next launch, without a dialog. `UseNativeCrashReporting` is on by default.

When native crash reporting is enabled, BugSplat disables Unity's built-in crash reporter in the generated Xcode project (`Classes/CrashReporter.h`) so bugsplat-native's handler sees crashes first, and sets `DEBUG_INFORMATION_FORMAT` to `dwarf-with-dsym` so symbols exist to upload.

For IL2CPP builds, BugSplat also copies `LineNumberMappings.json` into the Xcode project and uploads it alongside the dSYMs, so IL2CPP-generated C++ symbols map back to C# method names, file names, and line numbers. Unity only generates the file when **Edit > Project Settings > Player > Other Settings > IL2CPP Stacktrace Information** is set to **Method Name, File Name, and Line Number**; without it, the build log warns and names the setting.

## Attachments

`PersistentDataFileAttachmentPaths` and `AttachNativeLogFile` work as on the other platforms; the SDK records the attachment list per session, and the next-launch import copies the files into the report. `Player.log` is attached when `CapturePlayerLog` is enabled.

## Hang detection

## Testing on Device

A debugger claims the Mach exception ports before bugsplat-native's handler does, so **a crash that happens under the Xcode debugger never reaches BugSplat**. Xcode's **Build And Run** leaves the debugger on, so a crash triggered that way produces no report and no explanation.

Two ways to test:

- Uncheck **Product > Scheme > Edit Scheme > Run > Info > Debug executable**, then Run. Only the Run action changes; Test and Profile are unaffected.
- Or launch the app from the home screen instead of from Xcode.

Either way the report uploads on the **next** launch, not at crash time, so relaunch the app after the crash.

Set `HangDetectionTimeoutMs` on the options asset. The heartbeat BugSplat sends every frame feeds an in-process watchdog; when it stops for longer than the timeout, the SDK writes a hang report that uploads on the next launch. Full-memory dumps are not available on iOS; `DumpType` is ignored there with a logged warning.

## Symbols

Set `UploadDebugSymbolsForIos` to add an Xcode build phase that uploads the dSYMs as Breakpad `.sym` files (`symbol-upload --dumpSyms`) and `LineNumberMappings.json`. The phase reads credentials from `SYMBOL_UPLOAD_CLIENT_ID` / `SYMBOL_UPLOAD_CLIENT_SECRET` in the Xcode build environment or from `~/.bugsplat/credentials/<database>.sh`; see [Symbol Upload](symbol-upload.md).

> [!NOTE]
> Unity's Xcode project has a `Generate dSYM for archive, and strip` build phase. BugSplat's upload phase is inserted after it, so the dSYM exists when the upload runs; on the first build after export, check the build log to confirm the order.
