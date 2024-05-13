using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

[assembly: InternalsVisibleTo("BugSplat.Unity.RuntimeTests")]
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
                return clientSettings.Attachments;
            }
        }

        /// <summary>
        /// Upload Editor.log when Post is called
        /// </summary>
        public bool CaptureEditorLog
        {
            get
            {
                return clientSettings.CaptureEditorLog;
            }
            set
            {
                clientSettings.CaptureEditorLog = value;
            }
        }

        /// <summary>
        /// Upload Player.log when Post is called
        /// </summary>
        public bool CapturePlayerLog
        {
            get 
            {
                return clientSettings.CapturePlayerLog;
            }
            set
            {
                clientSettings.CapturePlayerLog = value;
            }
        }

        /// <summary>
        /// Take a screenshot and upload it when Post is called
        /// </summary>
        public bool CaptureScreenshots
        {
            get
            {
                return clientSettings.CaptureScreenshots;
            }
            set
            {
                clientSettings.CaptureScreenshots = value;
            }
        }

        /// <summary>
        /// Determines whether BugSplat should post exceptions when user is in the Unity editor.
        /// </summary>
        public bool PostExceptionsInEditor
        {
            get
            {
                return clientSettings.PostExceptionsInEditor;
            }
            set
            {
                clientSettings.PostExceptionsInEditor = value;
            }
        }

        /// <summary>
        /// A guard that prevents Exceptions from being posted in rapid succession and must be able to handle null - defaults to 1 crash every 10 seconds.
        /// </summary>
        public Func<Exception, bool> ShouldPostException
        {
            get
            {
                return clientSettings.ShouldPostException;
            }
            set
            {
                clientSettings.ShouldPostException = value;
            }
        }

        /// <summary>
        /// A default description that can be overridden by call to Post
        /// </summary>
        public string Description
        {
            set
            {
                clientSettings.Description = value;
            }
        }

        /// <summary>
        /// A default email that can be overridden by call to Post
        /// </summary>
        public string Email
        {
            set
            {
                clientSettings.Email = value;
            }
        }

        /// <summary>
        /// A default key that can be overridden by call to Post
        /// </summary>
        public string Key
        {
            set
            {
                clientSettings.Key = value;
            }
        }

        // <summary>
        /// A general purpose field that can be overridden by call to Post. 
        /// </summary>
        public string Notes
        {
            set
            {
                clientSettings.Notes = value;
            }
        }

        /// <summary>
        /// A default user that can be overridden by call to Post
        /// </summary>
        public string User
        {
            set
            {
                clientSettings.User = value;
            }
        }

        private IClientSettingsRepository clientSettings;
        private IExceptionReporter exceptionReporter;

#if UNITY_STANDALONE_WIN || UNITY_WSA
        private readonly INativeCrashReporter nativeCrashReporter;
