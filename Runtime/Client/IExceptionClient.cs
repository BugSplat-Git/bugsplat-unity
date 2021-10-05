using BugSplatDotNetStandard;
using System;

namespace Packages.com.bugsplat.unity.Runtime.Client
{
    internal interface IExceptionClient<T>
    {
        T Post(string stackTrace, ExceptionPostOptions options = null);
        T Post(Exception ex, ExceptionPostOptions options = null);
    }
}
