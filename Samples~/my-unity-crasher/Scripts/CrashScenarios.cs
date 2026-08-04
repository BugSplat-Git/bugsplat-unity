using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BugSplat = BugSplatUnity.BugSplat;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif

namespace Crasher
{
	/// <summary>
	/// Which BugSplat mechanism is expected to capture a scenario. Knowing this up front is the
	/// point of the menu: a report showing up is only meaningful if it arrived by the path you
	/// were testing.
	/// </summary>
	public enum CapturePath
	{
		/// <summary>BugSplat's unhandled exception filter, in-process, dumped by BugSplatMonitor.</summary>
		NativeHandler,

		/// <summary>
		/// BugSplatWer.dll, via Windows Error Reporting. Fail-fast terminations bypass every
		/// in-process handler, so these are only reported when the WER handler is registered.
		/// </summary>
		WindowsErrorReporting,

		/// <summary>The managed handler, via Application.logMessageReceived.</summary>
		ManagedHandler,

		/// <summary>An explicit bugsplat.Post call rather than an automatic capture.</summary>
		ManualPost,

		/// <summary>BugSplatMonitor's hang watchdog.</summary>
		HangWatchdog,

		/// <summary>Known not to produce a report today. Included to document the gap.</summary>
		NotCaptured
	}

	public sealed class CrashScenario
	{
		public string Name;
		public string Expected;
		public CapturePath Path;

		/// <summary>The process does not survive this scenario, so testing it needs a relaunch.</summary>
		public bool Terminates;

		/// <summary>
		/// Safe to run inside the editor. False for anything native: BugSplat.dll is excluded from
		/// the editor, so there would be no reporter anyway, and the crash would take the editor
		/// down with any unsaved work.
		/// </summary>
		public bool RunsInEditor;

		public Action<ICrashScenarioHost> Run;
	}

	/// <summary>
	/// Services a scenario needs from whatever MonoBehaviour is hosting the menu: coroutines, a
	/// main-thread dispatcher for callbacks that arrive on other threads, and the BugSplat client.
	/// </summary>
	public interface ICrashScenarioHost
	{
		BugSplat BugSplat { get; }
		Coroutine Run(IEnumerator routine);
		void OnMainThread(Action action);
	}

	public static class CrashScenarios
	{
		public static IReadOnlyList<CrashScenario> All => all;

		static readonly List<CrashScenario> all = Build();

