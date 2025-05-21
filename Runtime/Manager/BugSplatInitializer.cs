using BugSplatUnity.Runtime.Client;
using UnityEngine;

namespace BugSplatUnity.Runtime.Manager
{
    /// <summary>
    /// Loads BugSplat configuration and creates the global instance at startup.
    /// </summary>
    internal static class BugSplatInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (BugSplat.Instance != null)
            {
                return;
            }

            var options = Resources.Load<BugSplatOptions>("BugSplatOptions");
            if (options != null)
            {
                BugSplat.Instance = BugSplat.CreateFromOptions(options);
            }
        }
    }
}
