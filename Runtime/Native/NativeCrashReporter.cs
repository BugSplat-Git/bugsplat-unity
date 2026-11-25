// The one native layer. Every player platform talks to bugsplat-native through the same C ABI;
// the platform differences (which process writes the dump, whether a dialog exists, where the
// helpers live) are the SDK's business. In the editor and on WebGL there is no native library:
// IsSupportedPlatform is false and every call is a no-op, so BugSplat.cs needs no platform #ifs.
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#define BUGSPLAT_NATIVE_SUPPORTED
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BugSplatUnity.Runtime.Native
{
	/// <summary>Outcome of an upload performed by the native SDK.</summary>
	internal struct NativeUploadResult
	{
		public BugSplatNative.Result Result;
		public int HttpStatus;
		public long CrashId;
		public long StackKeyId;
		public string InfoUrl;

		public bool Success => Result == BugSplatNative.Result.Ok;
		/// <summary>MANUAL policy leaves the report on disk for a later drain.</summary>
		public bool Pending => Result == BugSplatNative.Result.Ok && CrashId == 0;
	}

	/// <summary>One frame of a structured (managed) report.</summary>
	internal struct ReportFrame
	{
		public string Function;
		public string File;
		public int Line;
	}

	internal static class NativeCrashReporter
	{
		/// <summary>True on the player platforms bugsplat-native ships for; false in the editor and on WebGL.</summary>
		public static bool IsSupportedPlatform =>
#if BUGSPLAT_NATIVE_SUPPORTED
			true;
#else
			false;
#endif

		public static bool IsInitialized { get; private set; }

		// SystemInfo is main-thread only and structured reports post from a worker thread, so the
		// OS string is read once here, on the thread that calls Initialize.
		private static string operatingSystem = string.Empty;

		private static string initializedDatabase, initializedApplication, initializedVersion;

		/// <summary>The bugsplat-native version string, once initialized.</summary>
		public static string Version { get; private set; } = string.Empty;

		/// <summary>Why the last Initialize failed, for the log and for tests.</summary>
		public static string LastError { get; private set; } = string.Empty;

		/// <summary>
		/// Starts bugsplat-native for this process. Returns false, with LastError set, when the
		/// platform has no native library, when bugsplat_init refuses (BugSplatMonitor or
		/// BugSplatReporter missing next to the player), or when the library itself cannot load.
		/// Only the first call per process can succeed.
		/// </summary>
		public static bool Initialize(string database, string application, string version, NativeSettings settings,
			string key, string user, string email, string description, string notes,
			IEnumerable<KeyValuePair<string, string>> attributes, IEnumerable<string> attachments)
		{
#if BUGSPLAT_NATIVE_SUPPORTED
			if (IsInitialized)
			{
				if (database == initializedDatabase && application == initializedApplication && version == initializedVersion)
				{
					return true;
				}

				LastError = $"bugsplat-native is already running in this process for {initializedDatabase}/{initializedApplication}/{initializedVersion}; a second database, application or version cannot report native crashes.";
				return false;
			}

			settings = settings ?? new NativeSettings();

			try
			{
				if (BugSplatNative.bugsplat_abi_version() != BugSplatNative.AbiVersion)
				{
					LastError = $"bugsplat-native ABI {BugSplatNative.bugsplat_abi_version()} does not match the {BugSplatNative.AbiVersion} this package was built for.";
					return false;
				}

				var o = BugSplatNative.bugsplat_options_new(database, application, version);
				if (o == IntPtr.Zero)
				{
					LastError = "bugsplat_options_new returned null (database, application and version are required).";
					return false;
				}

				BugSplatNative.bugsplat_options_set_upload_policy(o, (int)settings.UploadPolicy);
				BugSplatNative.bugsplat_options_set_dump_type(o, (int)settings.DumpType);
				BugSplatNative.bugsplat_options_set_hang_detection(o, settings.HangDetectionTimeoutMs, (int)settings.HangPolicy);
				BugSplatNative.bugsplat_options_set_open_support_url(o, settings.OpenSupportUrl ? 1 : 0);
				BugSplatNative.bugsplat_options_set_crash_type_id(o, settings.CrashTypeId != 0 ? settings.CrashTypeId : DefaultCrashTypeId);
				// Unity's runtimes (Mono and IL2CPP) install their own signal handlers on the POSIX
				// platforms to turn faults in managed code into managed exceptions. They must see a
				// fault first, or a NullReferenceException becomes a native crash report.
				BugSplatNative.bugsplat_options_set_chain_previous_signal_handlers(o, 1);
#if UNITY_STANDALONE_OSX && ENABLE_CORECLR
				// CoreCLR takes the faults of managed code on thread-level Mach ports, so those never
				// reach bugsplat-native. Every other fault is dumped where it happens. Without this the
				// dump is written when the process dies, on whichever thread took the re-sent signal.
				// Not for Mono, whose null checks are faults nothing would stand in front of.
				BugSplatNative.bugsplat_options_set_mach_fault_exceptions(o, 1);
#endif
				// Terminate after the dump rather than running the CRT's exit path: a standalone
				// player's shutdown can hang inside its own teardown after a crash.
				BugSplatNative.bugsplat_options_set_crash_completion(o, 1 /* BUGSPLAT_COMPLETION_TERMINATE */);
				if (!string.IsNullOrEmpty(settings.StoreDir)) BugSplatNative.bugsplat_options_set_store_dir(o, settings.StoreDir);
				if (!string.IsNullOrEmpty(settings.MonitorPath)) BugSplatNative.bugsplat_options_set_monitor_path(o, settings.MonitorPath);
				if (!string.IsNullOrEmpty(settings.ReporterPath)) BugSplatNative.bugsplat_options_set_reporter_path(o, settings.ReporterPath);
				if (!string.IsNullOrEmpty(settings.ThemeDir)) BugSplatNative.bugsplat_options_set_theme_dir(o, settings.ThemeDir);
				if (!string.IsNullOrEmpty(key)) BugSplatNative.bugsplat_options_set_key(o, key);
				if (!string.IsNullOrEmpty(user)) BugSplatNative.bugsplat_options_set_user(o, user);
				if (!string.IsNullOrEmpty(email)) BugSplatNative.bugsplat_options_set_email(o, email);
				if (!string.IsNullOrEmpty(description)) BugSplatNative.bugsplat_options_set_user_description(o, description);
				if (!string.IsNullOrEmpty(notes)) BugSplatNative.bugsplat_options_set_notes(o, notes);
				if (attributes != null)
				{
					foreach (var kv in attributes)
					{
						if (!string.IsNullOrEmpty(kv.Key)) BugSplatNative.bugsplat_options_set_attribute(o, kv.Key, kv.Value ?? string.Empty);
					}
				}
				if (attachments != null)
				{
					foreach (var path in attachments)
					{
						if (!string.IsNullOrEmpty(path)) BugSplatNative.bugsplat_options_add_attachment(o, path);
					}
				}

				// bugsplat_init takes ownership of the options, success or not.
				BugSplatNative.Result result;
#if UNITY_STANDALONE_OSX && ENABLE_MONO
				// On macOS Mono's handlers have to go on top of bugsplat-native's: see MonoSignals.
				var monoHandover = MonoSignals.HandOver();
				try
				{
					result = (BugSplatNative.Result)BugSplatNative.bugsplat_init(o);
				}
				finally
				{
					if (monoHandover) MonoSignals.Reinstall();
				}
#else
				result = (BugSplatNative.Result)BugSplatNative.bugsplat_init(o);
#endif
				if (result != BugSplatNative.Result.Ok)
				{
					LastError = Describe(result);
					return false;
				}

				IsInitialized = true;
				initializedDatabase = database;
				initializedApplication = application;
				initializedVersion = version;
				Version = BugSplatNative.Utf8(BugSplatNative.bugsplat_version_string()) ?? string.Empty;
				try { operatingSystem = SystemInfo.operatingSystem ?? string.Empty; } catch (Exception) { operatingSystem = string.Empty; }
				LastError = string.Empty;
				return true;
			}
			catch (DllNotFoundException ex)
			{
				LastError = $"the bugsplat native library could not be loaded ({ex.Message}). It ships in Runtime/Plugins/<platform> of com.bugsplat.sdk; run Tools~/fetch-native-runtime to populate it.";
				return false;
			}
			catch (EntryPointNotFoundException ex)
			{
				LastError = $"the bugsplat native library is not ABI version {BugSplatNative.AbiVersion} ({ex.Message}).";
				return false;
			}
#else
			LastError = "native crash reporting is not available in the editor or on this platform.";
			return false;
#endif
		}

#if UNITY_STANDALONE_OSX && ENABLE_MONO && !UNITY_EDITOR
		/// <summary>
		/// Mono on macOS turns a null dereference into a NullReferenceException in a SIGSEGV handler,
		/// and ends the process itself when a thread it does not know faults. For both to work with
		/// bugsplat-native, Mono's handlers go on top of bugsplat-native's and pass on what is not
		/// theirs: Mono keeps its own faults, and the rest are dumped from the faulting thread.
		/// </summary>
		private static class MonoSignals
		{
			private const string LibSystem = "/usr/lib/libSystem.dylib";
			private static readonly IntPtr RtldDefault = new IntPtr(-2);
			private const int SIGQUIT = 3, SIGILL = 4, SIGABRT = 6, SIGFPE = 8, SIGBUS = 10, SIGSEGV = 11;

			[StructLayout(LayoutKind.Sequential)]
			private struct SigAction
			{
				public IntPtr Handler;
				public uint Mask;
				public int Flags;
			}

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetChaining(int enabled);

			[DllImport(LibSystem)] private static extern IntPtr dlsym(IntPtr handle, string symbol);
			[DllImport(LibSystem)] private static extern IntPtr signal(int sig, IntPtr handler);
			[DllImport(LibSystem)] private static extern int sigaction(int sig, IntPtr action, out SigAction previous);
			[DllImport(LibSystem)] private static extern int sigaction(int sig, ref SigAction action, IntPtr previous);

			// Internal to mscorlib, so reached by reflection; link.xml keeps it from being stripped.
			private static readonly System.Reflection.MethodInfo InstallSignalHandlers = typeof(object).Assembly
				.GetType("Mono.Runtime")
				?.GetMethod("InstallSignalHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

			/// <summary>
			/// Call right before bugsplat_init. Returns false, having changed nothing, when this Mono
			/// lacks what the handover needs; native crashes are then reported when the process dies.
			/// </summary>
			public static bool HandOver()
			{
				var setSignalChaining = dlsym(RtldDefault, "mono_set_signal_chaining");
				var setCrashChaining = dlsym(RtldDefault, "mono_set_crash_chaining");
				if (InstallSignalHandlers == null || setSignalChaining == IntPtr.Zero || setCrashChaining == IntPtr.Zero)
				{
					return false;
				}

				// Signal chaining makes Mono save the handlers it replaces and pass them the faults
				// that are not its own. Crash chaining off makes it pass a native crash on at once,
				// rather than first writing its own crash dump from inside the signal handler.
				Marshal.GetDelegateForFunctionPointer<SetChaining>(setSignalChaining)(1);
				Marshal.GetDelegateForFunctionPointer<SetChaining>(setCrashChaining)(0);

				// The signals Mono installs handlers for. Back to the default action, so that is what
				// bugsplat_init saves as the handler to call after its own. Were it Mono's, the two
				// would call each other forever once Mono's handlers are back on top.
				foreach (var sig in new[] { SIGABRT, SIGBUS, SIGFPE, SIGILL, SIGQUIT, SIGSEGV })
				{
					signal(sig, IntPtr.Zero);
				}

				return true;
			}

			/// <summary>Call after bugsplat_init, whether it succeeded or not: Mono's handlers are gone until then.</summary>
			public static void Reinstall()
			{
				var haveSigill = sigaction(SIGILL, IntPtr.Zero, out var sigill) == 0;
				InstallSignalHandlers.Invoke(null, null);

				// Mono's SIGILL handler, unlike its others, writes Mono's crash dump and aborts without
				// passing the fault on. Mono does not use SIGILL for managed code, so the handler
				// bugsplat_init installed (or the default, when it failed) gets the signal back.
				if (haveSigill) sigaction(SIGILL, ref sigill, IntPtr.Zero);
			}
		}
#endif

		/// <summary>Windows players keep BugSplat's UnityNative crash type so IL2CPP frames symbolicate through LineNumberMappings.json.</summary>
		private static int DefaultCrashTypeId =>
#if UNITY_STANDALONE_WIN
			15;
#else
			0;
#endif

		private static string Describe(BugSplatNative.Result result)
		{
			switch (result)
			{
				case BugSplatNative.Result.MonitorNotFound:
					return "BugSplatMonitor was not found next to the player. The package's post-build step copies it there when Use Native Crash Reporting is enabled; ship it with your game.";
				case BugSplatNative.Result.ReporterNotFound:
					return "BugSplatReporter was not found next to the player. The package's post-build step copies it (and its theme folder) there; ship them with your game.";
				case BugSplatNative.Result.MonitorStartFailed:
					return "BugSplatMonitor could not be started.";
				case BugSplatNative.Result.AlreadyInitialized:
					return "bugsplat-native is already initialized in this process.";
				default:
					return $"bugsplat_init failed with {result}.";
			}
		}

		public static void Shutdown()
		{
#if BUGSPLAT_NATIVE_SUPPORTED
			if (!IsInitialized) return;
			IsInitialized = false;
			try { BugSplatNative.bugsplat_shutdown(); } catch (Exception) { }
#endif
		}

		// ---- first-class report properties, changeable at any time from any thread ----

		public static void SetKey(string value) { if (IsInitialized) Guard(() => BugSplatNative.bugsplat_set_key(value ?? string.Empty)); }
		public static void SetUser(string value) { if (IsInitialized) Guard(() => BugSplatNative.bugsplat_set_user(value ?? string.Empty)); }
		public static void SetEmail(string value) { if (IsInitialized) Guard(() => BugSplatNative.bugsplat_set_email(value ?? string.Empty)); }
		public static void SetDescription(string value) { if (IsInitialized) Guard(() => BugSplatNative.bugsplat_set_user_description(value ?? string.Empty)); }
		public static void SetNotes(string value) { if (IsInitialized) Guard(() => BugSplatNative.bugsplat_set_notes(value ?? string.Empty)); }

		/// <summary>Null restores the SDK's own OS/hardware detection.</summary>
		public static void SetEnvironment(string value) { if (IsInitialized) Guard(() => BugSplatNative.bugsplat_set_environment(value)); }

		public static string GetEnvironment()
		{
#if BUGSPLAT_NATIVE_SUPPORTED
			if (!IsInitialized) return null;
			try { return BugSplatNative.Utf8(BugSplatNative.bugsplat_get_environment()); } catch (Exception) { return null; }
#else
			return null;
#endif
		}

		/// <summary>value == null removes the attribute.</summary>
		public static void SetAttribute(string name, string value)
		{
			if (!IsInitialized || string.IsNullOrEmpty(name)) return;
			Guard(() => BugSplatNative.bugsplat_set_attribute(name, value));
		}

		public static bool AddAttachment(string path)
		{
			if (!IsInitialized || string.IsNullOrEmpty(path)) return false;
			return Guard(() => BugSplatNative.bugsplat_add_attachment(path) == 0);
		}

		public static bool RemoveAttachment(string path)
		{
			if (!IsInitialized || string.IsNullOrEmpty(path)) return false;
			return Guard(() => BugSplatNative.bugsplat_remove_attachment(path) == 0);
		}

		public static void SetQuietMode(bool quiet) { if (IsInitialized) Guard(() => { BugSplatNative.bugsplat_set_quiet_mode(quiet ? 1 : 0); return 0; }); }

		/// <summary>Dump the live process out of process; the report flows like a crash. The game keeps running.</summary>
		public static bool CaptureReport() => IsInitialized && Guard(() => BugSplatNative.bugsplat_capture_report() == 0);

		public static void Heartbeat() { if (IsInitialized) Guard(() => { BugSplatNative.bugsplat_heartbeat(); return 0; }); }
		public static bool WatchThread(string name) => IsInitialized && Guard(() => BugSplatNative.bugsplat_watch_thread(name ?? "thread") == 0);
		public static void UnwatchThread() { if (IsInitialized) Guard(() => { BugSplatNative.bugsplat_unwatch_thread(); return 0; }); }

		public static void PostPendingReportsAsync() { if (IsInitialized) Guard(() => BugSplatNative.bugsplat_post_pending_reports_async()); }

		public static bool HasCapability(BugSplatNative.Capability capability) =>
			IsInitialized && Guard(() => BugSplatNative.bugsplat_has_capability((int)capability) == 1);

		public static string LogFilePath
		{
			get
			{
#if BUGSPLAT_NATIVE_SUPPORTED
				if (!IsInitialized) return null;
				try { return BugSplatNative.Utf8(BugSplatNative.bugsplat_log_file_path()); } catch (Exception) { return null; }
#else
				return null;
#endif
			}
		}

		/// <summary>
		/// Posts a structured (managed) report through the native SDK: stored, uploaded by the
		/// presigned flow, and given a crash id like every other report. Blocks until the upload
		/// completes, so call it off the main thread.
		/// </summary>
		public static NativeUploadResult PostStructuredReport(ManagedReportFormat format, string exceptionType, string message,
			IReadOnlyList<ReportFrame> frames, IEnumerable<string> attachments)
		{
#if BUGSPLAT_NATIVE_SUPPORTED
			if (!IsInitialized) return new NativeUploadResult { Result = BugSplatNative.Result.NotInitialized };

			var r = IntPtr.Zero;
			try
			{
				r = BugSplatNative.bugsplat_report_new((int)format);
				if (r == IntPtr.Zero) return new NativeUploadResult { Result = BugSplatNative.Result.Internal };

				BugSplatNative.bugsplat_report_set_platform(r, "Unity", operatingSystem);
				BugSplatNative.bugsplat_report_set_exception(r, exceptionType ?? "Exception", message ?? string.Empty);
				var thread = BugSplatNative.bugsplat_report_add_thread(r, "main", 1);
				if (frames == null || frames.Count == 0)
				{
					BugSplatNative.bugsplat_report_add_frame(r, thread, message ?? "(no stack)", string.Empty, 0, string.Empty, 0);
				}
				else
				{
					foreach (var f in frames)
					{
						BugSplatNative.bugsplat_report_add_frame(r, thread, f.Function ?? string.Empty, f.File ?? string.Empty, f.Line, string.Empty, 0);
					}
				}
				if (attachments != null)
				{
					foreach (var path in attachments)
					{
						if (!string.IsNullOrEmpty(path) && File.Exists(path)) BugSplatNative.bugsplat_report_add_attachment(r, path);
					}
				}

				var result = BugSplatNative.UploadResult.Create();
				var rc = (BugSplatNative.Result)BugSplatNative.bugsplat_report_post(r, ref result);
				return Take(rc, ref result);
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat error: native report post failed: {ex.Message}");
				return new NativeUploadResult { Result = BugSplatNative.Result.Internal };
			}
			finally
			{
				if (r != IntPtr.Zero) BugSplatNative.bugsplat_report_free(r);
			}
#else
			return new NativeUploadResult { Result = BugSplatNative.Result.Unsupported };
#endif
		}

		/// <summary>Posts user feedback (crash type 36). Blocks until uploaded; call off the main thread.</summary>
		public static NativeUploadResult PostFeedback(string title, string description, IReadOnlyList<string> attachments)
		{
#if BUGSPLAT_NATIVE_SUPPORTED
			if (!IsInitialized) return new NativeUploadResult { Result = BugSplatNative.Result.NotInitialized };

			var count = attachments?.Count ?? 0;
			var strings = new IntPtr[count];
			var array = IntPtr.Zero;
			try
			{
				for (var i = 0; i < count; i++)
				{
					var bytes = System.Text.Encoding.UTF8.GetBytes(attachments[i] + "\0");
					strings[i] = Marshal.AllocHGlobal(bytes.Length);
					Marshal.Copy(bytes, 0, strings[i], bytes.Length);
				}
				if (count > 0)
				{
					array = Marshal.AllocHGlobal(IntPtr.Size * count);
					Marshal.Copy(strings, 0, array, count);
				}

				var result = BugSplatNative.UploadResult.Create();
				var rc = (BugSplatNative.Result)BugSplatNative.bugsplat_post_feedback(title ?? string.Empty, description ?? string.Empty, array, (UIntPtr)count, ref result);
				return Take(rc, ref result);
			}
			catch (Exception ex)
			{
				Debug.LogError($"BugSplat error: native feedback post failed: {ex.Message}");
				return new NativeUploadResult { Result = BugSplatNative.Result.Internal };
			}
			finally
			{
				foreach (var p in strings) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
				if (array != IntPtr.Zero) Marshal.FreeHGlobal(array);
			}
#else
			return new NativeUploadResult { Result = BugSplatNative.Result.Unsupported };
#endif
		}

		private static NativeUploadResult Take(BugSplatNative.Result rc, ref BugSplatNative.UploadResult result)
		{
			var taken = new NativeUploadResult
			{
				Result = rc,
				HttpStatus = result.HttpStatus,
				CrashId = result.CrashId,
				StackKeyId = result.StackKeyId,
				InfoUrl = BugSplatNative.Utf8(result.InfoUrl) ?? string.Empty,
			};
			BugSplatNative.bugsplat_upload_result_free(ref result);
			return taken;
		}

		// A library that vanished from under a running player (or an ABI mismatch that slipped past
		// init) must never take the game down over a metadata call.
		private static bool Guard(Func<bool> call)
		{
			try { return call(); }
			catch (Exception ex) { Debug.LogWarning($"BugSplat warning: native call failed: {ex.Message}"); return false; }
		}

		private static void Guard(Func<int> call)
		{
			try { call(); }
			catch (Exception ex) { Debug.LogWarning($"BugSplat warning: native call failed: {ex.Message}"); }
		}
	}
}
