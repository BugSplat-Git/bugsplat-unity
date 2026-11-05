using System;

namespace BugSplatUnity
{
	/// <summary>What happens to a native report once it exists on disk.</summary>
	public enum UploadPolicy
	{
		/// <summary>Desktop: show the BugSplat crash dialog, then upload. Mobile: upload on the next launch.</summary>
		Dialog = 0,
		/// <summary>Upload without any UI.</summary>
		Quiet = 1,
		/// <summary>Leave reports on disk; the app drains them with <see cref="BugSplat.PostPendingReportsAsync"/>.</summary>
		Manual = 2,
	}

	/// <summary>How much process memory a crash dump carries. Size is enforced server-side at upload.</summary>
	public enum DumpType
	{
		/// <summary>Threads, stacks, modules and the exception. The Crashpad default.</summary>
		Normal = 0,
		/// <summary>Plus private writable memory: the managed heaps, so IL2CPP and Mono objects resolve in the debugger.</summary>
		Heap = 1,
		/// <summary>Every readable region.</summary>
		Full = 2,
	}

	/// <summary>What BugSplat does when the main thread stops responding for longer than the hang timeout.</summary>
	public enum HangPolicy
	{
		/// <summary>Dump the live process out of process, upload a hang report, and let the game continue.</summary>
		Report = 0,
		/// <summary>Report, offer the user Wait / Close, and terminate the game on Close.</summary>
		ReportAndTerminate = 1,
	}

	/// <summary>Serialization of managed exception reports (BugSplat structured reports, crash type 21).</summary>
	public enum ManagedReportFormat
	{
		/// <summary>BugSplat's bsCrashReport.xml schema. Processed by every BugSplat server today.</summary>
		Xml = 0,
		/// <summary>The 1:1 JSON mirror of the XML schema. Requires a server that accepts JSON structured reports.</summary>
		Json = 1,
	}

	/// <summary>
	/// Settings applied to the native crash reporter (bugsplat-native) when BugSplat starts. Every
	/// platform shares this one set: the same out-of-process BugSplatMonitor writes the dump on
	/// Windows, macOS, Linux and Android, and the in-process handler does on iOS.
	/// </summary>
	[Serializable]
	public sealed class NativeSettings
	{
		public UploadPolicy UploadPolicy = UploadPolicy.Dialog;
		public DumpType DumpType = DumpType.Normal;

		/// <summary>0 disables hang detection.</summary>
		public int HangDetectionTimeoutMs;
		public HangPolicy HangPolicy = HangPolicy.Report;

		/// <summary>Open the support-response URL after an interactive upload (desktop only).</summary>
		public bool OpenSupportUrl = true;

		/// <summary>
		/// BugSplat crash type for native dumps. 0 uses the SDK's default for the platform; Windows
		/// players use 15 (UnityNative) so the backend applies LineNumberMappings.json to IL2CPP frames.
		/// </summary>
		public int CrashTypeId;

		public ManagedReportFormat ManagedReportFormat = ManagedReportFormat.Xml;

		/// <summary>Override where reports are stored. Null uses the SDK's per-platform default.</summary>
		public string StoreDir;

		/// <summary>Override where BugSplatMonitor / BugSplatReporter / the theme live. Null lets the SDK find them next to the player.</summary>
		public string MonitorPath;
		public string ReporterPath;
		public string ThemeDir;

		public NativeSettings Clone() => (NativeSettings)MemberwiseClone();
	}
}
