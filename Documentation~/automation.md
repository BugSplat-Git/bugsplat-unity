[&larr; BugSplat for Unity](../README.md)

# 🤖 Setting up without the Editor UI

Everything **Edit > Project Settings > BugSplat** does is a view over two files and one editor API, so a script, a CI job, or an AI agent can configure BugSplat without opening the editor's UI. This page is written for that reader.

There are three ways in. Pick the one that matches how the rest of your project is driven.

## 1. Write the options asset

BugSplat initializes from a `BugSplatOptions` asset. **A project with exactly one such asset needs nothing else** — it is selected automatically, in the editor and at build time. Put this at `Assets/BugSplat/BugSplatOptions.asset` (any path under `Assets/` works):

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 485e0e2b32558c54fb70b32dea6b7c38, type: 3}
  m_Name: BugSplatOptions
  m_EditorClassIdentifier: 
  Database: my-database
```

and next to it `BugSplatOptions.asset.meta`, with any fresh 32-hex-digit GUID:

```yaml
fileFormatVersion: 2
guid: 0123456789abcdef0123456789abcdef
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

The `m_Script` GUID is the package's `BugSplatOptions.cs` GUID and does not change between versions. Every field you leave out takes its default on import; the full list is in [API](api.md#bugsplat-options), and the field names in the YAML are exactly the C# names. `Database` is the only required one.

### If the project has more than one options asset

Nothing is picked silently — a build fails and tells you so. Select one explicitly by adding it to `ProjectSettings/EditorBuildSettings.asset`, using the GUID from the asset's `.meta`:

```yaml
  m_configObjects:
    com.bugsplat.unity.options: {fileID: 11400000, guid: <guid of the asset's .meta>, type: 2}
```

Or, from an editor script: `BugSplatUnity.Editor.BugSplatProjectOptions.Set(asset)`.

### Do not add the asset to Preloaded Assets yourself

BugSplat adds the selected asset to **Player Settings > Preloaded Assets** for the duration of a build and removes it again afterwards — that is how a player, which has no Project Settings to read, finds it. Leaving a `BugSplatOptions` in that list yourself is what you want to avoid: a player takes its options from the first one loaded, so two of them would make the choice depend on load order. A build that finds a `BugSplatOptions` there other than the selected one **fails** and names it.

## 2. Run one command

The editor can do all of the above for you in batch mode:

```sh
Unity -batchmode -quit -projectPath . \
  -executeMethod BugSplatUnity.Editor.BugSplatSetup.ConfigureFromCommandLine \
  -bugsplatDatabase my-database \
  [-bugsplatApplication "My Game"] [-bugsplatVersion 1.2.3] \
  [-bugsplatAssetPath Assets/BugSplat/BugSplatOptions.asset]
```

It updates the project's selected asset in place, or creates one at the given path (default `Assets/BugSplat/BugSplatOptions.asset`) and selects it. The process exits `0` on success and `1` with a logged reason on failure. Safe to run again with a new database.

The same call from an editor script:

```cs
using BugSplatUnity.Editor;

var options = BugSplatSetup.Configure("my-database", application: "My Game");
```

`BugSplatSetup.CreateAsset(path)` and `BugSplatProjectOptions.Get()` / `Set(asset)` / `FindAll()` are public for anything more particular.

## 3. Initialize from code only

For a project that wants no asset at all — the options built in code, initialization timed by the project, a consent screen first — define the scripting symbol **`BUGSPLAT_MANUAL_INITIALIZE`** and call `BugSplat.Initialize` yourself:

```cs
var options = ScriptableObject.CreateInstance<BugSplatOptions>();
options.Database = "my-database";
BugSplat.Initialize(options);
```

With the define, BugSplat does not initialize itself, does not warn at startup, and does not fail a build for a missing asset; the project owns all of it. Set the define per build target under **Edit > Project Settings > Player > Scripting Define Symbols**, from a script with `PlayerSettings.SetScriptingDefineSymbols`, or in `ProjectSettings/ProjectSettings.asset` under `scriptingDefineSymbols`.

If you would rather keep an asset for the field values but control the timing, leave the define out, turn **Initialize Automatically** off on the asset, and call `BugSplat.Initialize(options)` with it when ready.

## What you will see when it is wrong

Every message names both fixes, so a log is enough to act on:

- At startup with nothing configured: a warning, `BugSplat is not configured, so nothing will be reported. Open Edit > Project Settings > BugSplat, or add a BugSplatOptions asset anywhere under Assets/ …`
- At build time with nothing selected, several assets and none selected, or an empty database: the build **fails** with the same guidance.
- A leftover `BugSplatManager` in a scene: a warning that it is no longer needed. It still works; see [Migrating from 4.x](migrating-from-4x.md).
