using System;
using UnityEngine;
using BugSplat = BugSplatUnity.BugSplat;
using BugSplatUnity.Runtime.Manager;
using BugSplatUnity;
using BugSplatUnity.Runtime.Reporter;
using System.Diagnostics;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Crasher
{
	public class ErrorGenerator : MonoBehaviour
	{
		BugSplat bugsplat;
		private string infoUrl = "";
		
		void Start()
		{
			bugsplat = FindFirstObjectByType<BugSplatManager>().BugSplat;
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
			UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.AccessViolation);
#else
			UnityEngine.Debug.LogError("BugSplat: Native crash not yet implemented on this platform");
#endif
		}

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
			var popup = FindFirstObjectByType<FeedbackPopup>(FindObjectsInactive.Include);
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


