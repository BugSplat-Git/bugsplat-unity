using BugSplatUnity.Runtime.Client;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("BugSplat.Unity.RuntimeTests")]
namespace BugSplatUnity.Runtime.Manager
{
	internal class BugSplatManagerImpl
	{
		public BugSplat BugSplat { get; private set; }

		private BugSplatOptions bugSplatOptions;
		private bool registerLogMessageRecieved;
		private bool dontDestroyManagerOnSceneLoad;
		private GameObject managerGameObject;

		public BugSplatManagerImpl(BugSplatOptions bugSplatOptions, bool registerLogMessageRecieved, bool dontDestroyManagerOnSceneLoad, GameObject managerGameObject)
		{
			this.bugSplatOptions = bugSplatOptions;
			this.registerLogMessageRecieved = registerLogMessageRecieved;
			this.dontDestroyManagerOnSceneLoad = dontDestroyManagerOnSceneLoad;
			this.managerGameObject = managerGameObject;
		}

		public void Instantiate()
		{
			if (bugSplatOptions == null)
			{
				throw new ArgumentException("BugSplat error: BugSplatOptions is null! BugSplat will not be initialized.");
			}

			BugSplat = BugSplat.CreateFromOptions(bugSplatOptions);

			if (registerLogMessageRecieved)
			{
				Application.logMessageReceived += BugSplat.LogMessageReceived;
			}
			
			if (dontDestroyManagerOnSceneLoad)
			{
				Object.DontDestroyOnLoad(managerGameObject);
			}
		}
	}
}
