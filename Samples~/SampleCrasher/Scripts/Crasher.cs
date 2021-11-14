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

        private void generateSampleStackFramesAndThrow()
		{
			sampleStackFrame0();
		}

		private void sampleStackFrame0()
		{
			sampleStackFrame1();
		}

		private void sampleStackFrame1()
		{
			sampleStackFrame2();
		}

		private void sampleStackFrame2()
		{
			throwException();
		}

		private void throwException()
		{
			throw new Exception("BugSplat rocks!");
		}

		public void Event_ForceCrash(ForcedCrashCategory category)
		{
			Utils.ForceCrash(category);
		}

		public void Event_CatchExceptionThenPostNewBugSplat()
		{
			try
			{
				generateSampleStackFramesAndThrow();
			}
			catch (Exception ex)
			{
				var options = new ReportPostOptions()
				{
					Description = "a new description"
				};

				static void callback()
				{
					Debug.Log($"Exception post callback!");
				};

				StartCoroutine(bugsplat.Post(ex, options, callback));
			}
		}

		public void Event_ThrowException()
		{
			generateSampleStackFramesAndThrow();
		}
	}
}


