using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformDependentButtonRenderer : MonoBehaviour
{
    [SerializeField]
    List<GameObject> crashButtons;

    void Start()
    {
        var shouldButtonsBeActive = true;

#if !UNITY_STANDALONE_WIN
        shouldButtonsBeActive = false;
#endif
        foreach (var button in crashButtons)
        {
            button.SetActive(shouldButtonsBeActive);
        }
    }

}
