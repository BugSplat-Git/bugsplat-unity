using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.Windows;
using System.Linq;
using BugSplatDotNetStandard;

namespace BugSplatUnity
{
    /// <summary>
    /// A BugSplat implementation for Unity crash and exception reporting
    /// </summary>
    public class BugSplat
    {
        /// <summary>
        /// A list of files to be uploaded every time Post is called
        /// </summary>
        public List<FileInfo> Attachments
        {
            get
            {
                return bugsplat.Attachments;
            }
        }

        /// <summary>
        /// Upload Editor.log when Post is called
        /// </summary>
        public bool CaptureEditorLog { get; set; } = false;

        /// <summary>
        /// Upload Player.log when Post is called
        /// </summary>
        public bool CapturePlayerLog { get; set; } = true;

        /// <summary>
        /// Take a screenshot and upload it when Post is called
        /// </summary>
        public bool CaptureScreenshots { get; set; } = false;

        /// <summary>
        /// A guard that prevents Exceptions from being posted in rapid succession and must be able to handle null - defaults to 1 crash every 10 seconds.
        /// </summary>
        public Func<Exception, bool> ShouldPostException { get; set; } = ShouldPostExceptionImpl;

        /// <summary>
        /// A default description that can be overridden by call to Post
        /// </summary>
        public string Description
        {
            set
            {
                bugsplat.Description = value;
            }
        }

        /// <summary>
        /// A default email that can be overridden by call to Post
        /// </summary>
        public string Email
        {
            set
            {
                bugsplat.Email = value;
            }
        }

        /// <summary>
        /// A default key that can be overridden by call to Post
        /// </summary>
        public string Key
        {
            set
            {
                bugsplat.Key = value;
            }
        }

        /// <summary>
        /// A default user that can be overridden by call to Post
        /// </summary>
        public string User
        {
            set
            {
                bugsplat.User = value;
            }
        }

        private readonly BugSplatDotNetStandard.BugSplat bugsplat;
        private static DateTime lastPost;
        private static readonly string sentinelFileName = "BugSplatPostSuccess.txt";

        /// <summary>
        /// Post Exceptions and minidump files to BugSplat
        /// </summary>
        /// <param name="database">The BugSplat database for your organization</param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        public BugSplat(
            string database,
            string application,
            string version
        )
        {
            if (string.IsNullOrEmpty(database))
            {
                throw new ArgumentException("BugSplat error: database cannot be null or empty");
            }

            if (string.IsNullOrEmpty(application))
            {
                throw new ArgumentException("BugSplat error: application cannot be null or empty");
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException("BugSplat error: version cannot be null or empty");
            }

            bugsplat = new BugSplatDotNetStandard.BugSplat(database, application, version);
            bugsplat.MinidumpType = BugSplatDotNetStandard.BugSplat.MinidumpTypeId.UnityNativeWindows;
            bugsplat.ExceptionType = BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;
        }

