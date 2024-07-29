using System;
using UnityEngine;
using UnityEngine.Diagnostics;
using BugSplat = BugSplatUnity.BugSplat;
using BugSplatUnity.Runtime.Manager;
using BugSplatUnity;
using BugSplatUnity.Runtime.Reporter;
using System.Diagnostics;
#if UNITY_STANDALONE_WIN || (UNITY_IOS && !UNITY_EDITOR)
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
			bugsplat = FindObjectOfType<BugSplatManager>().BugSplat;
			Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.Full);
#if UNITY_STANDALONE_WIN
            StartCoroutine(bugsplat.PostMostRecentCrash());
#endif
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

		public void Event_ForceCrash(ForcedCrashCategory category)
		{
			Utils.ForceCrash(category);
		}
		
		public void Event_NativeCrashIos()
		{
#if UNITY_IOS && !UNITY_EDITOR
			_crashNativeIos();
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

		private void GenerateSampleStackFramesAndThrow()
		{
			SampleStackFrame0();
		}

		private void OpenUrl(string url)
		{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
			Process.Start(url);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
			var escaped = url.Replace("?", "\\?").Replace("&", "\\&").Replace(" ", "%20");
			Process.Start("open", escaped);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
			Process.Start("xdg-open", url);
#elif UNITY_WEBGL
			Application.OpenURL(url);
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
#endif
	}
}


