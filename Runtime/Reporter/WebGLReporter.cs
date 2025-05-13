using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using UnityEngine;

#if !UNITY_2022_1_OR_NEWER
using BugSplatUnity.Runtime.Util.Extensions;
#endif

namespace BugSplatUnity.Runtime.Reporter
{
    internal class WebGLReporter : IExceptionReporter
    {
        private IClientSettingsRepository clientSettings;
        private IWebGlExceptionClient exceptionClient;

        internal IReportUploadGuardService reportUploadGuardService;

        public WebGLReporter(
            IClientSettingsRepository clientSettings,
            IWebGlExceptionClient exceptionClient
        )
        {
            this.clientSettings = clientSettings;
            this.exceptionClient = exceptionClient;
            reportUploadGuardService = new ReportUploadGuardService(clientSettings);
        }

        public IEnumerator LogMessageReceived(string logMessage, string stackTrace, LogType type, Action<ExceptionReporterPostResult> callback = null)
        {
            if (!reportUploadGuardService.ShouldPostLogMessage(type))
            {
                yield break;
            }

            var options = new ReportPostOptions
            {
                Description = clientSettings.Description,
                Email = clientSettings.Email,
                Key = clientSettings.Key,
                Notes = clientSettings.Notes,
                User = clientSettings.User,
                CrashTypeId = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.UnityLegacy
            };

            foreach (var attribute in clientSettings.Attributes)
            {
                options.AdditionalAttributes.TryAdd(attribute.Key, attribute.Value);
            }

            stackTrace = $"{logMessage}\n{stackTrace}";

            yield return Post(stackTrace, options, callback);
        }

        public IEnumerator Post(Exception ex, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            if (!reportUploadGuardService.ShouldPostException(ex))
            {
                yield break;
            }

            options = options ?? new ReportPostOptions();
            options.SetNullOrEmptyValues(clientSettings);
            options.CrashTypeId = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;

            yield return Post(ex.ToString(), options, callback);
        }

        private IEnumerator Post(string stackTrace, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            if (clientSettings.CaptureEditorLog)
            {
                // TODO BG support this
                // https://github.com/BugSplat-Git/bugsplat-unity/issues/33
                Debug.Log($"BugSplat info: CaptureEditorLog is not implemented on this platform");
            }

            if (clientSettings.CapturePlayerLog)
            {
                // TODO BG support this
                // https://github.com/BugSplat-Git/bugsplat-unity/issues/32
                Debug.Log($"BugSplat info: CapturePlayerLog is not implemented on this platform");
            }

            if (clientSettings.CaptureScreenshots)
            {
                // TODO BG support this
                // https://github.com/BugSplat-Git/bugsplat-unity/issues/34
                Debug.Log("BugSplat info: CaptureScreenshots is not implemented on this platform");
            }

            yield return exceptionClient.Post(stackTrace, options, callback);
        }
    }
}