		static List<CrashScenario> Build()
		{
			var scenarios = new List<CrashScenario>();

			// ---- Managed. The player survives all of these. ----

			scenarios.Add(new CrashScenario
			{
				Name = "Unhandled managed exception",
				Expected = "Reported via the log callback. The player keeps running.",
				Path = CapturePath.ManagedHandler,
				Terminates = false,
				RunsInEditor = true,
				Run = _ => ThrowFromSampleFrames()
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Exception inside a coroutine",
				Expected = "Unity logs it on the main thread, so it reports. The coroutine stops.",
				Path = CapturePath.ManagedHandler,
				Terminates = false,
				RunsInEditor = true,
				Run = host => host.Run(ThrowNextFrame())
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Exception on a background thread",
				Expected =
					"KNOWN GAP: appears in Player.log but produces no report. Unity only raises " +
					"logMessageReceived for main-thread logs.",
				Path = CapturePath.NotCaptured,
				Terminates = false,
				RunsInEditor = true,
				Run = _ => RunOnBackgroundThread(
					() => throw new Exception("BugSplat sample: exception on a background thread"))
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Caught exception, posted manually",
				Expected = "Reported by an explicit bugsplat.Post with an overridden description.",
				Path = CapturePath.ManualPost,
				Terminates = false,
				RunsInEditor = true,
				Run = host => PostCaughtException(host)
			});

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

			// ---- Native, captured by BugSplat's unhandled exception filter. ----

			scenarios.Add(new CrashScenario
			{
				Name = "Access violation — write",
				Expected = "Native report: EXCEPTION_ACCESS_VIOLATION writing address 0.",
				Path = CapturePath.NativeHandler,
				Terminates = true,
				RunsInEditor = false,
				Run = host => Crash(host, "AccessViolationWrite", AccessViolationWrite)
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Access violation — read",
				Expected = "Native report: EXCEPTION_ACCESS_VIOLATION reading address 0.",
				Path = CapturePath.NativeHandler,
				Terminates = true,
				RunsInEditor = false,
				Run = host => Crash(host, "AccessViolationRead", AccessViolationRead)
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Access violation — background thread",
				Expected = "Native report whose faulting thread is not the main thread.",
				Path = CapturePath.NativeHandler,
				Terminates = true,
				RunsInEditor = false,
				Run = host =>
				{
					Describe(host, "AccessViolationBackgroundThread");
					RunOnBackgroundThread(AccessViolationWrite);
				}
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Custom SEH exception",
				Expected = "Native report with code 0xE0BADBAD — proves the filter is not AV-specific.",
				Path = CapturePath.NativeHandler,
				Terminates = true,
				RunsInEditor = false,
				Run = host => Crash(host, "CustomSehException",
					() => RaiseException(0xE0BADBAD, 0, 0, IntPtr.Zero))
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Stack overflow",
				Expected =
					"Backend-dependent. IL2CPP: native 0xC00000FD. Mono: converted to a managed " +
					"StackOverflowException or an abort. Record what actually happens.",
				Path = CapturePath.NativeHandler,
				Terminates = true,
				RunsInEditor = false,
				Run = host => Crash(host, "StackOverflow", () => Sink = Overflow(0))
			});

			// ---- Fail-fast. These bypass the filter entirely and need the WER handler. ----

			scenarios.Add(new CrashScenario
			{
				Name = "Fail-fast (0xC0000602)",
				Expected =
					"WER only. Armed: native report, STATUS_FAIL_FAST_EXCEPTION. Not armed: no " +
					"report, and a dump lands in %LOCALAPPDATA%\\CrashDumps.",
				Path = CapturePath.WindowsErrorReporting,
				Terminates = true,
				RunsInEditor = false,
				Run = host => Crash(host, "FailFast",
					() => RaiseFailFastException(IntPtr.Zero, IntPtr.Zero, FailFastGenerateExceptionAddress))
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Fail-fast as stack buffer overrun (0xC0000409)",
				Expected =
					"WER only. The signature a /GS cookie failure or __fastfail produces in the field.",
				Path = CapturePath.WindowsErrorReporting,
				Terminates = true,
				RunsInEditor = false,
				Run = host => Crash(host, "StackBufferOverrun", FailFastAsStackBufferOverrun)
			});

			scenarios.Add(new CrashScenario
			{
				Name = "Heap corruption (0xC0000374)",
				Expected =
					"WER only. A double free on the process heap. On Win8+ the heap reports " +
					"corruption via a fail-fast, so this bypasses the filter too.",
				Path = CapturePath.WindowsErrorReporting,
				Terminates = true,
				RunsInEditor = false,
				Run = host => Crash(host, "HeapCorruption", CorruptProcessHeap)
			});

			// ---- Hang. ----

			scenarios.Add(new CrashScenario
			{
				Name = "Main-thread hang",
				Expected =
					"Arms hang detection at 5s, then blocks the main thread for 30s. BugSplatMonitor " +
					"notices the window stopped pumping, uploads a hang report, and terminates.",
				Path = CapturePath.HangWatchdog,
				Terminates = true,
				RunsInEditor = false,
				Run = host => host.Run(HangAfterArmingDetection(host))
			});
#endif

			return scenarios;
		}

		// ---- Managed implementations ----

		/// <summary>Shared with ErrorGenerator so the sample has one implementation per trigger.</summary>
		internal static void ThrowFromSampleFrames() => SampleStackFrame0();
		static void SampleStackFrame0() => SampleStackFrame1();
		static void SampleStackFrame1() => SampleStackFrame2();
		static void SampleStackFrame2() => throw new Exception("BugSplat rocks!");

		static IEnumerator ThrowNextFrame()
		{
			yield return null;
			throw new Exception("BugSplat sample: exception inside a coroutine");
		}

		static void PostCaughtException(ICrashScenarioHost host)
		{
			try
			{
				ThrowFromSampleFrames();
			}
			catch (Exception ex)
			{
				var options = new BugSplatUnity.ReportPostOptions { Description = "a new description" };
				host.Run(host.BugSplat.Post(ex, options));
			}
		}

		static void RunOnBackgroundThread(Action action)
		{
			var thread = new System.Threading.Thread(() => action())
			{
				IsBackground = true,
				Name = "BugSplatSampleCrashThread"
			};
			thread.Start();
		}

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

		// Every fail-fast scenario faults at the same address inside ntdll, so without a
		// distinguishing field they all collapse into one bucket in the dashboard.
		static void Describe(ICrashScenarioHost host, string scenarioKey)
		{
			host.BugSplat.Key = scenarioKey;
			host.BugSplat.Description = $"BugSplat sample scenario: {scenarioKey}";
		}

		static void Crash(ICrashScenarioHost host, string scenarioKey, Action crash)
		{
			Describe(host, scenarioKey);
			crash();
		}

