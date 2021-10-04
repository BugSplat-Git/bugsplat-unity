using BugSplatDotNetStandard;
using Packages.com.bugsplat.unity.Runtime.Reporter;
using Packages.com.bugsplat.unity.Runtime.Settings;
using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Packages.com.bugsplat.unity.Runtime.Reporter
{

    // TODO BG better names for BugSplatClient and IBugSplatClient
    internal class DotNetStandardExceptionReporter: IExceptionReporter
    {
        private readonly IClientSettingsRepository _clientSettings;
        private readonly IExceptionClient _exceptionClient;

        public DotNetStandardExceptionReporter(IClientSettingsRepository clientSettings, IExceptionClient exceptionClient)
        {
            _clientSettings = clientSettings;
            _exceptionClient = exceptionClient;
            // TODO BG where should this go?
            //bugsplat.MinidumpType = BugSplat.MinidumpTypeId.UnityNativeWindows;
            //bugsplat.ExceptionType = BugSplat.ExceptionTypeId.Unity;
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

        public IEnumerator Post(Exception exception, ExceptionPostOptions options = null, Action<System.Net.Http.HttpResponseMessage> callback = null)
        {
            if (!_clientSettings.ShouldPostException(exception))
            {
                yield break;
            }

            options ??= new ExceptionPostOptions();

            if (_clientSettings.CaptureEditorLog)
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

            if (_clientSettings.CapturePlayerLog)
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

            if (_clientSettings.CaptureScreenshots)
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
