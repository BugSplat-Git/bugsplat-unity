[&larr; BugSplat for Unity](../README.md)

# 🚚 Migrating from 4.x

Version 5.0.0 replaces the Unity crash-folder minidump flow with native crash reporting:

- `PostAllCrashes`, `PostCrash`, and `PostMostRecentCrash` have been removed. Unsent native crash reports are uploaded automatically at startup — you no longer need to call anything at launch. Delete any calls to these methods.
- Unity's `CrashReporting.crashReportFolder` minidumps are no longer read or uploaded.
- `Post(FileInfo minidump)` still works for posting your own minidump files on every platform except WebGL, where it logs that it isn't implemented and returns without uploading.
- `SymbolUploadClientId` and `SymbolUploadClientSecret` have been removed from `BugSplatOptions`. Storing them there put the secret in version control and inside shipped builds. Set them per database from **BugSplat > Symbol Upload > Set Credentials**, or with environment variables in CI.
- Those environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET` to `SYMBOL_UPLOAD_CLIENT_ID`/`SYMBOL_UPLOAD_CLIENT_SECRET`, matching the names the `symbol-upload` CLI already reads. The old names are no longer read. See [Symbol Upload Credentials](symbol-upload.md#symbol-upload-credentials).
- **Delete any previously imported copy of the sample before importing the 5.0.0 one.** Package Manager copies samples into `Assets/`, so an imported sample is a snapshot that does not update with the package. An older copy keeps calling into the native bridge by names that no longer exist, and the build fails at link time rather than in the C# compiler, which makes the cause hard to see:

  ```
  Undefined symbols for architecture x86_64:
    "__crashNativeMac", referenced from:
        _CrashScenarios_CrashNativeMac_... in ...o
  ```

  Delete `Assets/Samples/BugSplat/<old version>/` and re-import from Package Manager. Note that the folder is version stamped, so importing 5.0.0 alongside a 4.x copy leaves both in the project and both get compiled. If you configured the sample's `BugSplatOptions` asset, copy your values out first: a re-import overwrites it.
- **iOS projects exported with Append, or checked into version control, keep their old "Upload dSYM files to BugSplat" build phase.** Unity matches an existing phase on its script body, so the rewritten phase is not recognised as the same one. Delete the old phase and build again, or re-export with Replace. A phase generated before 5.0.0 contains your Client ID and Secret in plain text — **rotate them**.
