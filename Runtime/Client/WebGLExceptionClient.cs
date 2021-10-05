using BugSplatDotNetStandard;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Packages.com.bugsplat.unity.Runtime.Client
{
    internal class WebGLExceptionClient : IExceptionClient<IEnumerator>
    {
        private readonly string _database;
        private readonly string _application;
        private readonly string _version;

        public WebGLExceptionClient(string database, string application, string version)
        {
            _database = database;
            _application = application;
            _version = version;
        }

        public IEnumerator Post(string stackTrace, ExceptionPostOptions options = null)
        {
            return PostException(stackTrace, options);
        }

        public IEnumerator Post(Exception ex, ExceptionPostOptions options = null)
        {
            return PostException(ex.ToString(), options);
        }

        private IEnumerator PostException(string exception, ExceptionPostOptions options = null)
        {
            options ??= new ExceptionPostOptions();

            var url = $"https://{_database}.bugsplat.com/post/dotnetstandard/";
            var formData = new WWWForm();
            formData.AddField("database", _database);
            formData.AddField("appName", _application);
            formData.AddField("appVersion", _version);
            formData.AddField("description", options.Description);
            formData.AddField("email", options.Email);
            formData.AddField("appKey", options.Key);
            formData.AddField("user", options.User);
            formData.AddField("callstack", exception);
            formData.AddField("crashTypeId", $"{(int)options.ExceptionType}");

            var request = UnityWebRequest.Post(url, formData);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"BugSplat error: Could not post exception {request.error}");
                yield break;
            }

            Debug.Log($"BugSplat info: status {request.responseCode}\n {request.downloadHandler.text}");
        }
    }
}
