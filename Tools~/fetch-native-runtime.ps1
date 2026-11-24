<#
.SYNOPSIS
  Populates Runtime/Plugins/<platform> with a bugsplat-native release.
.EXAMPLE
  pwsh Tools~/fetch-native-runtime.ps1 -Version 9.0.0 -Platform all
  pwsh Tools~/fetch-native-runtime.ps1 -Platform windows-x64 -Archive C:\dl\bugsplat-native-9.0.0-windows-x64.zip
#>
param(
  [string] $Version = "9.0.0",
  [ValidateSet("windows-x64", "windows-x86", "windows-arm64", "macos", "linux-x64", "android", "ios", "all")]
  [string] $Platform = "all",
  [string] $Archive = "",
  [string] $ReleaseBase = "https://github.com/BugSplat-Git/bugsplat-native/releases/download"
)
$ErrorActionPreference = "Stop"
$package = Split-Path -Parent $PSScriptRoot
$plugins = Join-Path $package "Runtime/Plugins"

# One layout entry per platform: where the loadable library goes (Unity imports it) and where
# the helpers go (Support~, invisible to Unity, copied by PostBuild.cs).
$layout = @{
  "windows-x64"   = @{ lib = "Windows/x86_64"; libs = @("BugSplat.dll"); support = "Windows/Support~/x64"; helpers = @("BugSplatMonitor.exe", "BugSplatReporter.exe", "BugSplatWer.dll", "theme") }
  "windows-x86"   = @{ lib = "Windows/x86";    libs = @("BugSplat.dll"); support = "Windows/Support~/x86"; helpers = @("BugSplatMonitor.exe", "BugSplatReporter.exe", "BugSplatWer.dll", "theme") }
  "windows-arm64" = @{ lib = "Windows/ARM64";  libs = @("BugSplat.dll"); support = "Windows/Support~/ARM64"; helpers = @("BugSplatMonitor.exe", "BugSplatReporter.exe", "BugSplatWer.dll", "theme") }
  "macos"         = @{ lib = "macOS";          libs = @("libbugsplat.dylib"); support = "macOS/Support~"; helpers = @("BugSplatMonitor", "BugSplatReporter.app", "theme") }
  "linux-x64"     = @{ lib = "Linux/x86_64";   libs = @("libbugsplat.so"); support = "Linux/Support~/x86_64"; helpers = @("BugSplatMonitor", "BugSplatReporter", "theme") }
  "android"       = @{ lib = "Android/libs";   libs = @("arm64-v8a", "armeabi-v7a", "x86_64"); support = ""; helpers = @() }
  "ios"           = @{ lib = "iOS";            libs = @("libbugsplat.a"); support = ""; helpers = @() }
}

function Install-Platform([string] $name) {
  $entry = $layout[$name]
  $zip = $Archive
  if (-not $zip) {
    $zip = Join-Path ([System.IO.Path]::GetTempPath()) "bugsplat-native-$Version-$name.zip"
    $url = "$ReleaseBase/v$Version/bugsplat-native-$Version-$name.zip"
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $zip
  }
  $stage = Join-Path ([System.IO.Path]::GetTempPath()) ("bugsplat-native-" + $name + "-" + [guid]::NewGuid().ToString("N"))
  Expand-Archive -Path $zip -DestinationPath $stage -Force

  $libDir = Join-Path $plugins $entry.lib
  New-Item -ItemType Directory -Force $libDir | Out-Null
  foreach ($lib in $entry.libs) {
    $src = Get-ChildItem -Path $stage -Recurse -Filter $lib | Select-Object -First 1
    if (-not $src) { throw "$lib not found in $zip" }
    Copy-Item -Recurse -Force $src.FullName (Join-Path $libDir $lib)
    Write-Host "  $($entry.lib)/$lib"
  }
  if ($entry.support) {
    $supportDir = Join-Path $plugins $entry.support
    New-Item -ItemType Directory -Force $supportDir | Out-Null
    foreach ($helper in $entry.helpers) {
      $src = Get-ChildItem -Path $stage -Recurse -Filter $helper | Select-Object -First 1
      if (-not $src) { throw "$helper not found in $zip" }
      Copy-Item -Recurse -Force $src.FullName (Join-Path $supportDir $helper)
      Write-Host "  $($entry.support)/$helper"
    }
  }
  Remove-Item -Recurse -Force $stage
}

$targets = if ($Platform -eq "all") { @($layout.Keys) } else { @($Platform) }
foreach ($t in $targets) { Write-Host "== $t"; Install-Platform $t }
Write-Host "Done. Open the project once so Unity generates .meta files for any new plugin, then check each library's platform in the Inspector."
