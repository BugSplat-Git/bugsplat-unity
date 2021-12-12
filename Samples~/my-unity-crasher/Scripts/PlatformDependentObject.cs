using System.Collections.Generic;
using UnityEngine;

public class PlatformDependentObject : MonoBehaviour
{
    [SerializeField] List<RuntimePlatform> validPlatforms;

    public void Awake()
    {
        var shouldBeActive = validPlatforms.Contains(Application.platform);
        gameObject.SetActive(shouldBeActive);
    }
}
