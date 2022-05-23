using System;
using UnityEngine;
using UnityEngine.Diagnostics;
using BugSplat = BugSplatUnity.BugSplat;
using BugSplatUnity.Runtime.Manager;
using BugSplatUnity;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace Crasher
{
	public class Crasher : MonoBehaviour
	{
		BugSplat bugsplat;
		
		void Start()
		{
			bugsplat = FindObjectOfType<BugSplatManager>().BugSplat;
			Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.Full);
#if UNITY_STANDALONE_WIN
            StartCoroutine(bugsplat.PostMostRecentCrash());
#endif
        }

		public void Event_ForceCrash(ForcedCrashCategory category)
		{
			Utils.ForceCrash(category);
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

		static void ExceptionCallback()
		{
			Debug.Log($"Exception post callback!");
		}
	}
}


