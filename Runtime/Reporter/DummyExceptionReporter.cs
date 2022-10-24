using System;
using System.Collections;
using BugSplatUnity;
using BugSplatUnity.Runtime.Reporter;
using UnityEngine;

public class DummyExceptionReporter : IExceptionReporter
{
	public void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action callback = null)
	{
	}

	public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action callback = null)
	{
		yield break;
	}
}
