# Third Party Notices

This package contains third-party software components governed by the license(s) indicated below.

Most of the third-party code in this package arrives inside prebuilt binaries rather than as source,
so the entries are grouped by the binary payload that carries them. Each group lists where the payload
lives in this repository, which upstream project it is built from, and the evidence used to identify
the components inside it.

Anything that could **not** be established from this repository or from the upstream project's own
license files is marked **UNRESOLVED** and collected in [Open questions](#open-questions) at the end
of this document. Those entries are deliberately left blank rather than filled in with a guess.

> **The MIT license in [LICENSE.md](./LICENSE.md) covers this package's source code only.** The
> prebuilt binaries under `Runtime/Plugins/**` and `Editor/IOS/Frameworks/**` are licensed
> separately, by the terms recorded for each payload below. `LICENSE.md` states that scope
> explicitly so the MIT grant is not read as extending to them.

---------

## Apple platforms (iOS, tvOS, macOS)

Shipped in:

- `Editor/IOS/Frameworks/BugSplat.xcframework/**` (iOS, tvOS and macOS slices, plus their dSYMs)
- `Runtime/Plugins/macOS/BugSplat-macOS.dylib`

Built from [BugSplat-Git/bugsplat-apple](https://github.com/BugSplat-Git/bugsplat-apple). The
component list below comes from that project's `LICENSE.txt`, corroborated by the source paths and
symbols embedded in the shipped binaries (`Vendor/PLCrashReporter/Source/*`,
`Vendor/PLCrashReporter/Dependencies/protobuf-c/protobuf-c/protobuf-c.c`, and `BugSplatPLCrash*` /
`protobuf_c_*` symbols in the macOS dylib).

---------

Component Name: BugSplat for Apple platforms

License Type: "MIT"

Copyright (c) 2012-2014 HockeyApp, Bit Stadium GmbH. All rights reserved.

[BugSplat for Apple platforms license](https://github.com/BugSplat-Git/bugsplat-apple/blob/main/LICENSE.txt)

---------

Component Name: PLCrashReporter

License Type: "MIT"

Copyright (c) 2008 - 2014 Plausible Labs Cooperative, Inc.
Copyright (c) 2012 - 2014 HockeyApp, Bit Stadium GmbH.
All rights reserved.

[PLCrashReporter license](https://github.com/BugSplat-Git/bugsplat-apple/blob/main/LICENSE.txt)

> Note: the 5.0.0 audit described this payload as BSD-licensed. The upstream `LICENSE.txt` in
> bugsplat-apple states MIT for both BugSplat and PLCrashReporter. MIT is what is recorded here.

---------

Component Name: protobuf-c

License Type: "Apache-2.0"

Copyright 2008, Dave Benson.

[protobuf-c license](https://github.com/BugSplat-Git/bugsplat-apple/blob/main/LICENSE.txt)

---------

## Android

Shipped in: `Runtime/Plugins/Android/bugsplat-android-release.aar`

The `.aar` contains `classes.jar` (`com.bugsplat.android.*`) and, for each of `arm64-v8a`,
`armeabi-v7a` and `x86_64`, the native libraries `libbugsplat.so`, `libcrashpad_handler.so` and
`libcurl.so`. It is built from
[BugSplat-Git/bugsplat-android](https://github.com/BugSplat-Git/bugsplat-android), whose
`.gitmodules` declares `third_party/crashpad` -> `https://github.com/chromium/crashpad.git` and
`third_party/libcurl-android` -> `https://github.com/BugSplat-Git/libcurl-android.git`;
`libcurl-android` in turn vendors `https://github.com/curl/curl` and
`https://github.com/google/boringssl`.

`Runtime/Plugins/Android/LICENSE` sits next to the `.aar`. It is the unmodified Apache License 2.0
boilerplate, including the "Copyright [yyyy] [name of copyright owner]" appendix placeholders. It
names no copyright holder and does not state which component it applies to. See
[Open questions](#open-questions).

---------

Component Name: BugSplat for Android (`classes.jar`, `libbugsplat.so`)

License Type: **UNRESOLVED**

Upstream: https://github.com/BugSplat-Git/bugsplat-android — that repository contains no license file,
and the `.aar` contains no `META-INF` license or notice entry.

---------

Component Name: Crashpad

License Type: "Apache-2.0"

Upstream: https://github.com/chromium/crashpad (declared as a submodule of bugsplat-android; the
shipped `libcrashpad_handler.so` and `libbugsplat.so` contain `crashpad::` symbols and
`crashpad.chromium.org` strings)

[Crashpad license](https://github.com/chromium/crashpad/blob/main/LICENSE)

---------

Component Name: mini_chromium

License Type: "BSD-3-Clause"

Bundled inside Crashpad as `third_party/mini_chromium`; `libcrashpad_handler.so` contains
`third_party/mini_chromium` path strings and `base::` symbols.

[mini_chromium license](https://github.com/chromium/mini_chromium/blob/main/LICENSE)

---------

Component Name: curl / libcurl

License Type: "curl" (MIT/X derivative)

Copyright (c) 1996 - 2026, Daniel Stenberg, <daniel@haxx.se>, and many contributors, see the THANKS
file.

The shipped `libcurl.so` reports `libcurl/8.13.1-DEV`.

[curl license](https://github.com/curl/curl/blob/master/COPYING)

---------

Component Name: BoringSSL

License Type: "Apache-2.0" (upstream `LICENSE` additionally carries notices covering portions of the
code under other terms)

Statically linked into the shipped `libcurl.so` (`BORINGSSL_*` symbols are present).

[BoringSSL license](https://github.com/google/boringssl/blob/main/LICENSE)

---------

The Android native libraries also link the Android platform `libz.so` and the NDK's
`libc++_shared.so`. Neither is redistributed by this package.

---------

## Windows

Shipped in:

- `Runtime/Plugins/Windows/{x86,x86_64,ARM64}/BugSplat.dll`
- `Runtime/Plugins/Windows/Support~/{x86,x64,ARM64}/BugSplatMonitor.exe`
- `Runtime/Plugins/Windows/Support~/{x86,x64,ARM64}/BugSplatRc.dll`
- `Runtime/Plugins/Windows/Support~/{x86,x64,ARM64}/BugSplatWer.dll`

Built from [BugSplat-Git/bugsplat-windows](https://github.com/BugSplat-Git/bugsplat-windows). The
version resources report `8.1.0.0`, "BugSplat, LLC", "Copyright BugSplat. All rights reserved."

---------

Component Name: BugSplat Crash Reporting SDK for Windows

License Type: **UNRESOLVED — proprietary, terms pending**

Upstream: https://github.com/BugSplat-Git/bugsplat-windows — a private repository containing no
license file. These binaries are not open source and are **not** covered by this package's MIT
license. A binary redistribution license granting the right to embed and ship them inside an
application is being prepared; this entry must be replaced with a link to it before 5.0.0 tags.

---------

Component Name: Windows Template Library (WTL)

License Type: "MS-PL"

Confirmed present in `BugSplatMonitor.exe` (`WTL::CAppModule`, `WTL::CWinDataExchange`,
`WTL::CDialogResize` RTTI records).

[WTL license](https://github.com/BugSplat-Git/Wtl/blob/master/MS-PL.txt)

---------

Component Name: MultipartEncoder

License Type: "MIT"

Declared as a submodule of bugsplat-windows; `BugSplatMonitor.exe` emits
`Content-Type: multipart/form-data; boundary=`.

[MultipartEncoder license](https://github.com/BugSplat-Git/MultipartEncoder)

---------

The bugsplat-windows repository publishes an open-source notice file, `ReadMeOss.txt`, that is not
mirrored into this package. It covers RapidJSON v1.1 (MIT; Copyright (C) 2015 THL A29 Limited, a
Tencent company, and Milo Yip), msinttypes r29 (BSD-3-Clause; Copyright (c) 2006-2013 Alexander
Chemeris), the JSON.org code under the JSON License (which BugSplat states it excludes),
GenericHTTPClient v0.1.0 (BSD-3-Clause; Copyright 2003 Heo Yongsun) and Info-ZIP ZIP 3.0 (Info-ZIP
license; Copyright (c) 1990-2007 Info-ZIP). It is not possible to confirm from the shipped binaries
alone which of those are actually linked into the Windows payload in this package, so they are listed
under [Open questions](#open-questions) rather than asserted here.

The Windows binaries import Microsoft's Active Template Library (ATL), the Visual C++ runtime
(`MSVCP140.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`) and the Universal CRT. None of those are
redistributed by this package.

---------

## .NET

Component Name: BugSplatDotNetStandard

License Type: "MIT"

Version: 4.3.0.0 (per `Runtime/Plugins/BugSplatDotNetStandard.deps.json`)

Shipped in: `Runtime/Plugins/BugSplatDotNetStandard.dll`

`BugSplatDotNetStandard.deps.json` declares no dependency other than `NETStandard.Library` and
`Microsoft.NETCore.Platforms`, and the assembly references only .NET Standard 2.0 base class
libraries, so the DLL carries no additional third-party code.

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
provenance. See [Open questions](#open-questions).

---------

## Note on `Editor/LICENSE-MIT`

An unattributed MIT license text lived at `Editor/LICENSE-MIT`. It named no copyright holder and did
not say what it covered, and it has been removed in favour of this file. For the record, its
provenance is established, not assumed:

- The file was byte-identical (MD5 `b377b220f43d747efdec40d69fcaa69d`) to the `LICENSE-MIT` in
  [mozilla/dump_syms](https://github.com/mozilla/dump_syms/blob/main/LICENSE-MIT), which is itself
  published upstream without a copyright line. `dump_syms` is dual-licensed Apache-2.0 / MIT.
- It was added in commit `474b82d` ("fix: android symbols", #88, 2024-03-06) in the same change that
  added the `Editor/dump-syms-linux`, `Editor/dump-syms-mac` and `Editor/dump-syms-win.exe` binaries.
- Those binaries were removed in `7b7ca2b` ("chore: use symbol-upload for dump-syms", #90,
  2024-04-16), and the remaining vendored `symbol-upload` binaries in `af5b08d` ("chore: download
  symbol-upload just-in-time", #99, 2025-04-17). The license file was never removed with them.

It was therefore never related to the Apple payload; bugsplat-apple ships its own `LICENSE.txt` with
named copyright holders, reproduced in the Apple section above.

---------

## Open questions

These could not be resolved from this repository or from the upstream projects' own license files.
They need a decision from the maintainers before this file can be considered complete.

1. **Windows natives** — `BugSplat-Git/bugsplat-windows` is private and has no license file. The
   source is staying closed, so these ship under a proprietary binary redistribution license that is
   being drafted; counsel review is outstanding on the warranty disclaimer, the limitation of
   liability, and whether MS-PL's conditions on distributing WTL in compiled form sit comfortably
   inside those terms. **Blocking 5.0.0:** replace the Windows entry above with a link to the final
   document before tagging.
2. **Windows third-party set** — upstream `ReadMeOss.txt` lists RapidJSON, msinttypes, JSON.org,
   GenericHTTPClient and Info-ZIP, while `bugsplat-windows/.gitmodules` additionally declares
   `ThirdParty/Miniz-Cpp` (tfussell/miniz-cpp, MIT) and `ThirdParty/ATGTK`, which `ReadMeOss.txt`
   does not cover. Which of these are actually linked into the four binaries shipped here, and should
   `ReadMeOss.txt` be mirrored into this package?
3. **Android natives** — `BugSplat-Git/bugsplat-android` has no license file. Under what terms are
   `classes.jar` and `libbugsplat.so` redistributed?
4. **`Runtime/Plugins/Android/LICENSE`** — the bare Apache-2.0 text with no copyright statement. Was
   it intended to cover BugSplat's own Android code, or Crashpad? It should either be given a
   copyright/attribution line or replaced by an entry in this file.
5. **Upstream revisions** — this repository records no build manifest for the vendored binaries, so
   the exact upstream revisions of Crashpad, curl, BoringSSL, PLCrashReporter and protobuf-c that
   went into the shipped payloads are not knowable from here. Only `libcurl/8.13.1-DEV` (from the
   binary's own version string) and `BugSplatDotNetStandard 4.3.0.0` (from `deps.json`) are pinned by
   evidence in-tree. Recording the source revisions at build time would make future audits mechanical.
6. **BoringSSL** — its upstream `LICENSE` is Apache-2.0 with additional notices for portions of the
   tree. Whether any of those non-Apache portions end up in the statically linked copy inside
   `libcurl.so` was not determined.
7. **Sample sprites** — the origin of `grey_button_up.png` and `grey_button_pressed.png` in
   `Samples~/my-unity-crasher/Sprites/UI/` is not recorded anywhere in the repository. If they came
   from a third-party asset pack they need an entry here; if they are BugSplat's own, no entry is
   needed.
