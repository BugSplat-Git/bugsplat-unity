[&larr; BugSplat for Unity](../README.md)

# 🚚 Migrating from 4.x

Version 5.0.0 changes how BugSplat starts. Nothing needs to be in a scene anymore:

- **`BugSplatManager` is obsolete.** BugSplat initializes itself from the `BugSplatOptions` asset selected in **Edit > Project Settings > BugSplat**, before the first scene loads. Open that page — a project with exactly one options asset has it selected already — and remove the `BugSplatManager` from your scene. Until you do, it keeps working: it adopts the instance created at startup and logs a warning that it is no longer needed. **Capture Exceptions On Background Threads** and **Capture Unobserved Task Exceptions** moved from the manager to the options asset.
- **Replace `FindAnyObjectByType<BugSplatManager>().BugSplat` with `BugSplat.Instance`.** It is set before the first scene loads, so it is safe to read in any `Awake` — nothing depends on `Start` running after a manager's `Awake` anymore.
- **Builds fail when no options asset is selected, or the selected one has an empty database.** A misconfigured project used to build a player that silently reported nothing. If your code calls `BugSplat.Initialize` itself, select an asset with **Initialize Automatically** off.
- `BugSplatRef` has been removed. It was an implementation detail of `BugSplatManager`.
- **Your existing options asset needs no edits.** `Enabled`, `Initialize Automatically`, and the three capture options are new fields, and an asset written before they existed loads with all of them **on** — so reporting keeps working exactly as it did. `Post Exceptions In Editor` stays off, as before.
- Not ready to configure BugSplat, or want it only in QA and release builds? Uncheck **Enabled** on the asset, or define `BUGSPLAT_DISABLED` for the targets you want it off for. A release build fails on a missing database; a development build only warns.

Version 5.0.0 replaces the Unity crash-folder minidump flow with native crash reporting:

- `PostAllCrashes`, `PostCrash`, and `PostMostRecentCrash` have been removed. Unsent native crash reports are uploaded automatically at startup — you no longer need to call anything at launch. Delete any calls to these methods.
- Unity's `CrashReporting.crashReportFolder` minidumps are no longer read or uploaded.
- `Post(FileInfo minidump)` still works for posting your own minidump files on every platform except WebGL, where it logs that it isn't implemented and returns without uploading.
- `SymbolUploadClientId` and `SymbolUploadClientSecret` have been removed from `BugSplatOptions`. Storing them there put the secret in version control and inside shipped builds. Set them per database from **BugSplat > Symbol Upload > Set Credentials**, or with environment variables in CI.
- Those environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET` to `SYMBOL_UPLOAD_CLIENT_ID`/`SYMBOL_UPLOAD_CLIENT_SECRET`, matching the names the `symbol-upload` CLI already reads. The old names are no longer read. See [Symbol Upload Credentials](symbol-upload.md#symbol-upload-credentials).
- **Delete any sample you imported under 4.x, then re-import it.** Package Manager copies samples into `Assets/`, and the folder it copies them to is version stamped, so an old copy stays in your project after the upgrade and keeps getting compiled. The 4.x sample calls methods 5.0.0 removed, so it will fail to compile until you delete it. Remove `Assets/Samples/BugSplat/<old version>/` and import the sample again from Package Manager. Deleting that folder discards everything in it, so copy out anything you changed and want to keep first — most often the `BugSplatOptions` asset, but the scene and scripts too if you edited them.
- **iOS projects exported with Append, or checked into version control, keep their old "Upload dSYM files to BugSplat" build phase.** Unity matches an existing phase on its script body, so the rewritten phase is not recognised as the same one. Delete the old phase and build again, or re-export with Replace. A phase generated before 5.0.0 contains your Client ID and Secret in plain text — **rotate them**.
