using System;
using System.Collections.Generic;
using UnityEngine;

namespace BugSplatUnity.Runtime.Client
{
	/// <summary>
	/// A single report attribute. Unity cannot serialize a Dictionary, so attributes are authored
	/// as a list of pairs.
	/// </summary>
	[Serializable]
	public class BugSplatAttribute
	{
		public string Name;
		public string Value;
	}

	[CreateAssetMenu(menuName = "BugSplat Options")]
	public class BugSplatOptions : ScriptableObject
	{
		[Header("BugSplat Account")]
		[Tooltip("The name of your BugSplat database. Required.")]
		public string Database;

		[Tooltip("The name of your BugSplat application. Defaults to Application.productName if no value is set.")]
		public string Application;

		[Tooltip("The version of your BugSplat application. Defaults to Application.version if no value is set.")]
		public string Version;

		[Header("Report Metadata")]
		[Tooltip("A default description that can be overridden by a call to Post.")]
		public string Description;

		[Tooltip("A default email that can be overridden by a call to Post.")]
		public string Email;

		[Tooltip("A default key that can be overridden by a call to Post.")]
		public string Key;

		[Tooltip("A default general purpose field that can be overridden by a call to Post.")]
		public string Notes;

		[Tooltip("A default user that can be overridden by a call to Post.")]
		public string User;

		[Tooltip("Attributes to attach to reports.")]
		public List<BugSplatAttribute> Attributes = new List<BugSplatAttribute>();

		[Header("Capture")]
		[Tooltip("Upload Editor.log when Post is called.")]
		public bool CaptureEditorLog;

		[Tooltip("Upload Player.log when Post is called (default). Player.log paths contain the OS username - uncheck to opt out. Not available on WebGL.")]
		public bool CapturePlayerLog = true;

		[Tooltip("Maximum size of the log files to upload in MB. Defaults to 10MB if not set.")]
		public int LogFileMaxSizeMB = 10;

		[Tooltip("Take a screenshot and upload it when Post is called.")]
		public bool CaptureScreenshots;

		[Tooltip("Should BugSplat upload exceptions when in editor. Off by default so play mode exceptions stay out of your database.")]
		public bool PostExceptionsInEditor;

		[Tooltip("Paths to files, relative to Application.persistentDataPath, to attach to managed reports, and to native crash reports where native crash reporting is enabled. Entries that are not relative are skipped with a warning.")]
		public List<string> PersistentDataFileAttachmentPaths;

		[Header("Windows")]
		[Tooltip("Use native crash reporting library (bugsplat-windows) for Windows builds. Captures native crashes in addition to .NET exceptions. Works with both Mono and IL2CPP. If set to false, will only use .NET handler.")]
		public bool UseNativeCrashReportingForWindows;

		[Tooltip("Upload .pdb, .dll and .exe symbols to BugSplat for Windows builds. On by default, unlike the other platforms - Windows has always uploaded automatically, so defaulting this off would silently stop existing projects symbolicating. Also requires Copy PDB Files in Build Settings, and a Windows editor.")]
		public bool UploadDebugSymbolsForWindows = true;

		[Tooltip("Show the BugSplat crash dialog when a native crash occurs on Windows (default). When disabled, crash reports are sent silently.")]
		public bool WindowsShowCrashDialog = true;

		[Tooltip("Native hang detection timeout in milliseconds for Windows. 0 (default) disables hang detection. When a hang is detected, BugSplat uploads a hang report and terminates the process, so choose a timeout longer than your longest expected frame (e.g. loading screens).")]
		public int WindowsHangDetectionTimeoutMs = 0;

		[Header("macOS")]
		[Tooltip("Use native crash reporting framework for macOS builds (requires IL2CPP). If set to false, will only use .NET handler.")]
		public bool UseNativeCrashReportingForMac;

		[Tooltip("Upload debug symbols (dSYMs) to BugSplat for macOS builds.")]
		public bool UploadDebugSymbolsForMac;

		[Tooltip("Submit macOS crash reports without asking the user. Off by default - the convention on desktop, and bugsplat-apple's own macOS default - so a native crash shows the BugSplat dialog on the next launch and the user can describe what happened. Maps to bugsplat-apple's autoSubmitCrashReport.")]
		public bool MacAutoSubmitCrashReport;

		[Tooltip("Submit macOS fatal hang reports without asking the user. On by default, because the app was frozen and then terminated so the user was never in a position to consent. Turn this off to ask instead, which also requires Mac Auto Submit Crash Report to be off. Maps to bugsplat-apple's autoSubmitFatalHangReport.")]
		public bool MacAutoSubmitFatalHangReport = true;

		[Tooltip("Seconds the macOS main thread must be blocked before BugSplat declares a hang. Defaults to 5, the top of bugsplat-apple's recommended 1-5 range, because Unity routinely blocks the main thread for seconds at a time on scene loads and shader warmup and a false positive costs a bogus hang report. Positive values below 0.1 are clamped to 0.1 by the tracker. Zero or less is not a usable threshold, so it falls back to bugsplat-apple's own default and logs a warning.")]
		public float MacHangDetectionThresholdSeconds = 5f;

		[Header("iOS")]
		[Tooltip("Use crash reporting framework for iOS builds. If set to false, will only use .NET handler.")]
		public bool UseNativeCrashReportingForIos;

		[Tooltip("Add a build script phase to the Xcode project to upload the Debug symbols to BugSplat.")]
		public bool UploadDebugSymbolsForIos;

		[Tooltip("Submit iOS crash reports without asking the user. On by default - the convention on mobile, and bugsplat-apple's own iOS default. Turn this off to show the BugSplat dialog on the next launch instead. Maps to bugsplat-apple's autoSubmitCrashReport.")]
		public bool IosAutoSubmitCrashReport = true;

		[Tooltip("Submit iOS fatal hang reports without asking the user. On by default, because the app was frozen and then terminated so the user was never in a position to consent. Turn this off to ask instead, which also requires Ios Auto Submit Crash Report to be off. Maps to bugsplat-apple's autoSubmitFatalHangReport.")]
		public bool IosAutoSubmitFatalHangReport = true;

		[Tooltip("Seconds the iOS main thread must be blocked before BugSplat declares a hang. Defaults to 5, the top of bugsplat-apple's recommended 1-5 range, because Unity routinely blocks the main thread for seconds at a time on scene loads and shader warmup and a false positive costs a bogus hang report. Positive values below 0.1 are clamped to 0.1 by the tracker. Zero or less is not a usable threshold, so it falls back to bugsplat-apple's own default and logs a warning.")]
		public float IosHangDetectionThresholdSeconds = 5f;

		[Header("Android")]
		[Tooltip("Use crash reporting library for Android builds. If set to false, will only use .NET handler.")]
		public bool UseNativeCrashReportingForAndroid;

		[Tooltip("Add a build script phase to upload the Debug symbols to BugSplat.")]
		public bool UploadDebugSymbolsForAndroid;

	}
}