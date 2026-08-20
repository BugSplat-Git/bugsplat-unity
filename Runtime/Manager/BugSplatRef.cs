using System;

namespace BugSplatUnity.Runtime.Manager
{
    internal class BugSplatRef
    {
        public BugSplat BugSplat { get; }

        public BugSplatRef(BugSplat bugsplat)
        {
            if (bugsplat == null)
            {
                throw new ArgumentException("BugSplat error: BugSplat instance is null! BugSplatRef will not be initialized.");
            }

            BugSplat = bugsplat;
        }
    }
}
