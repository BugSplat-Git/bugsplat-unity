using UnityEngine;

namespace BugSplatUnity.Runtime.Client
{
	public sealed class BugSplatManager : MonoBehaviour
	{
		public static BugSplatManager GetBugSplatManager => FindObjectOfType<BugSplatManager>();

		[SerializeField]
		[Tooltip("BugSplat configuration SerializedObject to instantiate BugSplat with.")]
		private BugSplatConfigurationOptions configurationOptions = null;

		[SerializeField]
		[Tooltip("Should the BugSplatManager be destroyed when a new scene is loaded?")]
		private bool dontDestroyManagerOnSceneLoad = true;

		[SerializeField]
		[Tooltip("Register BugSplat to capture LogType.Exceptions on initialization.")]
		private bool registerLogMessageRecieved;

		public BugSplat BugSplat;

		private void Awake()
		{
			ConfigureBugSplat();
			if (dontDestroyManagerOnSceneLoad)
			{
				DontDestroyOnLoad(this);
			}
		}

		/// <summary>
		/// Function to instantiate BugSplat object based on configurationOptions.
		/// </summary>
		private void ConfigureBugSplat()
		{
			if (configurationOptions == null)
			{
				Debug.LogError("No BugSplatConfigurationOptions serialized for BugSplatManager! BugSplat will not be created.", this);
				return;
			}

			BugSplat = BugSplatFactory.CreateBugSplatFromConfigurationOptions(configurationOptions, Application.productName, Application.version);

			if (registerLogMessageRecieved)
			{
				Application.logMessageReceived += BugSplat.LogMessageReceived;
			}
		}
	}
}
