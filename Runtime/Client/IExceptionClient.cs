using System;

namespace BugSplatUnity.Runtime.Client
{
    internal interface IExceptionClient<T>
    {
        T Post(string stackTrace, IReportPostOptions options = null);
        T Post(Exception ex, IReportPostOptions options = null);
    }
}
