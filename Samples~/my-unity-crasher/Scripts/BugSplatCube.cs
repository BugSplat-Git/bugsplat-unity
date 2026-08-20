using UnityEngine;

namespace Crasher
{
    public class BugSplatCube : MonoBehaviour
    {
        void Update()
        {
            // Rotate slowly, but menacingly
            transform.Rotate(new Vector3(0, 0, -0.2f));
        }
    }
}
