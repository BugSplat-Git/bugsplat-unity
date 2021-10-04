using BugSplatDotNetStandard;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    internal class WebGLReporter : IExceptionReporter
    {
        public WebGLReporter(string database, string application, string version)
        {
            _database = database;
            _application = application;
            _version = version;
        }

        public async Task LogMessageReceived(string logMessage, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            if (!ShouldPostException(null))
            {
                return;
            }

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

        public IEnumerator Post(Exception exception, ExceptionPostOptions options = null, Action<HttpResponseMessage> callback = null)
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
