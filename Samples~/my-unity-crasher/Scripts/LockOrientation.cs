using UnityEngine;

namespace Crasher
{
    public class LockOrientation : MonoBehaviour
    {
        // The menu is laid out for landscape. AutoRotation with portrait disabled left a phone held
        // upright in portrait, so request landscape outright.
        void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
