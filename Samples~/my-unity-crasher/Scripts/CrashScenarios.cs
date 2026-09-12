using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using BugSplat = BugSplatUnity.BugSplat;

namespace Crasher
{
	/// <summary>
	/// A group of scenarios captured by the same BugSplat mechanism. Knowing the mechanism up
	/// front is the point of the menu: a report showing up is only meaningful if it arrived by
	/// the path you were testing.
	/// </summary>
	public sealed class ScenarioGroup
	{
		public string Title;
		public string Subtitle;
		public List<CrashScenario> Scenarios = new List<CrashScenario>();
	}

	public sealed class CrashScenario
	{
		public string Name;
		public string Expected;

		/// <summary>Not captured by the SDK today. Included to document the gap.</summary>
		public bool KnownGap;

		/// <summary>
		/// Only reported when the Windows Error Reporting handler is registered. The menu disables
		/// these when it isn't, because they otherwise terminate the player and report nothing.
		/// </summary>
		public bool RequiresWer;

		/// <summary>
		/// Safe to run inside the editor. False for anything native: bugsplat-native does not run
		/// in the editor, so there would be no report anyway, and the crash would take the editor
		/// down with any unsaved work.
		/// </summary>
		public bool RunsInEditor;

		public Action<ICrashScenarioHost> Run;
	}

	/// <summary>
	/// Services a scenario needs from whatever MonoBehaviour is hosting the menu: coroutines, the
	/// BugSplat client, and the feedback dialog.
	/// </summary>
	public interface ICrashScenarioHost
	{
		BugSplat BugSplat { get; }
		Coroutine Run(IEnumerator routine);
		void ShowFeedback();
	}

	/// <summary>
	/// The scenario table, built per platform. Every player platform shares bugsplat-native, so
	/// the NATIVE, CAPTURE and HANG sections are the same everywhere; Windows adds the fail-fast
	/// rows that only Windows Error Reporting can deliver. In the editor the native rows for the
	/// current build target are listed but disabled.
	/// </summary>
	public static class CrashScenarios
	{
		public static IReadOnlyList<ScenarioGroup> Groups => groups;

		static readonly List<ScenarioGroup> groups = Build();

		static List<ScenarioGroup> Build()
		{
			var result = new List<ScenarioGroup> { BuildManaged() };

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_IOS || UNITY_ANDROID
			result.Add(BuildNative());
#endif
#if UNITY_STANDALONE_WIN
			result.Add(BuildWindowsFailFast());
#endif
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_IOS || UNITY_ANDROID
			result.Add(BuildHang());
#endif

			result.Add(BuildFeedback());
			return result;
		}

		// ---- Managed. Identical on every platform; the player survives all of these. ----

