using BugSplatDotNetStandard;
using Packages.com.bugsplat.unity.Runtime.Settings;
using System;
using System.Collections;
using System.Net.Http;
using UnityEngine;
using UnityEngine.Networking;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    internal class WebGLReporter : MonoBehaviour, IExceptionReporter
    {
        private IClientSettingsRepository Settings;
        public string Database { get; set; }
        public string Application { get; set; }
        public string Version { get; set; }

        public static WebGLReporter Create(string database, string application, string version, IClientSettingsRepository clientSettings, GameObject gameObject)
        {
            var reporter = gameObject.AddComponent(typeof(WebGLReporter)) as WebGLReporter;
            reporter.Database = database;
            reporter.Application = application;
            reporter.Version = version;
            reporter.Settings = clientSettings;
            return reporter;
        }


        public void LogMessageReceived(string logMessage, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            if (!Settings.ShouldPostException(null))
            {
                return;
            }

            var options = new ExceptionPostOptions();
            options.ExceptionType = BugSplat.ExceptionTypeId.UnityLegacy;
            stackTrace = $"{logMessage}\n{stackTrace}";

            StartCoroutine(PostException(stackTrace, 12));
        }

        public IEnumerator Post(Exception exception, ExceptionPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            if (!Settings.ShouldPostException(exception))
            {
                yield break;
            }

            options ??= new ExceptionPostOptions();

            if (Settings.CaptureEditorLog)
            {
                // TODO BG can we support this?
                Debug.Log($"BugSplat info: CaptureEditorLog is not implemented on this platform");
            }

            if (Settings.CapturePlayerLog)
            {
                // TODO BG can we support this?
                Debug.Log($"BugSplat info: CapturePlayerLog is not implemented on this platform");
            }

            if (Settings.CaptureScreenshots)
            {
                // TODO BG can we support this?
                Debug.Log("BugSplat info: CaptureScreenshots is not implemented on this platform");
            }

            yield return PostException(exception.ToString(), 24);
        }

        private IEnumerator PostException(string exception, int crashTypeId)
        {
            var url = $"https://{Database}.bugsplat.com/post/dotnetstandard/";
            var formData = new WWWForm();
            formData.AddField("database", Database);
            formData.AddField("appName", Application);
            formData.AddField("appVersion", Version);
            formData.AddField("description", Settings.Description);
            formData.AddField("email", Settings.Email);
            formData.AddField("appKey", Settings.Key);
            formData.AddField("user", Settings.User);
            formData.AddField("callstack", exception.ToString());
            formData.AddField("crashTypeId", crashTypeId);

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
