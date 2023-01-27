using BugSplatUnity;
using BugSplatUnity.Runtime.Manager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BugSplatSettings : MonoBehaviour
{
    BugSplat bugsplat;

    // Start is called before the first frame update
    void Start()
    {
        bugsplat = FindObjectOfType<BugSplatManager>().BugSplat;
        bugsplat.Description = "Overridden description from BugSplatSettings.";
        bugsplat.Notes = GetSystemInfo();

        var lastPost = DateTime.Now;
        bugsplat.ShouldPostException = (ex) =>
        {
            var now = DateTime.Now;

            // Set to a short TimeSpan for demonstration purposes
            // In production BugSplat recommends 60 seconds between posts
            if (now - lastPost < TimeSpan.FromSeconds(3))
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


    private string GetSystemInfo()
    {
        var info = new Dictionary<string, string>();
        info.Add("OS", SystemInfo.operatingSystem);
        info.Add("CPU", SystemInfo.processorType);
        info.Add("MEMORY", $"{SystemInfo.systemMemorySize} MB");
        info.Add("GPU", SystemInfo.graphicsDeviceName);
        info.Add("GPU MEMORY", $"{SystemInfo.graphicsMemorySize} MB");

        var sections = info.Select(section => $"{section.Key}: {section.Value}");
        return string.Join(Environment.NewLine, sections);
    }
}
