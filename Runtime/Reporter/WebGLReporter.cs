using BugSplatDotNetStandard;
using Packages.com.bugsplat.unity.Runtime.Client;
using Packages.com.bugsplat.unity.Runtime.Settings;
using System;
using System.Collections;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{
    internal class WebGLReporter : IExceptionReporter
    {
        private IClientSettingsRepository _clientSettings;
        private IExceptionClient _exceptionClient;

        public WebGLReporter(IClientSettingsRepository clientSettings, IExceptionClient exceptionClient)
        {
            _clientSettings = clientSettings;
            _exceptionClient = exceptionClient;
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
                var result = await _exceptionClient.Post(stackTrace, options);
                var status = result.StatusCode;
                var contents = await result.Content.ReadAsStringAsync();
                Debug.Log($"BugSplat info: status {status}\n {contents}");
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

            yield return Task.Run(
                async () =>
                {
                    try
                    {
                        var result = await _exceptionClient.Post(exception, options);
                        callback?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"BugSplat error: {ex}");
                    }
                }
            );
        }
    }
}
