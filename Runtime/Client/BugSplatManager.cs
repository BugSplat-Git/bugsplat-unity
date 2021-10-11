using System;
using UnityEngine;

namespace BugSplatUnity.Runtime.Client
{
	public sealed class BugSplatManager : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("BugSplat configuration SerializedObject to instantiate BugSplat with.")]
		private BugSplatOptions bugSplatOptions = null;

		[SerializeField]
		[Tooltip("Should the BugSplatManager be destroyed when a new scene is loaded?")]
		private bool dontDestroyManagerOnSceneLoad = true;

		[SerializeField]
		[Tooltip("Register BugSplat to capture LogType.Exceptions on initialization.")]
		private bool registerLogMessageRecieved;

		public BugSplat BugSplat { get; private set; }

		private void Awake()
		{
			ConfigureBugSplat();
			if (dontDestroyManagerOnSceneLoad)
			{
				DontDestroyOnLoad(this);
			}
		}

		/// <summary>
		/// Function to instantiate BugSplat object based on BugSplatOptions.
		/// </summary>
		private void ConfigureBugSplat()
		{
			if (bugSplatOptions == null)
			{
				throw new InvalidOperationException("BugSplat error: no instance of BugSplatOptions is serialized in BugSplatManager! BugSplat will not be initialied.");
			}

			BugSplat = BugSplat.CreateFromOptions(bugSplatOptions, Application.productName, Application.version);

			if (registerLogMessageRecieved)
			{
				Application.logMessageReceived += BugSplat.LogMessageReceived;
			}
		}
	}
}