        /// <summary>
        /// Event handler that will post the stackTrace to BugSplat if type equals LogType.Exception
        /// </summary>
        /// <param name="logMessage">logMessage provided by logMessageReceived event that will be used as post description</param>
        /// <param name="stackTrace">stackTrace provided by logMessageReceived event</param>
        /// <param name="type">type provided by logMessageReceived event</param>
        public async void LogMessageReceived(string logMessage, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            if (!ShouldPostException(null))
            {
                return;
            }

            var options = new ExceptionPostOptions();
            options.ExceptionType = BugSplatDotNetStandard.BugSplat.ExceptionTypeId.UnityLegacy;
            stackTrace = $"{logMessage}\n{stackTrace}";

            try
            {
                var result = await bugsplat.Post(stackTrace, options);
                var status = result.StatusCode;
                var contents = await result.Content.ReadAsStringAsync();
                Debug.Log($"BugSplat info: status {status}\n {contents}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"BugSplat error: {ex}");
            }
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="exception">The Exception that will be serialized and posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after exception is posted to BugSplat</param>
        public IEnumerator Post(Exception exception, ExceptionPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            if (!ShouldPostException(exception))
            {
                yield break;
            }

            options ??= new ExceptionPostOptions();

            if (CaptureEditorLog)
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

            if (CapturePlayerLog)
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

            if (CaptureScreenshots)
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
                        var result = await bugsplat.Post(exception, options);
                        callback?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"BugSplat error: {ex}");
                    }
                }
            );
        }

        /// <summary>
        /// Post all Unity player crashes that haven't been posted to BugSplat. Waits 1 second between posts to prevent rate-limiting.
        /// </summary>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after all crashes are posted to BugSplat</param>
        public IEnumerator PostAllCrashes(MinidumpPostOptions options = null, Action<List<HttpResponseMessage>> callback = null)
        {
#if UNITY_STANDALONE_WIN
            options ??= new MinidumpPostOptions();

            var crashReportFolder = new DirectoryInfo(CrashReporting.crashReportFolder);
            if (!crashReportFolder.Exists)
            {
                yield break;
            }

            var crashFolders = crashReportFolder.GetDirectories();
            var results = new List<HttpResponseMessage>();

            foreach (var crashFolder in crashFolders)
            {
                yield return new WaitForSeconds(1);
                yield return PostCrash(crashFolder, options, (response) => results.Add(response));
            }

            callback?.Invoke(results);
#else
            Debug.Log($"BugSplat info: PostAllCrashes is not implemented on this platform");
            yield return null;
#endif
        }

        /// <summary>
        /// Post a specifc crash to BugSplat and will skip crashes that have already been posted 
        /// </summary>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after the crash is posted to BugSplat</param>
        public IEnumerator PostCrash(DirectoryInfo crashFolder, MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
#if UNITY_STANDALONE_WIN
            options ??= new MinidumpPostOptions();

            if (crashFolder == null)
            {
                Debug.LogError($"BugSplat error: folder {crashFolder.Name} was not found");
                yield break;
            }

            var crashFiles = crashFolder.GetFiles();
            if (crashFiles.Any(file => file.Name == sentinelFileName))
            {
                Debug.Log($"BugSplat info: {crashFolder.Name} already posted, skipping...");
                yield break;
            }

            var minidump = crashFiles.Where(file => file.Extension == ".dmp").FirstOrDefault();
            if (minidump == null)
            {
                Debug.Log($"BugSplat info: {crashFolder.FullName} does not contain a minidump file, skipping...");
                yield break;
            }

            var attachments = crashFiles.Where(file => file.Extension != ".dmp");
            if (attachments != null)
            {
                options.AdditionalAttachments.AddRange(attachments);
            }

            Debug.Log($"BugSplat info: Posting {crashFolder.Name}");
            yield return Post(minidump, options, async (response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Debug.Log("BugSplat info: Crash post success, writing crash post sentinel file...");
                    var sentinelFilePath = Path.Combine(crashFolder.FullName, sentinelFileName);
                    var sentinelFileContents = await response.Content.ReadAsStringAsync();
                    System.IO.File.WriteAllText(sentinelFilePath, sentinelFileContents);
                }
                callback?.Invoke(response);
            });
#else
            Debug.Log($"BugSplat info: PostCrash is not implemented on this platform");
            yield return null;
#endif
        }

        /// <summary>
        /// Post the most recent Player crash to BugSplat and will skip crashes that have already been posted 
        /// </summary>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after the crash is posted to BugSplat</param>
        public IEnumerator PostMostRecentCrash(MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
#if UNITY_STANDALONE_WIN
            options ??= new MinidumpPostOptions();

            var folder = new DirectoryInfo(CrashReporting.crashReportFolder);
            var crashFolder = folder.GetDirectories()
                .OrderBy(dir => dir.LastWriteTime)
                .FirstOrDefault();

            yield return PostCrash(crashFolder, options, callback);
#else
            Debug.Log($"BugSplat info: PostMostRecentCrash is not implemented on this platform");
            yield return null;
#endif
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

        public IEnumerator Post(FileInfo minidump, MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA
            yield return Task.Run(
                async () =>
                {
                    try
                    {
                        var result = await bugsplat.Post(minidump, options);
                        callback?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"BugSplat error: {ex}");
                    }
                }
            );
#else
            Debug.Log($"BugSplat info: Post is not implemented on this platform");
            yield return null;
#endif
        }

        private static bool ShouldPostExceptionImpl(Exception ex)
        {
            if (lastPost + TimeSpan.FromSeconds(10) > DateTime.Now)
            {
                return false;
            }

            lastPost = DateTime.Now;
            return true;
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