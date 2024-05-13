using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;
using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using BugSplatUnity.Runtime.Util;

#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace BugSplatUnity.Runtime.Reporter
{
    internal class DotNetStandardExceptionReporter : MonoBehaviour, IExceptionReporter
    {
        public IClientSettingsRepository ClientSettings;
        public IDotNetStandardExceptionClient ExceptionClient;

        internal IReportUploadGuardService _reportUploadGuardService;

        public static DotNetStandardExceptionReporter Create(
            IClientSettingsRepository clientSettings,
            IDotNetStandardExceptionClient exceptionClient,
            GameObject gameObject
        )
        {
            var reporter = gameObject.AddComponent(typeof(DotNetStandardExceptionReporter)) as DotNetStandardExceptionReporter;
            reporter.ClientSettings = clientSettings;
            reporter.ExceptionClient = exceptionClient;
            reporter._reportUploadGuardService = new ReportUploadGuardService(clientSettings);
            return reporter;
        }

        public void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action<ExceptionReporterPostResult> callback = null)
        {
            if (!_reportUploadGuardService.ShouldPostLogMessage(type))
            {
                callback?.Invoke(new ExceptionReporterPostResult()
                {
                    Uploaded = false,
                    Exception = stackTrace,
                    Message = "BugSplat upload skipped due to ShouldPostLogMessage check.",
                });
                return;
            }

            var options = new ReportPostOptions();
            options.SetNullOrEmptyValues(ClientSettings);
            options.CrashTypeId = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.UnityLegacy;
            stackTrace = $"{logMessage}\n{stackTrace}";

            StartCoroutine(Post(stackTrace, options, callback));
        }

        public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            var stackTrace = exception.ToString();
            
            if (!_reportUploadGuardService.ShouldPostException(exception))
            {
                callback?.Invoke(new ExceptionReporterPostResult()
                {
                    Uploaded = false,
                    Exception = stackTrace,
                    Message = "BugSplat upload skipped due to ShouldPostException check.",
                });
                yield break;
            }

            yield return Post(stackTrace, options, callback);
        }

        private IEnumerator Post(string stackTrace, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            Debug.Log("About to post exception to BugSplat");

            options = options ?? new ReportPostOptions();
            options.SetNullOrEmptyValues(ClientSettings);
            options.CrashTypeId = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;

            if (ClientSettings.CaptureEditorLog)
            {
#if UNITY_EDITOR_WIN
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var editorLogFilePath = Path.Combine(localAppData, "Unity", "Editor", "Editor.log");
                var editorLogFileInfo = new FileInfo(editorLogFilePath);
                if (editorLogFileInfo.Exists)
                {
                    options.AdditionalAttachments.Add(editorLogFileInfo);
                }
                else
                {
                    Debug.Log($"BugSplat info: Could not find {editorLogFileInfo.FullName}, skipping...");
                }
#elif UNITY_EDITOR_OSX
                var home = Environment.GetEnvironmentVariable("HOME");
                var editorLogFilePath = Path.Combine(home, "Library", "Logs", "Unity", "Editor.log");
                var editorLogFileInfo = new FileInfo(editorLogFilePath);
                if (editorLogFileInfo.Exists)
                {
                    options.AdditionalAttachments.Add(editorLogFileInfo);
                }
                else
                {
                    Debug.Log($"BugSplat info: Could not find {editorLogFileInfo.FullName}, skipping...");
                }
#elif UNITY_EDITOR_LINUX
                var home = Environment.GetEnvironmentVariable("HOME");
                var editorLogFilePath = Path.Combine(home, ".config", "unity3d", "Editor.log");
                var editorLogFileInfo = new FileInfo(editorLogFilePath);
                if (editorLogFileInfo.Exists)
                {
                    options.AdditionalAttachments.Add(editorLogFileInfo);
                }
                else
                {
                    Debug.Log($"BugSplat info: Could not find {editorLogFileInfo.FullName}, skipping...");
                }
#else
                Debug.Log($"BugSplat info: CaptureEditorLog is not implemented on this platform");
#endif
            }

            if (ClientSettings.CapturePlayerLog)
            {
#if  UNITY_STANDALONE_WIN
                var localLowId = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
                var localLow = GetKnownFolderPath(localLowId);
                var playerLogFilePath = Path.Combine(localLow, Application.companyName, Application.productName, "Player.log");
                var playerLogFileInfo = new FileInfo(playerLogFilePath);
                if (playerLogFileInfo.Exists)
                {
                    options.AdditionalAttachments.Add(playerLogFileInfo);
                }
                else
                {
                    Debug.Log($"BugSplat info: Could not find {playerLogFileInfo.FullName}, skipping...");
                }
#elif UNITY_STANDALONE_OSX
                var home = Environment.GetEnvironmentVariable("HOME");
                var playerLogFilePath = Path.Combine(home, "Library", "Logs", Application.companyName, Application.productName, "Player.log");
                var playerLogFileInfo = new FileInfo(playerLogFilePath);
                if (playerLogFileInfo.Exists)
                {
                    options.AdditionalAttachments.Add(playerLogFileInfo);
                }
                else
                {
                    Debug.Log($"BugSplat info: Could not find {playerLogFileInfo.FullName}, skipping...");
                }
#elif UNITY_STANDALONE_LINUX
                var home = Environment.GetEnvironmentVariable("HOME");
                var editorLogFilePath = Path.Combine(home, ".config", "unity3d", Application.companyName, Application.productName, "Player.log");
                var editorLogFileInfo = new FileInfo(editorLogFilePath);
                if (editorLogFileInfo.Exists)
                {
                    options.AdditionalAttachments.Add(editorLogFileInfo);
                }
                else
                {
                    Debug.Log($"BugSplat info: Could not find {editorLogFileInfo.FullName}, skipping...");
                }
#elif UNITY_WSA
                var tempState = Application.temporaryCachePath;
                var playerLogFilePath = Path.Combine(tempState, "UnityPlayer.log");
                var playerLogFileInfo = new FileInfo(playerLogFilePath);
                if (playerLogFileInfo.Exists)
                {
                    options.AdditionalAttachments.Add(playerLogFileInfo);
                }
                else
                {
                    Debug.Log($"BugSplat info: Could not find {playerLogFileInfo.FullName}, skipping...");
                }
#else
                Debug.Log($"BugSplat info: CapturePlayerLog is not implemented on this platform");
#endif
            }

            if (ClientSettings.CaptureScreenshots)
            {
                // There isn't really a safe way to do this
                // Serializing the image to disk potentially litters the file system
                // Capturing the image in memory potentially risks a memory exception
                yield return new WaitForEndOfFrame();
                var bytes = CaptureInMemoryPngScreenshot();
                if (bytes != null)
                {
                    var param = new FormDataParam()
                    {
                        Name = "screenshot",
                        Content = new ByteArrayContent(bytes),
                        FileName = "screenshot.png"
                    };
                    options.AdditionalFormDataParams.Add(param);
                }
            }

            yield return Task.Run(
                async () =>
                {
                    try
                    {
                        var result = await ExceptionClient.Post(stackTrace, options);
                        var status = result.StatusCode;
                        var contents = await result.Content.ReadAsStringAsync();
                        var uploaded = status == System.Net.HttpStatusCode.OK;
                        var message = uploaded ? "Crash successfully uploaded to BugSplat!" : $"BugSplat upload failed with code {status}";
                        var response = JsonUtility.FromJson<BugSplatResponse>(contents);
                        Debug.Log($"BugSplat info: status {status}\n {contents}");
                        callback?.Invoke(new ExceptionReporterPostResult()
                        {
                            Uploaded = uploaded,
                            Exception = stackTrace,
                            Message = message,
                            Response = response
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"BugSplat error: {ex}");
                        callback?.Invoke(new ExceptionReporterPostResult()
                        {
                            Uploaded = false,
                            Exception = stackTrace,
                            Message = $"BugSplat upload failed with exception: {ex}",
                        });
                    }
                }
            );
        }

        private byte[] CaptureInMemoryPngScreenshot()
        {
            try
            {
                var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                texture.Apply();
                var result = texture.EncodeToPNG();
                GameObject.Destroy(texture);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"BugSplat error: Could not create a screenshot {ex.Message}");
                return null;
            }
        }


#if UNITY_STANDALONE_WIN
        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

        private string GetKnownFolderPath(Guid knownFolderId)
        {
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                int hr = SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out pszPath);
                if (hr >= 0)
                    return Marshal.PtrToStringAuto(pszPath);
                throw Marshal.GetExceptionForHR(hr);
            }
            finally
            {
                if (pszPath != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pszPath);
            }
        }
#endif
    }
}
