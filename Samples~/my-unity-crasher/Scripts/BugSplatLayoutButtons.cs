using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class BugSplatLayoutButtons : MonoBehaviour
{
    private readonly Color bugSplatRed = new Color32(244, 102, 137, 255);
    private readonly Color bugSplatGreen = new Color32(74, 235, 195, 255);
    private readonly Color bugSplatBlue = new Color32(58, 163, 255, 255);
    
    void Start()
    {
        var images = transform.GetComponentsInChildren<Image>();
        var count = 0;
        foreach (var image in images)
        {            
            if (image.gameObject.activeInHierarchy)
            {
                switch (count % 3)
                {
                    case 0:
                        image.color = bugSplatRed;
                        break;
                    case 1:
                        image.color = bugSplatBlue;
                        break;
                    case 2:
                        image.color = bugSplatGreen;
                        break;
                }
                count++;
            }
        }

    }
}
