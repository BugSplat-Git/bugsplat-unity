// P/Invoke surface for bugsplat.h, the C ABI of bugsplat-native. One declaration set serves every
// player platform: the library is "bugsplat" everywhere (BugSplat.dll on Windows, libbugsplat.dylib
// on macOS, libbugsplat.so on Linux and Android) except iOS, where Unity links native plugins
// statically and the symbols resolve through "__Internal".
//
// Strings are UTF-8 (LPUTF8Str) and the calling convention is cdecl. Nothing here is called from
// the editor or WebGL: BugSplatNativeSupported is false there and NativeCrashReporter short-circuits
// before any of these would run, so a missing library never throws a DllNotFoundException at edit
// time.
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#define BUGSPLAT_NATIVE_SUPPORTED
#endif

using System;
using System.Runtime.InteropServices;

namespace BugSplatUnity.Runtime.Native
{
	internal static class BugSplatNative
	{
#if UNITY_IOS && !UNITY_EDITOR
		private const string Lib = "__Internal";
#else
		private const string Lib = "bugsplat";
#endif

		internal const uint AbiVersion = 1;

		internal enum Result
		{
			Ok = 0, InvalidArgument = 1, AlreadyInitialized = 2, NotInitialized = 3, MonitorNotFound = 4,
			MonitorStartFailed = 5, ReporterNotFound = 6, Io = 7, LimitExceeded = 8, Unsupported = 9,
			Network = 10, Http = 11, Rejected = 12, Cancelled = 13, Internal = 14,
		}

		internal enum Capability
		{
			OutOfProcess = 1, Wer = 2, HangDetection = 3, CrashDialog = 4, OnCrashCallback = 5,
			FullMemoryDump = 6, DynamicAttachments = 7, CrashSignature = 8,
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct UploadResult
		{
			public uint StructSize;
			public int Result;
			public int HttpStatus;
			public long CrashId;
			public long StackKeyId;
			public IntPtr InfoUrl;

			public static UploadResult Create() => new UploadResult { StructSize = (uint)Marshal.SizeOf<UploadResult>() };
		}

		// The declarations are unconditional: a DllImport only binds when it is first called, and
		// NativeCrashReporter never calls one outside a supported player.
		// ---- options ----
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr bugsplat_options_new([MarshalAs(UnmanagedType.LPUTF8Str)] string database, [MarshalAs(UnmanagedType.LPUTF8Str)] string app, [MarshalAs(UnmanagedType.LPUTF8Str)] string version);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_free(IntPtr o);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_upload_policy(IntPtr o, int policy);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_store_dir(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_monitor_path(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_reporter_path(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_theme_dir(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_crash_type_id(IntPtr o, int id);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_dump_type(IntPtr o, int type);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_hang_detection(IntPtr o, int timeoutMs, int policy);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_crash_completion(IntPtr o, int completion);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_protect_exception_filter(IntPtr o, int enabled);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_open_support_url(IntPtr o, int enabled);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_crash_signature(IntPtr o, int enabled);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_chain_previous_signal_handlers(IntPtr o, int enabled);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_key(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_user(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_email(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_user_description(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_notes(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_environment(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_set_attribute(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_options_add_attachment(IntPtr o, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

		// ---- lifecycle ----
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_init(IntPtr options);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_is_initialized();
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_has_capability(int capability);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_shutdown();

		// ---- dynamic metadata ----
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_set_key([MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_set_user([MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_set_email([MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_set_user_description([MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_set_notes([MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_set_environment([MarshalAs(UnmanagedType.LPUTF8Str)] string v);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr bugsplat_get_environment();
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_set_attribute([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_add_attachment([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_remove_attachment([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_set_quiet_mode(int enabled);

		// ---- reports ----
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_capture_report();
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr bugsplat_report_new(int format);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_report_free(IntPtr r);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_report_set_platform(IntPtr r, [MarshalAs(UnmanagedType.LPUTF8Str)] string platform, [MarshalAs(UnmanagedType.LPUTF8Str)] string os);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_report_set_exception(IntPtr r, [MarshalAs(UnmanagedType.LPUTF8Str)] string code, [MarshalAs(UnmanagedType.LPUTF8Str)] string explanation);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_report_add_module(IntPtr r, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, ulong baseAddress, ulong size, [MarshalAs(UnmanagedType.LPUTF8Str)] string fileVersion, [MarshalAs(UnmanagedType.LPUTF8Str)] string productVersion);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_report_add_thread(IntPtr r, [MarshalAs(UnmanagedType.LPUTF8Str)] string id, int crashing);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_report_add_frame(IntPtr r, int thread, [MarshalAs(UnmanagedType.LPUTF8Str)] string function, [MarshalAs(UnmanagedType.LPUTF8Str)] string file, int line, [MarshalAs(UnmanagedType.LPUTF8Str)] string module, ulong address);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_report_add_attachment(IntPtr r, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_report_post(IntPtr r, ref UploadResult result);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_post_feedback([MarshalAs(UnmanagedType.LPUTF8Str)] string title, [MarshalAs(UnmanagedType.LPUTF8Str)] string description, IntPtr attachments, UIntPtr count, ref UploadResult result);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_post_pending_reports_async();
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_upload_result_free(ref UploadResult result);

		// ---- hang detection ----
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_heartbeat();
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int bugsplat_watch_thread([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void bugsplat_unwatch_thread();

		// ---- diagnostics ----
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr bugsplat_version_string();
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern uint bugsplat_abi_version();
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr bugsplat_log_file_path();

		/// <summary>Copies a UTF-8 C string the library owns; null for a null pointer.</summary>
		internal static string Utf8(IntPtr p)
		{
			if (p == IntPtr.Zero) return null;

			// Marshal.PtrToStringUTF8 is .NET Standard 2.1; count the bytes by hand so this also
			// compiles against the .NET Standard 2.0 API compatibility level.
			var length = 0;
			while (Marshal.ReadByte(p, length) != 0) length++;
			if (length == 0) return string.Empty;

			var bytes = new byte[length];
			Marshal.Copy(p, bytes, 0, length);
			return System.Text.Encoding.UTF8.GetString(bytes);
		}
	}
}
