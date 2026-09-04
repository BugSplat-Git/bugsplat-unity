using System;
using BugSplatUnity.Runtime.Client;
using UnityEngine;

namespace BugSplatUnity.Runtime.Manager
{
	/// <summary>
	/// Kept for scenes authored against 4.x. BugSplat now initializes itself from the options asset
	/// selected in Edit > Project Settings > BugSplat, before the first scene loads, and exposes the
	/// client as <see cref="BugSplat.Instance"/> - nothing needs to be placed in a scene. This
	/// component adopts that instance when it exists and otherwise initializes from its own asset
	/// exactly as it did in 4.x, so an upgraded project keeps working until the component is removed.
	/// </summary>
	[Obsolete("BugSplat initializes itself from Edit > Project Settings > BugSplat and exposes the client as BugSplat.Instance. Remove this component from the scene.")]
	[AddComponentMenu("")]
	public sealed class BugSplatManager : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("BugSplat configuration SerializedObject to instantiate BugSplat with.")]
		internal BugSplatOptions bugSplatOptions;

		[SerializeField]
		[Tooltip("Should the BugSplatManager be destroyed when a new scene is loaded?")]
		internal bool dontDestroyManagerOnSceneLoad = true;

		[SerializeField]
		[Tooltip("Register BugSplat to capture LogType.Exceptions on initialization.")]
		internal bool registerLogMessageReceived = true;

		[SerializeField]
		[Tooltip("Also capture unhandled exceptions thrown on background threads. Requires Register Log Message Received.")]
		internal bool captureExceptionsOnBackgroundThreads = true;

		[SerializeField]
		[Tooltip("Also capture exceptions from Tasks that faulted and were never awaited. Requires Register Log Message Received.")]
		internal bool captureUnobservedTaskExceptions = true;

		// True when this component's Awake created the instance. Its OnDestroy then tears the
		// instance down, as a 4.x manager did when its scene unloaded. An adopted instance was never
		// this component's to end.
		private bool ownsInstance;

		public BugSplat BugSplat => BugSplat.Instance;

		private void Awake()
		{
			if (BugSplat.IsInitialized)
			{
				// Also the path two managers in one scene take: the second adopts the first's
				// instance instead of installing a second set of log hooks.
				Debug.LogWarning(
					$"BugSplat: the BugSplatManager on \"{gameObject.name}\" is no longer needed - BugSplat was " +
					"already initialized before it ran. Remove the component; configuration lives in " +
					"Edit > Project Settings > BugSplat.");
			}
			else
			{
				if (bugSplatOptions == null)
				{
					throw new ArgumentException("BugSplat error: BugSplatOptions is null! BugSplat will not be initialized.");
				}

				BugSplat.Initialize(
					bugSplatOptions,
					new BugSplatRuntime.Settings(
						registerLogMessageReceived,
						captureExceptionsOnBackgroundThreads,
						captureUnobservedTaskExceptions));
				ownsInstance = true;

				Debug.Log(
					$"BugSplat: initialized from the BugSplatManager on \"{gameObject.name}\". That component is " +
					"obsolete: select its options asset in Edit > Project Settings > BugSplat and remove it, and " +
					"BugSplat will initialize itself before the first scene loads.");
			}

			if (dontDestroyManagerOnSceneLoad)
			{
				DontDestroyOnLoad(this);
			}
		}

		private void OnDestroy()
		{
			if (ownsInstance)
			{
				BugSplat.Shutdown();
			}
		}
	}
}
