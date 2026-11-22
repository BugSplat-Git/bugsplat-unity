[&larr; BugSplat for Unity](../README.md)

# 🌐 WebGL

A WebGL player reports two kinds of failure:

- **C# exceptions**, through Unity's log callbacks, like every other platform. Log files and screenshots aren't attached on WebGL.
- **WebAssembly crashes.** A trap (memory access out of bounds, `unreachable`, a function signature mismatch, the browser's stack limit) or an Emscripten abort (out of memory) ends the player before any C# code can run, so BugSplat reports these from JavaScript.

## WebAssembly crashes

`CaptureWebAssemblyCrashes` is on by default. `BugSplatWebGL.jspre` runs when Unity's `framework.js` loads, before the engine starts, and:

- keeps the last 100 console lines (500 characters each) from that point on, since WebGL has no `Player.log`;
- listens for `error` and `unhandledrejection` on `window` in the capture phase, so it runs before the Unity loader's own handler, which blocks on `alert()`;
- wraps `Module.abortHandler`, which is the only place Unity's `abort()` (out of memory, for example) reports to.

On the first error with WebAssembly frames, it posts one report for the page load to `https://<database>.bugsplat.com/post/dotnetstandard/` with crash type 39, Unity (WebGL). The report carries the browser's message and stack as `UnityCallstack.txt`, the console lines as `BugSplatWebGL.log`, your attributes, and `userAgent`, `webglHeapBytes`, `jsHeapUsedBytes` and `webglErrorSource` (`error`, `unhandledrejection` or `abort`). Later errors in the same page load are ignored, since a dead module keeps raising them. Errors without WebAssembly frames, from your page's own scripts for example, are ignored too. A C# exception is reported once, by the C# reporter: it never reaches the JavaScript hooks.

User, email, key, description, notes and attributes are kept in sync with JavaScript as you change them on `BugSplat.Instance`, because nothing can be read from C# after a trap.

### Before BugSplat initializes

A trap can happen before `BugSplat.Instance` exists, during engine startup or in another package's `RuntimeInitializeOnLoadMethod`. The first one is held in JavaScript and posted as soon as BugSplat initializes. If the player dies before then, BugSplat uses the database, application, version and report fields it wrote into `Build/<name>.loader.js` at build time, and posts straight away. That happens only when your options asset has `Enabled`, `InitializeAutomatically` and `CaptureWebAssemblyCrashes` on and a `Database`, and `BUGSPLAT_DISABLED` isn't defined for WebGL. With `InitializeAutomatically` off, to wait for consent, nothing is posted until you call `BugSplat.Initialize`.

If you host the player with your own page, you can pass the same fields yourself as `bugsplat` in the config object you give `createUnityInstance`: `{ database, application, version, user, email, key, description, notes, attributes }`. It replaces the one BugSplat wrote.

### Turning it off

Uncheck `CaptureWebAssemblyCrashes` on your options asset. That also leaves `BugSplatWebGL.jspre` out of the build. A `BugSplat` created in code takes `captureWebAssemblyCrashes: false`. `BugSplat.WebAssemblyCrashReportingEnabled` tells you which applies.

### Browsers

Chromium browsers and Firefox put the WebAssembly function index and byte offset in every frame. Safari gives the function index only, so its reports can name functions but can't give lines.

## Symbol upload

Without symbols a WebAssembly frame is just `wasm-function[2594]:0xfeca2`. Set `UploadDebugSymbolsForWebGL` on your options asset to upload what BugSplat needs to name it and find its C# line. It's off by default because it changes how your WebGL builds are made, below, and makes them slower.

For the duration of each WebGL build, BugSplat:

- sets **Publishing Settings > Debug Symbols** to **External** if it was **Off**. That makes Unity write the function-name map;
- adds `-gsource-map` to `PlayerSettings.WebGL.emscriptenArgs`, for a source map of the shipped `.wasm`;
- adds `--compiler-flags=-g --emit-source-mapping` to the additional IL2CPP arguments. The first gives the source map lines for IL2CPP's generated C++ and your native plugins; the second writes `LineNumberMappings.json`, which maps that C++ back to C#. The **IL2CPP Stacktrace Information** player setting that writes it on other platforms is refused on WebGL;
- sets the environment variable `UNITYLINKER_ADDITIONAL_ARGS=--link-symbols`, so UnityLinker keeps your assemblies' PDBs. Without it every C# line in `LineNumberMappings.json` is `<no-source>`.

Your own values are kept: the flags are appended to them. When the build finishes they're put back. A build that fails doesn't reach Unity's post-build callbacks, so its settings are put back at the start of the next build or the next time the editor loads (they're saved in `Library/BugSplat/webgl-build-settings.json` meanwhile).

After the build, BugSplat uploads three files for the database, application and version, named for the shipped `.wasm`, with any `.br`, `.gz` or `.unityweb` suffix removed (`<wasmBase>`):

| Uploaded as | From |
| --- | --- |
| `<wasmBase>.symbols.json` | `Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.js.symbols.json`, the function index to name map Unity writes for External debug symbols |
| `<wasmBase>.wasm.map` | `Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.wasm.map`. Unity doesn't copy it into `Build/` |
| `LineNumberMappings-webgl.json` | `Library/Bee/artifacts/WebGL/il2cppOutput/cpp/Symbols/LineNumberMappings.json` |

The symbol-upload release this package downloads (v11.2.0) uploads the source map and the line mappings, but skips `<wasmBase>.symbols.json` with "missing dbgId"; a later release uploads it too.

### What ships

- **The function-name map.** When BugSplat turned External on, it deletes `Build/<name>.symbols.json` and the `symbolsUrl` line Unity added to `index.html` after uploading. Deployed, that file is about 7 MB of your function names, readable by anyone, and Unity's loader downloads it on an error only to tidy up the text of its alert. If you chose External yourself, both are left alone.
- **The `.wasm`.** `-gsource-map` and the IL2CPP flags don't change the WebAssembly code. Emscripten does append a 34-byte `sourceMappingURL` section naming `build.wasm.map`; BugSplat removes it when compression is **Disabled**, which leaves the `.wasm` byte-identical to a build without the flag. A Brotli or gzip `.wasm` keeps it, and a browser with developer tools open may then report that it couldn't load `build.wasm.map`.
- **`--link-symbols`** is the one flag that changes the shipped `.wasm`, and only slightly: the size and the code section stay the same, but about 165 bytes of the data section differ. With symbols, UnityLinker keeps one more generic type reference in `Unity.Scripting.dll`, `DelegateWithContext<LifecycleNativeDelegate>`, so IL2CPP numbers its generic instances differently.

### Build time

The flags make IL2CPP compile its C++ with debug information and Emscripten write the source map, so the first WebGL build after turning `UploadDebugSymbolsForWebGL` on, or off, rebuilds all of IL2CPP's output. In a small test project, on Unity 6000.5, that build took about 4 minutes, against 2 without the flags.

### An undocumented dependency

`UNITYLINKER_ADDITIONAL_ARGS` isn't documented by Unity. It's read by Unity's IL2CPP build logic (`Unity.IL2CPP.Bee.BuildLogic.dll`) in the player build program, which the editor starts after the pre-build callbacks run, so setting it from one reaches UnityLinker. Verified on Unity 6000.5. If a future Unity stops reading it, the build still succeeds and uploads, but C# lines in `LineNumberMappings-webgl.json` become `<no-source>`; function names and C++ lines are unaffected.
