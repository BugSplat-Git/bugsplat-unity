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

		[Header("Initialization")]
		[Tooltip("Initialize BugSplat from this asset before the first scene loads. On by default, so nothing has to be placed in a scene. Turn it off to call BugSplat.Initialize yourself - after a consent screen, for example.")]
		public bool InitializeAutomatically = true;

		[Tooltip("Register a callback and report LogType.Exception log messages as they happen.")]
		public bool RegisterLogMessageReceived = true;

		[Tooltip("Also capture unhandled exceptions thrown on background threads. Unity only raises logMessageReceived for main-thread logs, so without this those exceptions are written to the log but never reported. Requires Register Log Message Received.")]
		public bool CaptureExceptionsOnBackgroundThreads = true;

		[Tooltip("Also capture exceptions from Tasks that faulted and were never awaited. These never reach Unity's log at all, so they are otherwise invisible. They surface only after a garbage collection notices the Task, so they are reported late and are not guaranteed to be reported before the process exits. Requires Register Log Message Received.")]
		public bool CaptureUnobservedTaskExceptions = true;

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

		/// <summary>
		/// The key under which the editor stores the project's options asset in EditorBuildSettings.
		/// The build step carries that asset into the player as a preloaded asset.
		/// </summary>
		internal const string ConfigObjectKey = "com.bugsplat.unity.options";

		/// <summary>
		/// Scripting define that hands initialization to the project: BugSplat neither initializes itself
		/// nor fails a build for a missing asset. For code that calls BugSplat.Initialize itself.
		/// </summary>
		internal const string ManualInitializeDefine = "BUGSPLAT_MANUAL_INITIALIZE";

		/// <summary>
		/// The fix every "not configured" message ends with. It names the menu and the file, because the
		/// reader may be a person at the editor or a script reading a log - and both fixes work.
		/// </summary>
		internal const string ConfigureHint =
			"Open Edit > Project Settings > BugSplat, or add a BugSplatOptions asset anywhere under Assets/ - a " +
			"single asset is selected automatically. Scripted setup: " +
			"https://github.com/BugSplat-Git/bugsplat-unity/blob/main/Documentation~/automation.md";

		private static BugSplatOptions preloaded;

		// In a player the configured asset arrives as a preloaded asset, loaded before
		// RuntimeInitializeOnLoadMethod(BeforeSceneLoad) runs, and OnEnable is where a preloaded
		// ScriptableObject can announce itself. First wins: preloaded assets load ahead of any scene,
		// so an asset referenced by an obsolete BugSplatManager in a scene never displaces it.
		private void OnEnable()
		{
			if (preloaded == null)
			{
				preloaded = this;
			}
		}

		/// <summary>
		/// The options asset the project initializes from, or null when none is selected. In the editor
		/// this is the Project Settings selection; in a player it is the preloaded asset the build step
		/// added from that selection.
		/// </summary>
		internal static BugSplatOptions ResolveConfigured()
		{
#if UNITY_EDITOR
			return UnityEditor.EditorBuildSettings.TryGetConfigObject(ConfigObjectKey, out BugSplatOptions options)
				? options
				: null;
#else
			return preloaded;
#endif
		}
	}
}
