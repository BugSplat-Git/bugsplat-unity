using System;
using UnityEngine;
using BugSplat = BugSplatUnity.BugSplat;
using BugSplatUnity.Runtime.Manager;
using BugSplatUnity;
using BugSplatUnity.Runtime.Reporter;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Crasher
{
	public class ErrorGenerator : MonoBehaviour
	{
		BugSplat bugsplat;
		private string infoUrl = "";
		
		void Start()
		{
			bugsplat = FindAnyObjectByType<BugSplatManager>().BugSplat;
			Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.Full);
		}

		void Update()
		{
			if (string.IsNullOrEmpty(infoUrl))
			{
				return;
			}

			if (!infoUrl.StartsWith("https://"))
			{
				return;
			}

			OpenUrl(infoUrl);
			infoUrl = "";
		}

		public void Event_CrashNative()
		{
#if UNITY_IOS && !UNITY_EDITOR
			_crashNativeIos();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
			_crashNativeMac();
#elif UNITY_ANDROID && !UNITY_EDITOR
			CrashNativeAndroid();
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
			CrashWindowsNative();
#elif UNITY_EDITOR
			UnityEngine.Debug.LogWarning("BugSplat: native crash reporting runs in built players only, not the editor. Make a build and click this button there to test native crash reporting.");
#else
			UnityEngine.Debug.LogError("BugSplat: Native crash not yet implemented on this platform");
#endif
		}

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
		// Triggers a native access violation from a chain of C# frames. BugSplat's
		// native handler captures the crash; on an IL2CPP build these frames
		// symbolicate back to their C# method names and line numbers via
		// GameAssembly.pdb + LineNumberMappings.json, so the report shows a
		// game-code call stack instead of an engine-internal one. NoInlining keeps
		// the frames distinct through IL2CPP/MSVC optimization, so the full chain
		// shows up rather than a single collapsed frame.
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void CrashWindowsNative() => NativeCrashFrame0();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void NativeCrashFrame0() => NativeCrashFrame1();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void NativeCrashFrame1() => NativeCrashFrame2();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void NativeCrashFrame2() => DereferenceNullPointer();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void DereferenceNullPointer()
		{
			// Raw write to address 0 -> hardware access violation (SEH), not a
			// managed NullReferenceException. Works on both Mono and IL2CPP.
			Marshal.WriteInt32(IntPtr.Zero, 0);
		}
#endif

		public void Event_HangNative()
		{
#if UNITY_IOS && !UNITY_EDITOR
			// Wedges the main thread; the OS watchdog terminates the app and BugSplat
			// uploads an "App Hang (Fatal)" report on the next launch.
			_hangNativeIos();
#elif UNITY_ANDROID && !UNITY_EDITOR
			HangNativeAndroid();
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
			// Blocks the main thread so the window stops pumping messages. BugSplat
			// reports a hang if WindowsHangDetectionTimeoutMs is set to a non-zero value.
			System.Threading.Thread.Sleep(30000);
#else
			UnityEngine.Debug.LogError("BugSplat: Native hang not yet implemented on this platform");
#endif
		}

		public void Event_CatchExceptionThenPostNewBugSplat()
		{
			try
			{
				GenerateSampleStackFramesAndThrow();
			}
			catch (Exception ex)
			{
				var options = new ReportPostOptions()
				{
					Description = "a new description"
				};

				StartCoroutine(bugsplat.Post(ex, options, ExceptionCallback));
			}
		}

		public void Event_ThrowException()
		{
			GenerateSampleStackFramesAndThrow();
		}

		public void Event_LeaveFeedback()
		{
			var popup = FindAnyObjectByType<FeedbackPopup>(FindObjectsInactive.Include);
			if (popup != null)
			{
				popup.Show();
			}
			else
			{
				UnityEngine.Debug.LogError("[BugSplat] FeedbackPopup not found in scene");
			}
		}

		private void GenerateSampleStackFramesAndThrow()
		{
			SampleStackFrame0();
		}

		private void OpenUrl(string url)
		{
			var escaped = url.Replace("?", "\\?").Replace("&", "\\&").Replace(" ", "%20").Replace("!", "\\!");

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
			Process.Start(url);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
			Process.Start("open", escaped);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
			Process.Start("xdg-open", escaped);
#else
			UnityEngine.Debug.Log($"OpenUrl unsupported platform: {Application.platform}");
#endif
		}

		private void SampleStackFrame0()
		{
			SampleStackFrame1();
		}

		private void SampleStackFrame1()
		{
			SampleStackFrame2();
		}

		private void SampleStackFrame2()
		{
			ThrowException();
		}

		private void ThrowException()
		{
			throw new Exception("BugSplat rocks!");
		}

        void ExceptionCallback(ExceptionReporterPostResult result)
		{
			UnityEngine.Debug.Log($"Exception post callback result: {result.Message}");

			if (result.Response == null) {
				return;
			}

			UnityEngine.Debug.Log($"BugSplat Status: {result.Response.status}");
			UnityEngine.Debug.Log($"BugSplat Crash ID: {result.Response.crashId}");
			UnityEngine.Debug.Log($"BugSplat Support URL: {result.Response.infoUrl}");

			infoUrl = result.Response.infoUrl;
		}
		
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void _crashNativeIos();

        [DllImport("__Internal")]
        static extern void _hangNativeIos();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void _crashNativeMac();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
		private void CrashNativeAndroid()
		{
			using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
			javaClass.CallStatic("crash");
		}

		private void HangNativeAndroid()
		{
			// BugSplatBridge.hang() blocks whatever thread calls it. Unity runs C#
			// on its own player thread, not the Android UI thread, so the call must
			// be dispatched to the UI thread for the OS to register an ANR.
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


