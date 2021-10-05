using System;

namespace BugSplatUnity.Runtime.Util
{
    public static class ShouldPostExceptionImpl
    {
        private static DateTime lastPost;

        public static bool DefaultShouldPostExceptionImpl(Exception ex)
        {
            if (lastPost + TimeSpan.FromSeconds(10) > DateTime.Now)
            {
                return false;
            }

            lastPost = DateTime.Now;
            return true;
        }
    }
}
