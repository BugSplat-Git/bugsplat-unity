using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;
using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using BugSplatUnity.Runtime.Util;
using System.Collections.Generic;

#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace BugSplatUnity.Runtime.Reporter
{
    internal class DotNetStandardExceptionReporter : IExceptionReporter
    {
        private IClientSettingsRepository clientSettings;
        private IDotNetStandardExceptionClient exceptionClient;

        internal IReportUploadGuardService reportUploadGuardService;

        public DotNetStandardExceptionReporter(
            IClientSettingsRepository clientSettings,
            IDotNetStandardExceptionClient exceptionClient
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
                callback?.Invoke(new ExceptionReporterPostResult()
                {
                    Uploaded = false,
                    Exception = stackTrace,
                    Message = "BugSplat upload skipped due to ShouldPostLogMessage check.",
                });
                yield break;
            }

            var options = new ReportPostOptions();
            options.SetNullOrEmptyValues(clientSettings);
            options.CrashTypeId = (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.UnityLegacy;
            stackTrace = $"{logMessage}\n{stackTrace}";

            yield return Post(stackTrace, options, callback);
        }

        public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            var stackTrace = exception.ToString();

            if (!reportUploadGuardService.ShouldPostException(exception))
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

            var crashType = options?.CrashTypeId > 0 ? options.CrashTypeId : (int)BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;
            options = options ?? new ReportPostOptions();
            options.SetNullOrEmptyValues(clientSettings);
            options.CrashTypeId = crashType;

            var tempFiles = new List<FileInfo>();

            if (clientSettings.CaptureEditorLog)
            {
#if UNITY_EDITOR_WIN
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var editorLogFilePath = Path.Combine(localAppData, "Unity", "Editor", "Editor.log");
                var editorLogFileInfo = new FileInfo(editorLogFilePath);
                if (editorLogFileInfo.Exists)
                {
                    // Copy to a temp file to avoid sharing exception
                    var tempFile = CopyToTempFile(editorLogFileInfo);
                    options.AdditionalAttachments.Add(tempFile);
                    tempFiles.Add(tempFile);
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

            if (clientSettings.CapturePlayerLog)
            {
#if  UNITY_STANDALONE_WIN
                var localLowId = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
                var localLow = GetKnownFolderPath(localLowId);
                var playerLogFilePath = Path.Combine(localLow, Application.companyName, Application.productName, "Player.log");
                var playerLogFileInfo = new FileInfo(playerLogFilePath);
                if (playerLogFileInfo.Exists)
                {
                    // Copy to a temp file to avoid sharing exception
                    var tempFile = CopyToTempFile(playerLogFileInfo);
                    options.AdditionalAttachments.Add(tempFile);
                    tempFiles.Add(tempFile);
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

            if (clientSettings.CaptureScreenshots)
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
                        var result = await exceptionClient.Post(stackTrace, options);
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
                    finally
                    {
                        DeleteTempFiles(tempFiles);
                    }
                }
            );
        }

        private FileInfo CopyToTempFile(FileInfo fileToCopy)
        {
            var tempFile = new FileInfo(Path.Combine(fileToCopy.Directory.FullName, $"{Guid.NewGuid()}.log"));
            File.Copy(fileToCopy.FullName, tempFile.FullName);
            return tempFile;
        }

        private void DeleteTempFiles(IEnumerable<FileInfo> tempFiles)
        {
            foreach (var tempFile in tempFiles)
            {
                if (tempFile.Exists)
                {
                    tempFile.Delete();
                }
            }
        }

        private byte[] CaptureInMemoryPngScreenshot()
        {
            try
            {
                var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                texture.Apply();
                var result = texture.EncodeToPNG();
                UnityEngine.Object.Destroy(texture);
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
