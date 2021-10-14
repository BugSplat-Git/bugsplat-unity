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
		private bool registerLogMessageRecieved;

		private BugSplatManagerImpl _impl;
		public BugSplat BugSplat => _impl.BugSplat;

		private void Awake()
		{
			_impl = new BugSplatManagerImpl(bugSplatOptions, registerLogMessageRecieved, dontDestroyManagerOnSceneLoad, gameObject);
			_impl.Instantiate();
		}
	}
}
