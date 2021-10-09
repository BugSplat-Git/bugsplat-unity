using UnityEngine;

namespace BugSplatUnity.Runtime.Client
{
	public class BugSplatManager : MonoBehaviour
	{
		public BugSplatConfigurationOptions ConfigurationOptions = null;

		public BugSplat BugSplat;

		public static BugSplatManager GetBugSplatManager => FindObjectOfType<BugSplatManager>();

		protected virtual void Awake()
		{
			finalizeConfiguration();
			constructBugSplat();
		}

		/// <summary>
		/// Override-able function to instantiate BugSplat based on ConfigurationOptions
		/// </summary>
		protected virtual void constructBugSplat()
		{
			BugSplat = new BugSplat(ConfigurationOptions);

			if (ConfigurationOptions.RegisterLogMessageRecieved)
			{
				Application.logMessageReceived += BugSplat.LogMessageReceived;
			}

			if (!ConfigurationOptions.DestroyManagerOnSceneLoad)
			{
				DontDestroyOnLoad(this);
			}
		}

		/// <summary>
		/// Optional override-able function to modify configuration options before BugSplat is constructed.
		/// </summary>
		protected virtual void finalizeConfiguration() {}

		// TODO Do we want to have helper-setters here? The user can already access the BugSplat object, so it seems reduntant but could be convenient as a spring-board	
	}
}