		// A raw write through a null pointer is a hardware access violation, not a managed
		// NullReferenceException. The fault happens inside the runtime's native marshalling code
		// rather than in JIT'd managed code, so Mono's vectored handler declines it and it reaches
		// BugSplat's filter. NoInlining keeps the frames distinct through IL2CPP and MSVC
		// optimization so the report shows a game-code call stack.
		/// <summary>Shared with ErrorGenerator so the sample has one implementation per trigger.</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void AccessViolationWrite() => NativeCrashFrame0();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame0() => NativeCrashFrame1();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame1() => NativeCrashFrame2();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame2() => Marshal.WriteInt32(IntPtr.Zero, 0);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void AccessViolationRead() => Sink = Marshal.ReadInt32(IntPtr.Zero);

		static volatile int Sink;

		// '+ depth' makes the call non-tail-recursive, so it cannot be rewritten into a loop —
		// which would hang instead of overflowing.
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Overflow(int depth) => Overflow(depth + 1) + depth;

		/// <summary>
		/// Raises a fail-fast carrying STATUS_STACK_BUFFER_OVERRUN, the code a /GS cookie failure
		/// or __fastfail produces. RaiseFailFastException on its own reports 0xC0000602, so the
		/// code has to come from a supplied EXCEPTION_RECORD.
		/// </summary>
		static void FailFastAsStackBufferOverrun()
		{
			// Written by explicit offset rather than a marshalled struct: EXCEPTION_RECORD's
			// layout differs across x86/x64/ARM64 and only three fields matter here.
			var size = IntPtr.Size == 8 ? 152 : 80;
			var record = Marshal.AllocHGlobal(size);
			for (var offset = 0; offset < size; offset += 4)
			{
				Marshal.WriteInt32(record, offset, 0);
			}

			Marshal.WriteInt32(record, 0, unchecked((int)0xC0000409)); // ExceptionCode
			Marshal.WriteInt32(record, 4, 1);                          // EXCEPTION_NONCONTINUABLE

			RaiseFailFastException(record, IntPtr.Zero, FailFastGenerateExceptionAddress);
		}

		/// <summary>
		/// Double-frees a process-heap block. Deliberately uses the kernel32 heap APIs rather than
		/// Marshal.AllocHGlobal: Mono routes that through the UCRT (which may trip an invalid-
		/// parameter check first) and IL2CPP routes it through Unity's own allocator, where a
		/// double free is undefined and may produce nothing at all.
		/// </summary>
		static void CorruptProcessHeap()
		{
			// Termination-on-corruption is on by default for most modern processes and cannot be
			// turned back off; setting it explicitly makes the behaviour deterministic across
			// Windows builds and both scripting backends.
			HeapSetInformation(IntPtr.Zero, HeapEnableTerminationOnCorruption, IntPtr.Zero, UIntPtr.Zero);

			var heap = GetProcessHeap();
			var block = HeapAlloc(heap, 0, (UIntPtr)128);
			HeapFree(heap, 0, block);
			HeapFree(heap, 0, block);

			// If the heap did not notice the double free, smash the block and churn allocations to
			// force validation of the corrupted metadata.
			for (var offset = 0; offset < 512; offset += 4)
			{
				Marshal.WriteInt32(block, offset, unchecked((int)0xBAADF00D));
			}

			for (var i = 0; i < 64; i++)
			{
				HeapFree(heap, 0, HeapAlloc(heap, 0, (UIntPtr)128));
			}

			Debug.LogError(
				"BugSplat sample: the heap did not report corruption on this Windows build, so the " +
				"player is still running. Use one of the fail-fast scenarios to exercise WER instead.");
		}

		static IEnumerator HangAfterArmingDetection(ICrashScenarioHost host)
		{
			host.BugSplat.SetWindowsHangDetectionTimeout(5000);
			Describe(host, "MainThreadHang");

			// Let the timeout reach the monitor through shared memory, and let this frame finish
			// rendering, before wedging the main thread.
			yield return null;
			yield return null;

			System.Threading.Thread.Sleep(30000);
		}

		// kernel32 exports are WINAPI, which is stdcall on x86 — leave CallingConvention at the
		// default Winapi rather than copying Cdecl from the BugSplat.dll imports.

		const uint FailFastGenerateExceptionAddress = 0x1;
		const int HeapEnableTerminationOnCorruption = 1;

		[DllImport("kernel32.dll")]
		static extern void RaiseFailFastException(IntPtr exceptionRecord, IntPtr contextRecord, uint flags);

		[DllImport("kernel32.dll")]
		static extern void RaiseException(uint code, uint flags, uint argumentCount, IntPtr arguments);

		[DllImport("kernel32.dll")]
		static extern IntPtr GetProcessHeap();

		[DllImport("kernel32.dll")]
		static extern IntPtr HeapAlloc(IntPtr heap, uint flags, UIntPtr bytes);

		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool HeapFree(IntPtr heap, uint flags, IntPtr memory);

		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool HeapSetInformation(IntPtr heap, int infoClass, IntPtr info, UIntPtr infoLength);
#endif
	}
}
