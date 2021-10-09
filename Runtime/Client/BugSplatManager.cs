using UnityEngine;

namespace BugSplatUnity.Runtime.Client
{
	public class BugSplatManager : MonoBehaviour
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

		protected virtual void Awake()
		{
			if (configurationOptions != null)
			{
				ConfigureBugSplat(configurationOptions);
			}

			if (dontDestroyManagerOnSceneLoad)
			{
				DontDestroyOnLoad(this);
			}
		}

		/// <summary>
		/// Override-able function to instantiate BugSplat based on ConfigurationOptions.
		/// </summary>
		public virtual void ConfigureBugSplat(BugSplatConfigurationOptions configurationOptions)
		{
			finalizeConfiguration();

			this.configurationOptions = configurationOptions;

			BugSplat = new BugSplat(this.configurationOptions);

			if (registerLogMessageRecieved)
			{
				Application.logMessageReceived += BugSplat.LogMessageReceived;
			}
		}

		/// <summary>
		/// Optional override-able function to modify configuration options before BugSplat is constructed.
		/// </summary>
		protected virtual void finalizeConfiguration() {}
	}
}
