using BugSplatDotNetStandard;
using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using System.Net.Http;
using UnityEngine;

namespace BugSplatUnity.Runtime.Reporter
{
    internal class WebGLReporter : MonoBehaviour, IExceptionReporter
    {
        public IClientSettingsRepository ClientSettings { get; set; }
        public IExceptionClient<IEnumerator> ExceptionClient { get; set; }

        public static WebGLReporter Create(IClientSettingsRepository clientSettings, IExceptionClient<IEnumerator> exceptionClient, GameObject gameObject)
        {
            var reporter = gameObject.AddComponent(typeof(WebGLReporter)) as WebGLReporter;
            reporter.ClientSettings = clientSettings;
            reporter.ExceptionClient = exceptionClient;
            return reporter;
        }

        public void LogMessageReceived(string logMessage, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            if (!ClientSettings.ShouldPostException(null))
            {
                return;
            }

            var options = (IExceptionPostOptions)new ExceptionPostOptions();
            options.Description = ClientSettings.Description;
            options.Email = ClientSettings.Email;
            options.Key = ClientSettings.Key;
            options.User = ClientSettings.User;
            options.ExceptionType = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.UnityLegacy;
            stackTrace = $"{logMessage}\n{stackTrace}";

            StartCoroutine(ExceptionClient.Post(stackTrace, options));
        }

        public IEnumerator Post(Exception exception, IExceptionPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            if (!ClientSettings.ShouldPostException(exception))
            {
                yield break;
            }

            // TODO BG test
            options ??= (IExceptionPostOptions)new ExceptionPostOptions();
            options.SetNullOrEmptyValues(ClientSettings);
            options.ExceptionType = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;

            if (ClientSettings.CaptureEditorLog)
            {
                // TODO BG can we support this?
                Debug.Log($"BugSplat info: CaptureEditorLog is not implemented on this platform");
            }

            if (ClientSettings.CapturePlayerLog)
            {
                // TODO BG can we support this?
                Debug.Log($"BugSplat info: CapturePlayerLog is not implemented on this platform");
            }

            if (ClientSettings.CaptureScreenshots)
            {
                // TODO BG can we support this?
                Debug.Log("BugSplat info: CaptureScreenshots is not implemented on this platform");
            }

            // TODO BG can we return the response message here?
            yield return ExceptionClient.Post(exception, options);
        }
    }
}
