using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Events;

namespace Crasher
{
	public class ForcedCrashEvent : MonoBehaviour
	{
		public UnityEvent<ForcedCrashCategory> OnForceCrash;

		[SerializeField]
		ForcedCrashCategory category;	
		
		public void Event_OnForceCrash()
		{
			OnForceCrash.Invoke(category);
		}
	}
}
