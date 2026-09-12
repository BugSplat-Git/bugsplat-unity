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

		[Tooltip("Paths to files, relative to Application.persistentDataPath, to attach to managed reports and to native crash reports. Entries that are not relative are skipped with a warning.")]
		public List<string> PersistentDataFileAttachmentPaths;

		[Header("Native Crash Reporting")]
		[Tooltip("Start bugsplat-native in players on Windows, macOS, Linux, Android and iOS: native crashes, hangs and captures are reported out of process, and managed exceptions post through the same SDK. Works with both Mono and IL2CPP. Has no effect in the editor or on WebGL. When off, only the .NET handler runs and reports post directly over HTTP.")]
		public bool UseNativeCrashReporting = true;

		[Tooltip("What happens to a native report once it exists. Dialog: the BugSplat crash dialog on desktop (mobile uploads on the next launch). Quiet: upload with no UI. Manual: leave reports on disk for PostPendingReportsAsync.")]
		public UploadPolicy UploadPolicy = UploadPolicy.Dialog;

		[Tooltip("How much process memory a crash dump carries. Heap includes the managed heaps so objects resolve in a debugger; Full is every readable region. Larger dumps take longer to upload and are subject to your database's size limit.")]
		public DumpType DumpType = DumpType.Normal;

		[Tooltip("Hang detection timeout in milliseconds; 0 (default) disables it. Choose a timeout longer than your longest expected frame (loading screens, shader warmup), or those get reported as hangs. BugSplatManager sends the heartbeat every frame.")]
		public int HangDetectionTimeoutMs = 0;

		[Tooltip("Report: dump the live process, upload a hang report and let the game continue. Report And Terminate: also offer the user Wait / Close and end the game on Close.")]
		public HangPolicy HangPolicy = HangPolicy.Report;

		[Tooltip("Open the support-response URL in the browser after an interactive crash report upload on desktop.")]
		public bool OpenSupportUrl = true;

		[Tooltip("Serialization of managed exception reports posted through the native SDK. XML is processed by every BugSplat server; JSON requires a server that accepts JSON structured reports.")]
		public ManagedReportFormat ManagedReportFormat = ManagedReportFormat.Xml;

		[Header("Symbols")]
		[Tooltip("Upload .pdb, .dll and .exe symbols to BugSplat for Windows builds. On by default. Also requires Copy PDB Files in Build Settings, and a Windows editor.")]
		public bool UploadDebugSymbolsForWindows = true;

		[Tooltip("Upload debug symbols (dSYMs, as Breakpad .sym files) to BugSplat for macOS builds.")]
		public bool UploadDebugSymbolsForMac;

		[Tooltip("Upload debug symbols (as Breakpad .sym files) to BugSplat for Linux builds.")]
		public bool UploadDebugSymbolsForLinux;

		[Tooltip("Add a build script phase to the Xcode project to upload the debug symbols to BugSplat.")]
		public bool UploadDebugSymbolsForIos;

		[Tooltip("Upload the symbols.zip Unity generates for Android builds to BugSplat.")]
		public bool UploadDebugSymbolsForAndroid;
	}
}
