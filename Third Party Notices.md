# Third Party Notices

This package contains third-party software components governed by the license(s) indicated below.

> **The MIT license in [LICENSE.md](./LICENSE.md) covers this package's source code.** The prebuilt
> binaries under `Runtime/Plugins/**` are builds of the open-source projects listed here and are
> licensed by their own terms, reproduced below.

---------

## bugsplat-native (all player platforms)

Shipped in `Runtime/Plugins/**`: `BugSplat.dll` (Windows), `libbugsplat.dylib` (macOS),
`libbugsplat.so` (Linux, Android), `libbugsplat.a` (iOS), and the helpers under
`Runtime/Plugins/<platform>/Support~` (`BugSplatMonitor`, `BugSplatReporter`, `BugSplatWer.dll`,
`theme/`). Every one of them is built from
[BugSplat-Git/bugsplat-native](https://github.com/BugSplat-Git/bugsplat-native); the version is
reported at runtime by `BugSplat.NativeVersion` and recorded in `Tools~/README.md` for the binaries
committed to this repository.

---------

Component Name: bugsplat-native

License Type: "MIT"

Copyright (c) 2026 BugSplat, LLC.

[bugsplat-native license](https://github.com/BugSplat-Git/bugsplat-native/blob/main/LICENSE)

---------

Component Name: Crashpad

License Type: "Apache-2.0"

Copyright 2014 The Crashpad Authors

Statically linked into every bugsplat-native binary; the crash capture, minidump writer and the
`BugSplatMonitor` handler are Crashpad. Built unmodified from a pinned commit recorded in
bugsplat-native's `.github/workflows/ci.yml`.

[Crashpad license](https://github.com/chromium/crashpad/blob/main/LICENSE)

---------

Component Name: mini_chromium

License Type: "BSD-3-Clause"

Copyright 2006-2008 The Chromium Authors

Bundled inside Crashpad as `third_party/mini_chromium`.

[mini_chromium license](https://github.com/chromium/mini_chromium/blob/main/LICENSE)

---------

Component Name: miniz

License Type: "MIT"

Copyright 2013-2014 RAD Game Tools and Valve Software
Copyright 2010-2014 Rich Geldreich and Tenacious Software LLC

Report zips are written with miniz (`third_party/miniz` in bugsplat-native).

[miniz license](https://github.com/richgel999/miniz/blob/master/LICENSE)

---------

Component Name: JSON for Modern C++ (nlohmann/json)

License Type: "MIT"

Copyright (c) 2013-2022 Niels Lohmann

[nlohmann/json license](https://github.com/nlohmann/json/blob/develop/LICENSE.MIT)

---------

Component Name: Windows Template Library (WTL)

License Type: "MS-PL"

Used by `BugSplatReporter.exe` (the Windows crash dialog) only.

[WTL license](https://github.com/BugSplat-Git/Wtl/blob/master/MS-PL.txt)

---------

Component Name: zlib

License Type: "zlib"

Copyright (C) 1995-2024 Jean-loup Gailly and Mark Adler

Crashpad links the system zlib on macOS, Linux, Android and iOS and an embedded copy on Windows.

[zlib license](https://zlib.net/zlib_license.html)

---------

The Windows binaries import the Visual C++ runtime (`MSVCP140.dll`, `VCRUNTIME140.dll`,
`VCRUNTIME140_1.dll`) and the Universal CRT; the Android binaries link the NDK's `libc++_shared.so`.
None of those are redistributed by this package.

---------

## .NET

Component Name: BugSplatDotNetStandard

License Type: "MIT"

Version: 4.3.0.0 (per `Runtime/Plugins/BugSplatDotNetStandard.deps.json`)

Shipped in: `Runtime/Plugins/BugSplatDotNetStandard.dll`

Used for managed exception and feedback posts in the editor and on WebGL, for `Post(FileInfo)`, and by
the editor's symbol upload credential tooling. `BugSplatDotNetStandard.deps.json` declares no
dependency other than `NETStandard.Library` and `Microsoft.NETCore.Platforms`, and the assembly
references only .NET Standard 2.0 base class libraries, so the DLL carries no additional third-party
code.

[BugSplatDotNetStandard license](https://github.com/BugSplat-Git/bugsplat-dotnet-standard/blob/main/LICENSE)

---------

## Tools downloaded at build time (not redistributed)

`Editor/PostBuild.cs` downloads the `symbol-upload` CLI from `https://app.bugsplat.com/download/...`
when symbol upload is enabled. It is not vendored in this package and is therefore not covered by
this file; it is licensed MIT
([BugSplat-Git/symbol-upload](https://github.com/BugSplat-Git/symbol-upload/blob/main/LICENSE)).

---------

## Sample assets

`Samples~/my-unity-crasher/Sprites/UI/bug.png` and
`Samples~/my-unity-crasher/Sprites/UI/splats-overlap-gradient-bg-text-dark.png` are BugSplat artwork.

`Samples~/my-unity-crasher/Sprites/UI/grey_button_up.png` and
`Samples~/my-unity-crasher/Sprites/UI/grey_button_pressed.png` were added in 2021 with no recorded
provenance. If they came from a third-party asset pack they need an entry here; if they are
BugSplat's own, no entry is needed.
