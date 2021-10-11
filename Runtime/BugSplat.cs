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
        /// A guard that prevents Exceptions from being posted in rapid succession and must be able to handle null - defaults to 1 crash every 10 seconds.
        /// </summary>
        /// 
        // TODO can we be more explicit that the Exception might be null with the Type here?
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

        private readonly IClientSettingsRepository clientSettings;
        private readonly IExceptionReporter exceptionReporter;

#if UNITY_STANDALONE_WIN || UNITY_WSA
        private readonly INativeCrashReporter nativeCrashReporter;
#endif

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

#if UNITY_STANDALONE_WIN || UNITY_WSA
            var bugsplat = new BugSplatDotNetStandard.BugSplat(database, application, version);
            bugsplat.MinidumpType = BugSplatDotNetStandard.BugSplat.MinidumpTypeId.UnityNativeWindows;
            bugsplat.ExceptionType = BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;
            var dotNetStandardClientSettings = new DotNetStandardClientSettingsRepository(bugsplat);
            var dotNetStandardClient = new DotNetStandardClient(bugsplat);
            var dotNetStandardExceptionReporter = new DotNetStandardExceptionReporter(dotNetStandardClientSettings, dotNetStandardClient);
            var windowsReporter = new WindowsReporter(dotNetStandardClientSettings, dotNetStandardExceptionReporter, dotNetStandardClient);

            clientSettings = dotNetStandardClientSettings;
            exceptionReporter = windowsReporter;
            nativeCrashReporter = windowsReporter;
#elif UNITY_WEBGL
            // TODO BG is instantiating a game object like this safe?
            var gameObject = new GameObject();
            var webGLClientSettings = new WebGLClientSettingsRepository();
            var webGLExceptionClient = new WebGLExceptionClient(database, application, version);
            var webGLReporter = WebGLReporter.Create(
                webGLClientSettings,
                webGLExceptionClient,
                gameObject
            );
            clientSettings = webGLClientSettings;
            exceptionReporter = webGLReporter;
#else
            var bugsplat = new BugSplatDotNetStandard.BugSplat(database, application, version);
            bugsplat.MinidumpType = BugSplatDotNetStandard.BugSplat.MinidumpTypeId.UnityNativeWindows;
            bugsplat.ExceptionType = BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity;
            var dotNetStandardClientSettings = new DotNetStandardClientSettingsRepository(bugsplat);
            var dotNetStandardClient = new DotNetStandardClient(bugsplat);
            var dotNetStandardExceptionReporter = new DotNetStandardExceptionReporter(dotNetStandardClientSettings, dotNetStandardClient);

            clientSettings = dotNetStandardClientSettings;
            exceptionReporter = dotNetStandardExceptionReporter;
#endif
        }

        /// <summary>
        /// Constructs a BugSplat object from ConfigurationOptions
        /// </summary>
        /// <param name="options">collection of options which can be used to configure a BugSplat object </param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        public static BugSplat CreateFromOptions(BugSplatOptions options, string application, string version)
        {
            var bugSplat = new BugSplat(options.Database, application, version);

            bugSplat.Email = options?.Email;
            bugSplat.Key = options?.Key;
            bugSplat.User = options?.User;
            bugSplat.CaptureEditorLog = options.CaptureEditorLog;
            bugSplat.CapturePlayerLog = options.CapturePlayerLog;
            bugSplat.CaptureScreenshots = options.CaptureScreenshots;

            if (options.PersistentDataFileAttachmentPaths != null)
			{
                var paths = options.PersistentDataFileAttachmentPaths
                    .Select(fileAttachment => Path.Combine(Application.persistentDataPath, fileAttachment))
                    .ToList();

                foreach (var filePath in options.PersistentDataFileAttachmentPaths)
                {
                    var fileInfo = new FileInfo(filePath);
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
        public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action callback = null)
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
    }
}