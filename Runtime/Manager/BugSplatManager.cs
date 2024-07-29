using System;
using BugSplatUnity.Runtime.Client;
using UnityEngine;

namespace BugSplatUnity.Runtime.Manager
{
	public sealed class BugSplatManager : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("BugSplat configuration SerializedObject to instantiate BugSplat with.")]
		private BugSplatOptions bugSplatOptions;

		[SerializeField]
		[Tooltip("Should the BugSplatManager be destroyed when a new scene is loaded?")]
		private bool dontDestroyManagerOnSceneLoad = true;

		[SerializeField]
		[Tooltip("Register BugSplat to capture LogType.Exceptions on initialization.")]
		private bool registerLogMessageReceived = true;

		private BugSplatRef bugsplatRef;
		public BugSplat BugSplat => bugsplatRef.BugSplat;

		private void Awake()
		{
			if (bugSplatOptions == null)
			{
				throw new ArgumentException("BugSplat error: BugSplatOptions is null! BugSplat will not be initialized.");
			}

			var bugsplat = BugSplat.CreateFromOptions(bugSplatOptions);
			bugsplatRef = new BugSplatRef(bugsplat);

			if (registerLogMessageReceived)
			{
				Application.logMessageReceived += (logMessage, stackTrace, type) => StartCoroutine(bugsplat.LogMessageReceived(logMessage, stackTrace, type));
			}
			
			if (dontDestroyManagerOnSceneLoad)
			{
                DontDestroyOnLoad(this);
			}
		}
	}
}
