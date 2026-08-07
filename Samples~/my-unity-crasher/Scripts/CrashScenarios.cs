using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BugSplat = BugSplatUnity.BugSplat;

#if UNITY_STANDALONE_WIN
using System.Runtime.CompilerServices;
#endif
#if UNITY_STANDALONE_WIN || ((UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR)
using System.Runtime.InteropServices;
#endif

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

		/// <summary>Known not to produce a report today. Included to document the gap.</summary>
		public bool KnownGap;

		/// <summary>
		/// Only reported when the Windows Error Reporting handler is registered. The menu disables
		/// these when it isn't, because they otherwise terminate the player and report nothing.
		/// </summary>
		public bool RequiresWer;

		/// <summary>
		/// Safe to run inside the editor. False for anything native: the native reporters are
		/// excluded from the editor, so there would be no report anyway, and the crash would take
		/// the editor down with any unsaved work.
		/// </summary>
		public bool RunsInEditor;

		public Action<ICrashScenarioHost> Run;
	}

	/// <summary>
	/// Services a scenario needs from whatever MonoBehaviour is hosting the menu: coroutines, a
	/// main-thread dispatcher for callbacks that arrive on other threads, the BugSplat client,
	/// and the feedback dialog.
	/// </summary>
	public interface ICrashScenarioHost
	{
		BugSplat BugSplat { get; }
		Coroutine Run(IEnumerator routine);
		void OnMainThread(Action action);
		void ShowFeedback();
	}

	/// <summary>
	/// The scenario table, built per platform: groups compile for the active build target, so a
	/// Windows player offers Windows' native, fail-fast, and hang scenarios while an Android
	/// player offers Android's. In the editor the native rows for the current build target are
	/// listed but disabled.
	/// </summary>
	public static class CrashScenarios
	{
		public static IReadOnlyList<ScenarioGroup> Groups => groups;

		static readonly List<ScenarioGroup> groups = Build();

		static List<ScenarioGroup> Build()
		{
			var result = new List<ScenarioGroup> { BuildManaged() };

#if UNITY_STANDALONE_WIN
			result.Add(BuildWindowsNative());
			result.Add(BuildWindowsFailFast());
			result.Add(BuildWindowsHang());
#elif UNITY_STANDALONE_OSX
			result.Add(BuildMacNative());
#elif UNITY_IOS
			result.Add(BuildIosNative());
			result.Add(BuildIosHang());
#elif UNITY_ANDROID
			result.Add(BuildAndroidNative());
			result.Add(BuildAndroidHang());
#endif

			result.Add(BuildFeedback());
			return result;
		}

		// ---- Managed. Identical on every platform; the player survives all of these. ----

		static ScenarioGroup BuildManaged() => new ScenarioGroup
		{
			Title = "MANAGED",
			Subtitle = "C# exceptions, captured by BugSplat's .NET handler. The player keeps running.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Unhandled managed exception",
					Expected = "Reported via the log callback. The player keeps running.",
					RunsInEditor = true,
					Run = _ => ThrowFromSampleFrames()
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
						"KNOWN GAP: appears in Player.log but produces no report. Unity only raises " +
						"logMessageReceived for main-thread logs.",
					KnownGap = true,
					RunsInEditor = true,
					Run = _ => RunOnBackgroundThread(
						() => throw new Exception("BugSplat sample: exception on a background thread"))
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

		static void ThrowFromSampleFrames() => SampleStackFrame0();
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

#if UNITY_STANDALONE_WIN

		// ---- Windows ----

		static ScenarioGroup BuildWindowsNative() => new ScenarioGroup
		{
			Title = "NATIVE",
			Subtitle =
				"Captured in-process by BugSplat's crash handler and dumped by BugSplatMonitor. " +
				"Terminates the player.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Access violation — write",
					Expected = "Native report: EXCEPTION_ACCESS_VIOLATION writing address 0.",
					Run = host => Crash(host, "AccessViolationWrite", AccessViolationWrite)
				},
				new CrashScenario
				{
					Name = "Access violation — read",
					Expected = "Native report: EXCEPTION_ACCESS_VIOLATION reading address 0.",
					Run = host => Crash(host, "AccessViolationRead", AccessViolationRead)
				},
				new CrashScenario
				{
					Name = "Access violation — background thread",
					Expected = "Native report whose faulting thread is not the main thread.",
					Run = host =>
					{
						Describe(host, "AccessViolationBackgroundThread");
						RunOnBackgroundThread(AccessViolationWrite);
					}
				},
				new CrashScenario
				{
					Name = "Custom SEH exception",
					Expected = "Native report with code 0xE0BADBAD — proves the filter is not AV-specific.",
					Run = host => Crash(host, "CustomSehException",
						() => RaiseException(0xE0BADBAD, 0, 0, IntPtr.Zero))
				},
				new CrashScenario
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
				}
			}
		};

		static ScenarioGroup BuildWindowsFailFast() => new ScenarioGroup
		{
			Title = "FAIL-FAST",
			Subtitle =
				"Bypass every in-process handler; reported only when the WER handler is " +
				"registered. Terminates the player.",
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
					RequiresWer = true,
					Expected =
						"WER only. A double free on the process heap. On Win8+ the heap reports " +
						"corruption via a fail-fast, so this bypasses the filter too.",
					Run = host => Crash(host, "HeapCorruption", CorruptProcessHeap)
				}
			}
		};

		static ScenarioGroup BuildWindowsHang() => new ScenarioGroup
		{
			Title = "HANG",
			Subtitle = "Detected out of process by BugSplatMonitor's watchdog.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Main-thread hang",
					Expected =
						"Arms hang detection at 5s, then blocks the main thread for 30s. BugSplatMonitor " +
						"notices the window stopped pumping, uploads a hang report, and terminates.",
					Run = host => host.Run(HangAfterArmingDetection(host))
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
		// the exception continues to the SEH chain, and BugSplat's unhandled exception filter
		// gets it - on both Mono and IL2CPP. This is the same reason the custom SEH scenario
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

		// A valid buffer for the non-null side of the copy, so the only bad address is the one
		// the scenario is testing.
		static readonly IntPtr ScratchBuffer = Marshal.AllocHGlobal(64);

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

#elif UNITY_STANDALONE_OSX

		// ---- macOS ----

		static ScenarioGroup BuildMacNative() => new ScenarioGroup
		{
			Title = "NATIVE",
			Subtitle =
				"Captured by BugSplat's macOS crash reporter. Terminates the app; the report " +
				"uploads on the next launch.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Native crash",
					Expected = "A crash raised in the BugSplat-macOS bridge, reported with a native call stack.",
					Run = _ => CrashNativeMac()
				}
			}
		};

		static void CrashNativeMac()
		{
#if !UNITY_EDITOR
			_crashNativeMac();
#endif
		}

