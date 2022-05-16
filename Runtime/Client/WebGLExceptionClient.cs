using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BugSplatUnity.Runtime.Client
{
    internal class WebGLExceptionClient : IExceptionClient<IEnumerator>
    {
        private readonly string _database;
        private readonly string _application;
        private readonly string _version;

        public IUnityWebClient UnityWebClient { get; set; } = new UnityWebClient();

        public WebGLExceptionClient(string database, string application, string version)
        {
            _database = database;
            _application = application;
            _version = version;
        }

        public IEnumerator Post(string stackTrace, IReportPostOptions options = null)
        {
            return PostException(stackTrace, options);
        }

        public IEnumerator Post(Exception ex, IReportPostOptions options = null)
        {
            return PostException(ex.ToString(), options);
        }

        private IEnumerator PostException(string exception, IReportPostOptions options = null)
        {
            options = options ?? new ReportPostOptions();

            var url = $"https://{_database}.bugsplat.com/post/dotnetstandard/";
            var formData = new Dictionary<string, string>()
            {
                { "database", _database },
                { "appName", _application },
                { "appVersion", _version },
                { "description", options.Description },
                { "email", options.Email },
                { "appKey", options.Key },
                { "user", options.User },
                { "callstack", exception },
                { "crashTypeId", $"{(int)options.CrashTypeId}" }
            };

            var request = UnityWebClient.Post(url, formData);
            yield return request.SendWebRequest();

            if (!request.Success)
            {
                Debug.LogError($"BugSplat error: Could not post exception {request.Error}");
                yield break;
            }

            Debug.Log($"BugSplat info: status {request.responseCode}\n {request.downloadHandler.text}");
        }
    }
}
