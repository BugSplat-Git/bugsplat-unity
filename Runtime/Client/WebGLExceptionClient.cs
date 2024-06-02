using System;
using System.Collections;
using System.Collections.Generic;
using BugSplatUnity.Runtime.Reporter;
using UnityEngine;

namespace BugSplatUnity.Runtime.Client
{
    internal interface IWebGlExceptionClient
    {
        IEnumerator Post(string stackTrace, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null);
        IEnumerator Post(Exception ex, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null);
    }

    internal class WebGLExceptionClient : IWebGlExceptionClient
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

        public IEnumerator Post(string stackTrace, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            return PostException(stackTrace, options);
        }

        public IEnumerator Post(Exception ex, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            return PostException(ex.ToString(), options);
        }

        private IEnumerator PostException(string exception, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
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
                { "crashTypeId", $"{options.CrashTypeId}" }
            };

            var request = UnityWebClient.Post(url, formData);
            yield return request.SendWebRequest();

            var responseCode = request.ResponseCode;

            if (!request.Success)
            {
                Debug.LogError($"BugSplat error: Could not post exception {request.Error}");

                callback?.Invoke(new ExceptionReporterPostResult() {
                    Uploaded = false,
                    Exception = exception,
                    Message = $"BugSplat upload failed with code {responseCode}"
                });

                yield break;
            }

            var responseText = request.DownloadHandler.Text;
            var response = JsonUtility.FromJson<BugSplatResponse>(responseText);
            Debug.Log($"BugSplat info: status {responseCode}\n {responseText}");

            callback?.Invoke(new ExceptionReporterPostResult() {
                Uploaded = true,
                Exception = exception,
                Message =  "Crash successfully uploaded to BugSplat!",
                Response = response
            });
        }
    }
}
