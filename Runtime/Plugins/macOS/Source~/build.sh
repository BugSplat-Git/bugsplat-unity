#!/bin/sh
# Builds ../BugSplatBridgeMac.dylib, the C# <-> Objective-C bridge for macOS players, from
# BugSplatBridgeMac.mm, and its dSYM, ../Symbols~/BugSplatBridgeMac.dylib.dSYM. The bridge ships
# precompiled because Unity compiles .mm plugins into macOS players only under IL2CPP; a binary
# loads under Mono and IL2CPP alike. The post-build step uploads the dSYM with the player's own.
#
# Usage: sh build.sh   (on a Mac with the Xcode command line tools)
set -eu
cd "$(dirname "$0")"

# Universal (arm64 + x86_64), with the same minimum macOS as BugSplat-macOS.dylib. No -fobjc-arc:
# the bridge is written for manual retain/release, which is how Unity built it from source.
xcrun clang++ -x objective-c++ -std=c++14 -Wall -O2 -g \
    -arch arm64 -arch x86_64 -mmacosx-version-min=11.5 \
    -dynamiclib -install_name @rpath/BugSplatBridgeMac.dylib \
    -framework Foundation \
    -o ../BugSplatBridgeMac.dylib BugSplatBridgeMac.mm

# Compiling and linking in one step with -g, clang runs dsymutil itself and writes the dSYM next
# to the dylib. Unity would import it there, so it moves to a folder Unity ignores.
mkdir -p ../Symbols~
rm -rf ../Symbols~/BugSplatBridgeMac.dylib.dSYM
mv ../BugSplatBridgeMac.dylib.dSYM ../Symbols~/

# Ad-hoc signed, like BugSplat-macOS.dylib.
codesign --force --sign - ../BugSplatBridgeMac.dylib
