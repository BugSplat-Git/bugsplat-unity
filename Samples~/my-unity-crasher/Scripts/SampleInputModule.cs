using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Crasher
{
    // Adds the correct UI input module to the EventSystem at runtime based on
    // the project's active input backend, so the sample's buttons work whether
    // the project uses the new Input System, the legacy Input Manager, or both.
    // The module is added at runtime (not serialized in the scene) so the scene
    // never carries a package-specific component that shows up as a missing
    // script in projects without that package.
    [RequireComponent(typeof(EventSystem))]
    public class SampleInputModule : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<BaseInputModule>() != null)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var module = gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            Debug.Log("BugSplat sample: added InputSystemUIInputModule (new Input System).");
#elif ENABLE_LEGACY_INPUT_MANAGER
            gameObject.AddComponent<StandaloneInputModule>();
            Debug.Log("BugSplat sample: added StandaloneInputModule (legacy Input Manager).");
#else
            Debug.LogWarning("BugSplat sample: no supported input backend is enabled; UI buttons will not receive input.");
#endif
        }
    }
}