#endif

        /// <summary>
        /// Post Exceptions and minidump files to BugSplat
        /// </summary>
        /// <param name="database">The BugSplat database for your organization</param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        /// <param name="useNativeLibIos">Whether to use the native library for crash reporting on IOS</param>
        /// <param name="useNativeLibAndroid">Whether to use the native library for crash reporting on Android</param>
        public BugSplat(
            string database,
            string application,
            string version,
            bool useNativeLibIos, 
            bool useNativeLibAndroid 
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

            var gameObject = new GameObject();

#if UNITY_STANDALONE_WIN || UNITY_WSA           
            var bugsplat = new BugSplatDotNetStandard.BugSplat(database, application, version);
            bugsplat.MinidumpType = BugSplatDotNetStandard.BugSplat.MinidumpTypeId.UnityNativeWindows;
            bugsplat.ExceptionType = BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;
            var dotNetStandardClientSettings = new DotNetStandardClientSettingsRepository(bugsplat);
            var dotNetStandardClient = new DotNetStandardClient(bugsplat);
            var dotNetStandardExceptionReporter = DotNetStandardExceptionReporter.Create(dotNetStandardClientSettings, dotNetStandardClient, gameObject);
            var windowsReporter = new WindowsReporter(dotNetStandardClientSettings, dotNetStandardExceptionReporter, dotNetStandardClient);

            clientSettings = dotNetStandardClientSettings;
            exceptionReporter = windowsReporter;
            nativeCrashReporter = windowsReporter;
#elif UNITY_WEBGL
            var webGLClientSettings = new WebGLClientSettingsRepository();
            var webGLExceptionClient = new WebGLExceptionClient(database, application, version);
            var webGLReporter = WebGLReporter.Create(
                webGLClientSettings,
                webGLExceptionClient,
                gameObject
            );
            clientSettings = webGLClientSettings;
            exceptionReporter = webGLReporter;
#elif UNITY_IOS && !UNITY_EDITOR
            if (useNativeLibIos)
                _startBugSplat();

            version = $"{Application.version} ({_getBuildNumber()})";
            UseDotNetHandler(database, application, version);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (useNativeLibAndroid)
            {
                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                
                var javaClass = new AndroidJavaClass("com.ninevastudios.bugsplatunitylib.BugSplatBridge");
                javaClass.CallStatic("initBugSplat", activity, database, application, version);
            }

            UseDotNetHandler(database, application, version, gameObject);
#else
            UseDotNetHandler(database, application, version, gameObject);
#endif
        }

        private void UseDotNetHandler(string database, string application, string version, GameObject gameObject)
        {
            var bugsplat = new BugSplatDotNetStandard.BugSplat(database, application, version)
            {
                MinidumpType = BugSplatDotNetStandard.BugSplat.MinidumpTypeId.UnityNativeWindows,
                ExceptionType = BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity
            };
            var dotNetStandardClientSettings = new DotNetStandardClientSettingsRepository(bugsplat);
            var dotNetStandardClient = new DotNetStandardClient(bugsplat);
            var dotNetStandardExceptionReporter = DotNetStandardExceptionReporter.Create(dotNetStandardClientSettings, dotNetStandardClient, gameObject);

            clientSettings = dotNetStandardClientSettings;
            exceptionReporter = dotNetStandardExceptionReporter;
        }

        /// <summary>
        /// Constructs and returns a BugSplat object from BugSplatOptions
        /// </summary>
        /// <param name="options">collection of options which can be used to configure a BugSplat object </param>
        public static BugSplat CreateFromOptions(BugSplatOptions options)
        {
            var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;
            var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
            
            var bugSplat = new BugSplat(
                options.Database,
                application,
                version, 
                options.UseNativeCrashReportingForIos,
                options.UseNativeCrashReportingForAndroid
            );

            bugSplat.Description = options.Description;
            bugSplat.Email = options.Email;
            bugSplat.Key = options.Key;
            bugSplat.Notes = options.Notes;
            bugSplat.User = options.User;
            bugSplat.CaptureEditorLog = options.CaptureEditorLog;
            bugSplat.CapturePlayerLog = options.CapturePlayerLog;
            bugSplat.CaptureScreenshots = options.CaptureScreenshots;
            bugSplat.PostExceptionsInEditor = options.PostExceptionsInEditor;

            if (options.PersistentDataFileAttachmentPaths != null)
			{
                foreach (var filePath in options.PersistentDataFileAttachmentPaths)
                {
                    var trimmedFilePath = filePath.TrimStart('/', '\\');
                    var fullFilePath = Path.Combine(Application.persistentDataPath, trimmedFilePath); 
                    var fileInfo = new FileInfo(fullFilePath);
                    bugSplat.Attachments.Add(fileInfo);
                }
            }

            return bugSplat;
        }

        /// <summary>
        /// Event handler that will post the stackTrace to BugSplat if type equals LogType.Exception
        /// </summary>
        /// <param name="logMessage">logMessage provided by logMessageReceived event that will be used as post description</param>
        /// <param name="stackTrace">stackTrace provided by logMessageReceived event</param>
        /// <param name="type">type provided by logMessageReceived event</param>
        public void LogMessageReceived(string logMessage, string stackTrace, LogType type)
        {
            exceptionReporter.LogMessageReceived(logMessage, stackTrace, type);
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="exception">The Exception that will be serialized and posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after exception is posted to BugSplat</param>
        public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            return exceptionReporter.Post(exception, options, callback);
        }

        /// <summary>
        /// Post all Unity player crashes that haven't been posted to BugSplat. Waits 1 second between posts to prevent rate-limiting.
        /// </summary>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after all crashes are posted to BugSplat</param>
        public IEnumerator PostAllCrashes(IReportPostOptions options = null, Action<List<HttpResponseMessage>> callback = null)
        {
#if UNITY_STANDALONE_WIN
            return nativeCrashReporter.PostAllCrashes(options, callback);
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
        public IEnumerator PostCrash(DirectoryInfo crashFolder, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
#if UNITY_STANDALONE_WIN
            return nativeCrashReporter.PostCrash(new WrappedDirectoryInfo(crashFolder), options, callback);
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
        public IEnumerator PostMostRecentCrash(IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
#if UNITY_STANDALONE_WIN
            return nativeCrashReporter.PostMostRecentCrash(options, callback);
#else
            Debug.Log($"BugSplat info: PostMostRecentCrash is not implemented on this platform");
            yield return null;
#endif
        }

        public IEnumerator Post(FileInfo minidump, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA
            return nativeCrashReporter.Post(minidump, options, callback);
#else
            Debug.Log($"BugSplat info: Post is not implemented on this platform");
            yield return null;
#endif
        }
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void _startBugSplat();
        
        [DllImport("__Internal")]
        static extern string _getBuildNumber();
#endif
    }
}