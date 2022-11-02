using System;
using System.Collections;
using UnityEngine;

namespace BugSplatUnity.Runtime.Reporter
{
	public class IOSExceptionReporter : IExceptionReporter
	{
		public void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action callback = null)
		{
		}

		public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action callback = null)
		{
			yield break;
		}
	}
}