		static ScenarioGroup BuildManaged() => new ScenarioGroup
		{
			Title = "MANAGED",
			Subtitle = "C# exceptions, captured by BugSplat's .NET handler and posted as structured reports. The player keeps running.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Unhandled managed exception",
					Expected = "Reported via the log callback. The player keeps running.",
					RunsInEditor = true,
					Run = _ => ThrowFromSampleFrames(ManagedScenario.Unhandled)
				},
				new CrashScenario
				{
					Name = "Exception inside a coroutine",
					Expected = "Unity logs it on the main thread, so it reports. The coroutine stops.",
					RunsInEditor = true,
					Run = host => host.Run(ThrowNextFrame())
				},
				new CrashScenario
				{
					Name = "Exception on a background thread",
					Expected =
						"Queued by the threaded log callback and posted from the main thread. Reported " +
						"once, not twice. Requires Capture Exceptions On Background Threads.",
					RunsInEditor = true,
					Run = _ => RunOnBackgroundThread(
						() => ThrowFromSampleFrames(ManagedScenario.BackgroundThread))
				},
				new CrashScenario
				{
					Name = "Unobserved Task exception",
					Expected =
						"A Task nobody awaited. Never reaches Unity's log, so BugSplat subscribes to " +
						"TaskScheduler.UnobservedTaskException directly. Reported after the GC collects " +
						"the Task. Requires Capture Unobserved Task Exceptions.",
					RunsInEditor = true,
					Run = host => host.Run(FaultUnobservedTask())
				},
				new CrashScenario
				{
					Name = "Caught exception, posted manually",
					Expected =
						"Reported by an explicit bugsplat.Post with an overridden description. The " +
						"crash id and support URL are logged when the post completes.",
					RunsInEditor = true,
					Run = PostCaughtException
				}
			}
		};

		// ---- Feedback. Available everywhere. ----

		static ScenarioGroup BuildFeedback() => new ScenarioGroup
		{
			Title = "FEEDBACK",
			Subtitle = "Non-crash reports submitted by the user.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Leave feedback",
					Expected = "Opens the feedback dialog; submits via bugsplat.PostFeedback.",
					RunsInEditor = true,
					Run = host => host.ShowFeedback()
				}
			}
		};

		// ---- Managed implementations ----

		/// <summary>
		/// Which managed row is being run. Threaded through the shared sample frames so the frame that
		/// actually throws is named after the scenario, putting that name at the top of the reported
		/// stack. Without it every managed row reports the same SampleStackFrame2 and the rows are
		/// indistinguishable on the dashboard.
		/// </summary>
		enum ManagedScenario
		{
			Unhandled,
			Coroutine,
			BackgroundThread,
			UnobservedTask,
			CaughtAndPosted
		}

		// NoInlining throughout: these are one-line calls, and IL2CPP inlines them in release builds,
		// which is exactly where these stacks get read.
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ThrowFromSampleFrames(ManagedScenario scenario) => SampleStackFrame0(scenario);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void SampleStackFrame0(ManagedScenario scenario) => SampleStackFrame1(scenario);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void SampleStackFrame1(ManagedScenario scenario) => SampleStackFrame2(scenario);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void SampleStackFrame2(ManagedScenario scenario)
		{
			switch (scenario)
			{
				case ManagedScenario.Coroutine: ThrowCoroutineException(); break;
				case ManagedScenario.BackgroundThread: ThrowBackgroundThreadException(); break;
				case ManagedScenario.UnobservedTask: ThrowUnobservedTaskException(); break;
				case ManagedScenario.CaughtAndPosted: ThrowCaughtAndPostedException(); break;
				default: ThrowUnhandledManagedException(); break;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ThrowUnhandledManagedException() =>
			throw new Exception("BugSplat sample: unhandled managed exception");

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ThrowCoroutineException() =>
			throw new Exception("BugSplat sample: exception inside a coroutine");

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ThrowBackgroundThreadException() =>
			throw new Exception("BugSplat sample: exception on a background thread");

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ThrowUnobservedTaskException() =>
			throw new Exception("BugSplat sample: unobserved Task exception");

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ThrowCaughtAndPostedException() =>
			throw new Exception("BugSplat sample: caught exception, posted manually");

		static IEnumerator ThrowNextFrame()
		{
			yield return null;
			ThrowFromSampleFrames(ManagedScenario.Coroutine);
		}

		/// <summary>
		/// Faults a Task nobody awaits, then forces the collection that makes the runtime notice.
		/// Without the GC the exception can sit unobserved indefinitely.
		/// </summary>
		static IEnumerator FaultUnobservedTask()
		{
			faultedTaskThrew = false;
			StartFaultedTask();

			// Waiting a fixed number of frames would make this flaky: under load Task.Run may not
			// have reached the throw yet, and collecting while the Task is still live raises
			// nothing at all — the row would silently do nothing rather than fail.
			var deadline = Time.realtimeSinceStartup + FaultedTaskTimeoutSeconds;
			while (!faultedTaskThrew && Time.realtimeSinceStartup < deadline)
			{
				yield return null;
			}

			if (!faultedTaskThrew)
			{
				Debug.LogWarning(
					$"BugSplat sample: the faulted Task did not run within {FaultedTaskTimeoutSeconds}s, " +
					"so there is nothing unobserved to collect. Try the scenario again.");
				yield break;
			}

			// The flag is set as the exception unwinds, a moment before the Task transitions to
			// Faulted and its last reference goes out of scope. Give that a frame to settle,
			// otherwise the collection can run while the Task is still reachable.
			yield return null;

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		const float FaultedTaskTimeoutSeconds = 5f;

		static volatile bool faultedTaskThrew;

		// Kept out of the coroutine so the Task is unreachable by the time the GC runs — a Task
		// still rooted on the stack never becomes garbage, so its finalizer never runs.
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void StartFaultedTask()
		{
			Task.Run(() =>
			{
				try
				{
					ThrowFromSampleFrames(ManagedScenario.UnobservedTask);
				}
				finally
				{
					// Signalled from a finally rather than after the throw, which is unreachable.
					faultedTaskThrew = true;
				}
			});
		}

		static void PostCaughtException(ICrashScenarioHost host)
		{
			try
			{
				ThrowFromSampleFrames(ManagedScenario.CaughtAndPosted);
			}
			catch (Exception ex)
			{
				var options = new BugSplatUnity.ReportPostOptions { Description = "a new description" };
				host.Run(host.BugSplat.Post(ex, options, result =>
				{
					Debug.Log($"BugSplat sample: post result — {result.Message}");
					if (result.Response != null)
					{
						Debug.Log($"BugSplat crash id {result.Response.crashId}: {result.Response.infoUrl}");
					}
				}));
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

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_IOS || UNITY_ANDROID

		// ---- Native: the same bugsplat-native mechanism on every player platform ----

		static ScenarioGroup BuildNative()
		{
			var group = new ScenarioGroup
			{
				Title = "NATIVE",
#if UNITY_IOS
				Subtitle = "Captured by bugsplat-native's in-process handler; the report uploads on the next launch. Terminates the player.",
#else
				Subtitle = "Captured out of process by BugSplatMonitor and dumped while the player is frozen. Terminates the player.",
#endif
			};

			group.Scenarios.Add(new CrashScenario
			{
				Name = "Capture report (no crash)",
				Expected =
					"bugsplat.CaptureReport(): the monitor dumps the live process and the report flows " +
					"like a crash, but the player keeps running. The cube keeps spinning.",
				Run = host =>
				{
					Describe(host, "CaptureReport");
					var ok = host.BugSplat.CaptureReport();
					Debug.Log($"BugSplat sample: CaptureReport returned {ok}");
				}
			});

#if UNITY_STANDALONE_WIN
			group.Scenarios.Add(new CrashScenario
			{
				Name = "Access violation — write",
				Expected = "Native report: EXCEPTION_ACCESS_VIOLATION writing address 0.",
				Run = host => Crash(host, "AccessViolationWrite", AccessViolationWrite)
			});
			group.Scenarios.Add(new CrashScenario
			{
				Name = "Access violation — read",
				Expected = "Native report: EXCEPTION_ACCESS_VIOLATION reading address 0.",
				Run = host => Crash(host, "AccessViolationRead", AccessViolationRead)
			});
			group.Scenarios.Add(new CrashScenario
			{
				Name = "Access violation — background thread",
				Expected = "Native report whose faulting thread is not the main thread.",
				Run = host =>
				{
					Describe(host, "AccessViolationBackgroundThread");
					RunOnBackgroundThread(AccessViolationWrite);
				}
			});
			group.Scenarios.Add(new CrashScenario
			{
				Name = "Custom SEH exception",
				Expected = "Native report with code 0xE0BADBAD — proves the handler is not AV-specific.",
				Run = host => Crash(host, "CustomSehException",
					() => RaiseException(0xE0BADBAD, 0, 0, IntPtr.Zero))
			});
			group.Scenarios.Add(new CrashScenario
			{
				Name = "Stack overflow",
#if ENABLE_MONO
				// Mono guards the stack and raises a managed StackOverflowException rather than
				// letting the fault reach the native handler, so what arrives - if anything -
				// is a managed report and the player may survive. Say so rather than promising
				// a native crash the backend will not produce.
				Expected =
					"Mono: the runtime guards the stack, so expect a managed report (or nothing) " +
					"rather than a native crash. Check Player.log. Build with IL2CPP for 0xC00000FD.",
#else
				Expected = "Native report: EXCEPTION_STACK_OVERFLOW (0xC00000FD).",
#endif
				Run = host => Crash(host, "StackOverflow", () => Sink = Overflow(0))
			});
#else
			group.Scenarios.Add(new CrashScenario
			{
				Name = "Null pointer write (SIGSEGV)",
#if ENABLE_MONO
				Expected =
					"A write to address 0 inside the marshaling layer. Mono turns faults in managed " +
					"code into NullReferenceException; this one happens in native code, so it reaches " +
					"bugsplat-native. Build with IL2CPP for the most faithful native crash.",
#else
				Expected = "Native report: SIGSEGV / EXC_BAD_ACCESS writing address 0.",
#endif
				Run = host => Crash(host, "NullPointerWrite", NullPointerWrite)
			});
			group.Scenarios.Add(new CrashScenario
			{
				Name = "Null pointer write — background thread",
				Expected = "Native report whose faulting thread is not the main thread.",
				Run = host =>
				{
					Describe(host, "NullPointerWriteBackgroundThread");
					RunOnBackgroundThread(NullPointerWrite);
				}
			});
#endif
			return group;
		}

		static ScenarioGroup BuildHang() => new ScenarioGroup
		{
			Title = "HANG",
			Subtitle =
				"Detected by bugsplat-native's watchdog when BugSplatManager's per-frame heartbeat stops " +
				"for longer than Hang Detection Timeout Ms (the sample's options asset uses 5 s).",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Main-thread hang",
					Expected =
						"Blocks the main thread for 15 s. After the timeout the monitor dumps the live " +
						"process and uploads a hang report (reportKind=hang). With Hang Policy = Report " +
						"the player resumes when the sleep ends; with Report And Terminate the dialog " +
						"offers Wait / Close.",
					Run = host => host.Run(HangMainThread(host))
				}
			}
		};

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

		static IEnumerator HangMainThread(ICrashScenarioHost host)
		{
			if (host.BugSplat.NativeSettings.HangDetectionTimeoutMs <= 0)
			{
				Debug.LogWarning(
					"BugSplat sample: Hang Detection Timeout Ms is 0 on the options asset, so nothing " +
					"will notice this hang. Set it (the sample uses 5000) and rebuild.");
			}

			Describe(host, "MainThreadHang");

			// Let this frame finish rendering before wedging the main thread.
			yield return null;
			yield return null;

			System.Threading.Thread.Sleep(15000);
			Debug.Log("BugSplat sample: the main thread is responsive again.");
		}

		// A valid buffer for the non-null side of a copy, so the only bad address is the one the
		// scenario is testing.
		static readonly IntPtr ScratchBuffer = Marshal.AllocHGlobal(64);

#if UNITY_STANDALONE_WIN

		// ---- Windows-specific: fail-fast, and the fault primitives ----

		static ScenarioGroup BuildWindowsFailFast() => new ScenarioGroup
		{
			Title = "FAIL-FAST",
			Subtitle =
				"Bypass every in-process handler; reported only when BugSplatWer.dll is " +
				"registered with Windows Error Reporting. Terminates the player.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Fail-fast (0xC0000602)",
					RequiresWer = true,
					Expected =
						"WER only. Armed: native report, STATUS_FAIL_FAST_EXCEPTION. Not armed: no " +
						"report, and a dump lands in %LOCALAPPDATA%\\CrashDumps.",
					Run = host => Crash(host, "FailFast",
						() => RaiseFailFastException(IntPtr.Zero, IntPtr.Zero, FailFastGenerateExceptionAddress))
				},
				new CrashScenario
				{
					Name = "Stack buffer overrun (0xC0000409)",
					RequiresWer = true,
					Expected =
						"WER only. The signature a /GS cookie failure or __fastfail produces in the field.",
					Run = host => Crash(host, "StackBufferOverrun", FailFastAsStackBufferOverrun)
				},
				new CrashScenario
				{
					Name = "Heap corruption (0xC0000374)",
					Expected =
						"A double free on the process heap. bugsplat-native catches STATUS_HEAP_CORRUPTION " +
						"in-process (a first-position vectored handler), so this reports with or without WER.",
					Run = host => Crash(host, "HeapCorruption", CorruptProcessHeap)
				}
			}
		};

		// The fault has to happen in code the runtime does not own, or it never becomes a crash.
		//
		// Dereferencing null from C# - Marshal.WriteInt32(IntPtr.Zero, 0), or an unsafe pointer
		// write - does not produce a native crash on the Mono backend. Mono's vectored exception
		// handler sees a fault whose instruction pointer is inside JIT'd managed code, claims it,
		// and rethrows it as a managed NullReferenceException (which Marshal then translates to
		// AccessViolationException). The result is a caught managed exception and a player that
		// keeps running - not a crash, and nothing for the native handler to capture.
		//
		// Routing the same null dereference through RtlMoveMemory puts the faulting instruction
		// inside ntdll instead. Mono has no JIT info for that address, so its handler declines,
		// the exception continues to the SEH chain, and bugsplat-native's unhandled exception
		// filter gets it - on both Mono and IL2CPP. This is the same reason the custom SEH scenario
		// works: RaiseException also faults outside managed code.
		//
		// NoInlining keeps the frames distinct through IL2CPP and MSVC optimization so the report
		// shows a game-code call stack above the ntdll frames.

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void AccessViolationWrite() => NativeCrashFrame0();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame0() => NativeCrashFrame1();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame1() => NativeCrashFrame2();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame2() => RtlMoveMemory(IntPtr.Zero, ScratchBuffer, (UIntPtr)4);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void AccessViolationRead() => RtlMoveMemory(ScratchBuffer, IntPtr.Zero, (UIntPtr)4);

		// '+ depth' makes the call non-tail-recursive, so it cannot be rewritten into a loop —
		// which would hang instead of overflowing.
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Overflow(int depth) => Overflow(depth + 1) + depth;

		static volatile int Sink;

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

		// kernel32 exports are WINAPI, which is stdcall on x86 — leave CallingConvention at the
		// default Winapi.

		const uint FailFastGenerateExceptionAddress = 0x1;
		const int HeapEnableTerminationOnCorruption = 1;

		[DllImport("kernel32.dll")]
		static extern void RaiseFailFastException(IntPtr exceptionRecord, IntPtr contextRecord, uint flags);

		[DllImport("kernel32.dll")]
		static extern void RaiseException(uint code, uint flags, uint argumentCount, IntPtr arguments);

		// Also exported by kernel32 as the CopyMemory/MoveMemory macros; faults inside ntdll's
		// memmove when either address is invalid.
		[DllImport("kernel32.dll")]
		static extern void RtlMoveMemory(IntPtr destination, IntPtr source, UIntPtr length);

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

#else

		// ---- POSIX fault primitive (macOS, Linux, iOS, Android) ----

		// The write happens inside the marshaling layer's native memcpy, not in JIT'd or IL2CPP
		// code, so neither runtime can turn it into a NullReferenceException: it is a real
		// SIGSEGV / EXC_BAD_ACCESS for bugsplat-native to capture. Same NoInlining reasoning as
		// the Windows frames: the report should show a game-code stack above the fault.
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NullPointerWrite() => NativeCrashFrame0();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame0() => NativeCrashFrame1();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame1() => NativeCrashFrame2();

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void NativeCrashFrame2() => Marshal.Copy(new byte[] { 1, 2, 3, 4 }, 0, IntPtr.Zero, 4);

#endif
#endif
	}
}