#if !UNITY_EDITOR
		[DllImport("__Internal")]
		static extern void _crashNativeMac();
#endif

#elif UNITY_IOS

		// ---- iOS ----

		static ScenarioGroup BuildIosNative() => new ScenarioGroup
		{
			Title = "NATIVE",
			Subtitle =
				"Captured by BugSplat's iOS crash reporter. Terminates the app; the report " +
				"uploads on the next launch.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Native crash",
					Expected = "A crash raised in the BugSplat iOS wrapper, reported with a native call stack.",
					Run = _ => CrashNativeIos()
				}
			}
		};

		static ScenarioGroup BuildIosHang() => new ScenarioGroup
		{
			Title = "HANG",
			Subtitle = "Detected by the OS watchdog; reported on the next launch.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Main-thread hang",
					Expected =
						"Wedges the main thread until the OS watchdog terminates the app. BugSplat " +
						"uploads an App Hang (Fatal) report on the next launch.",
					Run = _ => HangNativeIos()
				}
			}
		};

		static void CrashNativeIos()
		{
#if !UNITY_EDITOR
			_crashNativeIos();
#endif
		}

		static void HangNativeIos()
		{
#if !UNITY_EDITOR
			_hangNativeIos();
#endif
		}

#if !UNITY_EDITOR
		[DllImport("__Internal")]
		static extern void _crashNativeIos();

		[DllImport("__Internal")]
		static extern void _hangNativeIos();
#endif

#elif UNITY_ANDROID

		// ---- Android ----

		static ScenarioGroup BuildAndroidNative() => new ScenarioGroup
		{
			Title = "NATIVE",
			Subtitle = "Captured by BugSplat's Android crash reporter. Terminates the app.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "Native crash",
					Expected = "A crash raised in the BugSplat Android bridge, reported with a native call stack.",
					Run = _ => CrashNativeAndroid()
				}
			}
		};

		static ScenarioGroup BuildAndroidHang() => new ScenarioGroup
		{
			Title = "HANG",
			Subtitle = "An Application Not Responding (ANR) raised by the OS.",
			Scenarios =
			{
				new CrashScenario
				{
					Name = "UI-thread hang",
					Expected =
						"Blocks the Android UI thread until the OS raises an ANR, which BugSplat reports.",
					Run = _ => HangNativeAndroid()
				}
			}
		};

		static void CrashNativeAndroid()
		{
			using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
			javaClass.CallStatic("crash");
		}

		static void HangNativeAndroid()
		{
			// BugSplatBridge.hang() blocks whatever thread calls it. Unity runs C# on its own
			// player thread, not the Android UI thread, so the call must be dispatched to the UI
			// thread for the OS to register an ANR.
			using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
			activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
			{
				using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
				javaClass.CallStatic("hang");
			}));
		}
#endif
	}
}
