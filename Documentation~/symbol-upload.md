[&larr; BugSplat for Unity](../README.md)

# 🔑 Symbol Upload

How BugSplat authenticates the post-build symbol upload, where those credentials come from, and what happens when an upload stalls or fails.

## Symbol Upload Credentials

Credentials are generated on BugSplat's [Integrations](https://app.bugsplat.com/v2/database/integrations) page and are **specific to one database**. They are never stored in your project — an asset carrying them ends up in version control and inside shipped builds. They resolve in this order:

1. **`SYMBOL_UPLOAD_CLIENT_ID` / `SYMBOL_UPLOAD_CLIENT_SECRET` environment variables** — use these in CI. They are the names the `symbol-upload` CLI reads, so the same pair works whether Unity runs the upload or your CI runs `xcodebuild` itself.
2. **`~/.bugsplat/credentials/<database>.sh`** — for local development. Set it from **Tools > BugSplat > Symbol Upload > Set Credentials**, which writes one file per database, so a machine can hold credentials for as many databases as you work with.

`Clear Credentials` deletes the current project's file; `Check Credentials` reports which source a build would use. When neither source supplies both values, symbol upload is skipped with a warning and the build still succeeds — on iOS as an Xcode build warning, since that upload runs during the Xcode build rather than the Unity one.

Because the file lives in your home directory rather than the project, there is nothing to add to `.gitignore` and nothing to strip out of a build.

> **Upgrading from 4.x:** `SymbolUploadClientId` and `SymbolUploadClientSecret` have been removed from `BugSplatOptions`, and the environment variables are renamed from `BUGSPLAT_CLIENT_ID`/`BUGSPLAT_CLIENT_SECRET`. Move your credentials to the menu or the new variables. **If an options asset holding credentials has ever been committed, rotate them** — prior versions serialized both values into player builds and into the generated `project.pbxproj`.

## Timeouts and Failures

The editor waits for each upload to finish. In the editor a progress bar shows how long it has been running and the uploader's latest output, and its **Cancel** button stops the upload; in batch mode the log gets a line every minute instead.

An upload that runs longer than **`SymbolUploadTimeoutMinutes`** (default `30`) is stopped and logged as an error, so a network outage or security software holding the uploader cannot hang the editor or a CI job. Thirty minutes leaves room for gigabytes of `.pdb` files on a slow connection; raise it if your symbols legitimately take longer, or set `0` to wait indefinitely.

A failed, timed-out, or cancelled upload is logged as an error, followed by a summary naming every upload that failed. The player is already built and works, so by default the build still succeeds. Turn on **`FailBuildOnSymbolUploadError`** to fail the build instead, which is what you usually want in CI: a build whose crash reports won't symbolicate stops there rather than shipping. Missing credentials are not a failure; the upload is skipped with a warning, as above.

## BugSplat Environment Variables

| Variable | Description |
|----------| --------------- |
| SYMBOL_UPLOAD_CLIENT_ID | An OAuth2 Client ID value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/database/integrations) page.<br>Takes precedence over `~/.bugsplat/credentials/<database>.sh` — see [Symbol Upload Credentials](#symbol-upload-credentials) |
| SYMBOL_UPLOAD_CLIENT_SECRET | An OAuth2 Client Secret value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/database/integrations) page.<br>Takes precedence over `~/.bugsplat/credentials/<database>.sh` — see [Symbol Upload Credentials](#symbol-upload-credentials) |
