using BugSplatDotNetStandard;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

// TODO BG this namespace was Exception but that caused 'Exception' type to be treated as
// a namespace in BugSplatClient... can we do better with the namespace name here?
namespace Packages.com.bugsplat.unity.Runtime.Client
{
    internal interface IExceptionClient
    {
        Task<HttpResponseMessage> Post(string stackTrace, ExceptionPostOptions options = null);
        Task<HttpResponseMessage> Post(Exception ex, ExceptionPostOptions options = null);
    }
}
