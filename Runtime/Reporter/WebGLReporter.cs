using BugSplatDotNetStandard;
using Packages.com.bugsplat.unity.Runtime.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    internal class WebGLReporter : IExceptionReporter
    {
        private IClientSettingsRepository _clientSettings;
        private readonly string _database;
        private readonly string _application;
        private readonly string _version;
        //private IExceptionClient _exceptionClient;

        public WebGLReporter(string database, string application, string version, IClientSettingsRepository clientSettings)
        {
            _database = database;
            _application = application;
            _version = version;
            _clientSettings = clientSettings;
            //_exceptionClient = exceptionClient;
        }

        public async Task LogMessageReceived(string logMessage, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            if (!_clientSettings.ShouldPostException(null))
            {
                return;
            }

            var options = new ExceptionPostOptions();
            options.ExceptionType = BugSplat.ExceptionTypeId.UnityLegacy;
            stackTrace = $"{logMessage}\n{stackTrace}";

            try
            {
                throw new NotImplementedException("TODO BG");
                //var result = await _exceptionClient.Post(stackTrace, options);
                //var status = result.StatusCode;
                //var contents = await result.Content.ReadAsStringAsync();
                //Debug.Log($"BugSplat info: status {status}\n {contents}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"BugSplat error: {ex}");
            }
        }

        public IEnumerator Post(Exception exception, ExceptionPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            if (!_clientSettings.ShouldPostException(exception))
            {
                yield break;
            }

            options ??= new ExceptionPostOptions();

            if (_clientSettings.CaptureEditorLog)
            {
                Debug.Log($"BugSplat info: CaptureEditorLog is not implemented on this platform");
            }

            if (_clientSettings.CapturePlayerLog)
            {
                // TODO BG can we support this?
                Debug.Log($"BugSplat info: CapturePlayerLog is not implemented on this platform");
            }

            if (_clientSettings.CaptureScreenshots)
            {
                // TODO BG can we support this?
                Debug.Log("BugSplat info: CaptureScreenshots is not implemented on this platform");
            }

            var url = $"https://{_database}.bugsplat.com/post/dotnetstandard/";
            //var formData = new List<IMultipartFormSection>();
            //formData.Add(new MultipartFormDataSection($"database=${_database}"));
            //formData.Add(new MultipartFormDataSection($"appName=${_application}"));
            //formData.Add(new MultipartFormDataSection($"version=${_version}"));
            //formData.Add(new MultipartFormDataSection($"description=${_clientSettings.Description}"));
            //formData.Add(new MultipartFormDataSection($"email=${_clientSettings.Email}"));
            //formData.Add(new MultipartFormDataSection($"appKey=${_clientSettings.Key}"));
            //formData.Add(new MultipartFormDataSection($"user=${_clientSettings.User}"));
            //formData.Add(new MultipartFormDataSection($"callstack=${exception}"));
            var formData = new WWWForm();
            formData.AddField("database", _database);
            formData.AddField("appName", _application);
            formData.AddField("appVersion", _version);
            formData.AddField("description", _clientSettings.Description);
            formData.AddField("email", _clientSettings.Email);
            formData.AddField("appKey", _clientSettings.Key);
            formData.AddField("user", _clientSettings.User);
            formData.AddField("callstack", exception.ToString());
            formData.AddField("crashTypeId", 24);

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
