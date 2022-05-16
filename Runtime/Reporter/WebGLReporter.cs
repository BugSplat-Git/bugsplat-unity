using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using UnityEngine;

namespace BugSplatUnity.Runtime.Reporter
{
    internal class WebGLReporter : MonoBehaviour, IExceptionReporter
    {
        public IClientSettingsRepository ClientSettings { get; set; }
        public IExceptionClient<IEnumerator> ExceptionClient { get; set; }

        internal IReportUploadGuardService _reportUploadGuardService;

        public static WebGLReporter Create(
            IClientSettingsRepository clientSettings,
            IExceptionClient<IEnumerator> exceptionClient,
            GameObject gameObject
        )
        {
            var reporter = gameObject.AddComponent(typeof(WebGLReporter)) as WebGLReporter;
            reporter.ClientSettings = clientSettings;
            reporter.ExceptionClient = exceptionClient;
            reporter._reportUploadGuardService = new ReportUploadGuardService(clientSettings);
            return reporter;
        }

        public void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action callback = null)
        {
            if (!_reportUploadGuardService.ShouldPostLogMessage(type))
            {
                return;
            }

            var options = new ReportPostOptions();
            options.Description = ClientSettings.Description;
            options.Email = ClientSettings.Email;
            options.Key = ClientSettings.Key;
            options.User = ClientSettings.User;
            options.CrashTypeId = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.UnityLegacy;
            stackTrace = $"{logMessage}\n{stackTrace}";

            StartCoroutine(Post(stackTrace, options, callback));
        }

        public IEnumerator Post(Exception ex, IReportPostOptions options = null, Action callback = null)
        {
            if (!_reportUploadGuardService.ShouldPostException(ex))
            {
                yield break;
            }

            options = options ?? new ReportPostOptions();
            options.SetNullOrEmptyValues(ClientSettings);
            options.CrashTypeId = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;

            yield return Post(ex.ToString(), options, callback);
        }

        private IEnumerator Post(string stackTrace, IReportPostOptions options = null, Action callback = null)
        {
            if (ClientSettings.CaptureEditorLog)
            {
                // TODO BG support this
                // https://github.com/BugSplat-Git/bugsplat-unity/issues/33
                Debug.Log($"BugSplat info: CaptureEditorLog is not implemented on this platform");
            }

            if (ClientSettings.CapturePlayerLog)
            {
                // TODO BG support this
                // https://github.com/BugSplat-Git/bugsplat-unity/issues/32
                Debug.Log($"BugSplat info: CapturePlayerLog is not implemented on this platform");
            }

            if (ClientSettings.CaptureScreenshots)
            {
                // TODO BG support this
                // https://github.com/BugSplat-Git/bugsplat-unity/issues/34
                Debug.Log("BugSplat info: CaptureScreenshots is not implemented on this platform");
            }

            yield return ExceptionClient.Post(stackTrace, options);

            callback?.Invoke();
        }
    }
}
