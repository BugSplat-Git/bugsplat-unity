using System;
using BugSplatUnity;

public class BugSplatRef
{
    public BugSplat BugSplat { get; set; }

    public BugSplatRef(BugSplat bugsplat)
    {
        if (bugsplat == null)
        {
            throw new ArgumentException("BugSplat error: BugSplat instance is null! BugSplatRef will not be initialized.");
        }

        BugSplat = bugsplat;
    }
}
