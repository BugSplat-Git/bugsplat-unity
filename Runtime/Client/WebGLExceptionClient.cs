using BugSplatDotNetStandard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Packages.com.bugsplat.unity.Runtime.Client
{
    internal class WebGLExceptionClient : IExceptionClient
    {
        public Task<HttpResponseMessage> Post(string stackTrace, ExceptionPostOptions options = null)
        {

            var url = $"https://{_database}.bugsplat.com/post/dotnetstandard/";
            var formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormDataSection($"database=${_database}"));
            formData.Add(new MultipartFormDataSection($"appName=${_application}"));
            formData.Add(new MultipartFormDataSection($"version=${_version}"));
            formData.Add(new MultipartFormDataSection($"description=${Description}"));
            formData.Add(new MultipartFormDataSection($"email=${Email}"));
            formData.Add(new MultipartFormDataSection($"appKey=${Key}"));
            formData.Add(new MultipartFormDataSection($"user=${User}"));
            formData.Add(new MultipartFormDataSection($"callstack=${logMessage}\n${stackTrace}"));

            var www = UnityWebRequest.Post(url, formData);
            var request = www.SendWebRequest();
            var task = GetTask(request);
            await task;

            //if (www.result != UnityWebRequest.Result.Success)
            //{
            //    Debug.Log(www.error);
            //}
            //else
            //{
            //    Debug.Log("Form upload complete!");
            //}
        }

        public Task<HttpResponseMessage> Post(Exception ex, ExceptionPostOptions options = null)
        {
            throw new NotImplementedException();
        }


        // https://gist.github.com/krzys-h/9062552e33dd7bd7fe4a6c12db109a1a
        // https://gist.github.com/mattyellen/d63f1f557d08f7254345bff77bfdc8b3
        Task GetTask(AsyncOperation asyncOp)
        {
            var tcs = new TaskCompletionSource<object>();
            asyncOp.completed += obj => { tcs.SetResult(null); };
            return tcs.Task;
        }
    }
}
