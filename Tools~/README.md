# Native runtime layout

`com.bugsplat.unity` 5.0 is a C# binding over [bugsplat-native](https://github.com/BugSplat-Git/bugsplat-native)'s C ABI (`bugsplat.h`). The same library and the same out-of-process helpers serve every player platform; Unity only needs them laid out where its plugin importer and the package's post-build step expect them.

| Platform | Library Unity loads (`Runtime/Plugins/...`) | Helpers copied next to the player at build time (`Runtime/Plugins/<platform>/Support~/...`) |
|---|---|---|
| Windows x64 | `Windows/x86_64/BugSplat.dll` | `Windows/Support~/x64/BugSplatMonitor.exe`, `BugSplatReporter.exe`, `BugSplatWer.dll`, `theme/` |
| Windows x86 / ARM64 | `Windows/x86/BugSplat.dll`, `Windows/ARM64/BugSplat.dll` | `Windows/Support~/x86/...`, `Windows/Support~/ARM64/...` |
| macOS (universal) | `macOS/libbugsplat.dylib` | `macOS/Support~/BugSplatMonitor`, `BugSplatReporter.app/`, `theme/` (copied into `Contents/Helpers`) |
| Linux x86_64 | `Linux/x86_64/libbugsplat.so` | `Linux/Support~/x86_64/BugSplatMonitor`, `BugSplatReporter`, `theme/` |
| Android | `Android/libs/<abi>/libbugsplat.so` and `libBugSplatMonitor.so` | none: the monitor is a shared library the SDK executes at crash time |
| iOS | `iOS/libbugsplat.a` (static, linked into UnityFramework) | none: the handler runs in process and reports upload on the next launch |

Folders ending in `~` are invisible to Unity, so it never tries to import `BugSplatMonitor.exe` as a plugin; `Editor/PostBuild.cs` copies them next to the built player.

The Windows x64 set in this repository was built from bugsplat-native `feat/m1-core`. Every other platform is populated from a bugsplat-native release with the scripts here:

```
pwsh Tools~/fetch-native-runtime.ps1 -Version 9.0.0            # Windows editor
sh   Tools~/fetch-native-runtime.sh 9.0.0                      # macOS / Linux editor
```

Both accept `-Platform` / a second argument (`windows-x64`, `windows-x86`, `windows-arm64`, `macos`, `linux-x64`, `android`, `ios`, or `all`) and write the files into the layout above. The release archives are `bugsplat-native-<version>-<platform>.zip` on the bugsplat-native releases page; `-Archive <path>` (or a third argument) uses a local zip instead of downloading.

After fetching, open the project once so Unity generates `.meta` files for any new plugin, then check each library's platform in the Inspector: Unity infers Android and iOS from the folder names and CPU from `x86_64`, but `macOS/` and `Linux/` need the platform ticked by hand.
