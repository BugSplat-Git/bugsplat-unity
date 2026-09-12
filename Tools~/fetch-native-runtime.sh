#!/bin/sh
# Populates Runtime/Plugins/<platform> with a bugsplat-native release.
#   sh Tools~/fetch-native-runtime.sh 9.0.0 [windows-x64|windows-x86|windows-arm64|macos|linux-x64|android|ios|all] [local.zip]
set -eu
VERSION="${1:-9.0.0}"
PLATFORM="${2:-all}"
ARCHIVE="${3:-}"
BASE="${BUGSPLAT_NATIVE_RELEASES:-https://github.com/BugSplat-Git/bugsplat-native/releases/download}"
PACKAGE="$(cd "$(dirname "$0")/.." && pwd)"
PLUGINS="$PACKAGE/Runtime/Plugins"

layout() {
  case "$1" in
    windows-x64)   echo "Windows/x86_64|BugSplat.dll|Windows/Support~/x64|BugSplatMonitor.exe BugSplatReporter.exe BugSplatWer.dll theme" ;;
    windows-x86)   echo "Windows/x86|BugSplat.dll|Windows/Support~/x86|BugSplatMonitor.exe BugSplatReporter.exe BugSplatWer.dll theme" ;;
    windows-arm64) echo "Windows/ARM64|BugSplat.dll|Windows/Support~/ARM64|BugSplatMonitor.exe BugSplatReporter.exe BugSplatWer.dll theme" ;;
    macos)         echo "macOS|libbugsplat.dylib|macOS/Support~|BugSplatMonitor BugSplatReporter.app theme" ;;
    linux-x64)     echo "Linux/x86_64|libbugsplat.so|Linux/Support~/x86_64|BugSplatMonitor BugSplatReporter theme" ;;
    android)       echo "Android/libs|arm64-v8a armeabi-v7a x86_64||" ;;
    ios)           echo "iOS|libbugsplat.a||" ;;
    *) echo "unknown platform $1" >&2; exit 2 ;;
  esac
}

install() {
  name="$1"
  spec="$(layout "$name")"
  libdir="$(echo "$spec" | cut -d'|' -f1)"; libs="$(echo "$spec" | cut -d'|' -f2)"
  supdir="$(echo "$spec" | cut -d'|' -f3)"; helpers="$(echo "$spec" | cut -d'|' -f4)"
  zip="$ARCHIVE"
  if [ -z "$zip" ]; then
    zip="${TMPDIR:-/tmp}/bugsplat-native-$VERSION-$name.zip"
    echo "Downloading $BASE/v$VERSION/bugsplat-native-$VERSION-$name.zip"
    curl -fsSL -o "$zip" "$BASE/v$VERSION/bugsplat-native-$VERSION-$name.zip"
  fi
  stage="$(mktemp -d)"
  unzip -q -o "$zip" -d "$stage"
  mkdir -p "$PLUGINS/$libdir"
  for lib in $libs; do
    src="$(find "$stage" -name "$lib" | head -n 1)"
    [ -n "$src" ] || { echo "$lib not found in $zip" >&2; exit 1; }
    rm -rf "$PLUGINS/$libdir/$lib"; cp -R "$src" "$PLUGINS/$libdir/$lib"; echo "  $libdir/$lib"
  done
  if [ -n "$supdir" ]; then
    mkdir -p "$PLUGINS/$supdir"
    for h in $helpers; do
      src="$(find "$stage" -name "$h" | head -n 1)"
      [ -n "$src" ] || { echo "$h not found in $zip" >&2; exit 1; }
      rm -rf "$PLUGINS/$supdir/$h"; cp -R "$src" "$PLUGINS/$supdir/$h"; echo "  $supdir/$h"
    done
    chmod -R +x "$PLUGINS/$supdir" 2>/dev/null || true
  fi
  rm -rf "$stage"
}

if [ "$PLATFORM" = "all" ]; then
  for p in windows-x64 windows-x86 windows-arm64 macos linux-x64 android ios; do echo "== $p"; install "$p"; done
else
  install "$PLATFORM"
fi
echo "Done. Open the project once so Unity generates .meta files for any new plugin, then check each library's platform in the Inspector."
