using BugSplatUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Crasher
{
    public class BugSplatSettings : MonoBehaviour
    {
        BugSplat bugsplat;

        // Start is called before the first frame update
        void Start()
        {
            if (!BugSplat.IsInitialized)
            {
                Debug.LogError("[BugSplat] BugSplat is not initialized, so no exceptions will be reported. Select or create a BugSplat Options asset in Edit > Project Settings > BugSplat.");
                return;
            }

            bugsplat = BugSplat.Instance;

            bugsplat.Attributes.Add("OS", SystemInfo.operatingSystem);
            bugsplat.Attributes.Add("CPU", SystemInfo.processorType);
            bugsplat.Attributes.Add("MEMORY", $"{SystemInfo.systemMemorySize} MB");
            bugsplat.Attributes.Add("GPU", SystemInfo.graphicsDeviceName);
            bugsplat.Attributes.Add("GPU MEMORY", $"{SystemInfo.graphicsMemorySize} MB");
            bugsplat.Description = "Overridden description from BugSplatSettings.";
            bugsplat.Notes = "Overridden notes field from BugSplatSettings.";
            bugsplat.User = "Fred";
            bugsplat.Email = "fred@bugsplat.com";

            var lastPost = new DateTime(0);
            bugsplat.ShouldPostException = (ex) =>
            {
                var now = DateTime.Now;

                // Set to a long TimeSpan for demonstration purposes
                // In production BugSplat recommends 3 seconds between posts
                if (now - lastPost < TimeSpan.FromSeconds(7))
                {
                    Debug.LogWarning("ShouldPostException returns false in BugSplatSettings. Skipping BugSplat report...");
                    return false;
                }

                Debug.Log("ShouldPostException returns true in BugSplatSettings. Posting BugSplat report...");
                lastPost = now;
                return true;
            };
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
