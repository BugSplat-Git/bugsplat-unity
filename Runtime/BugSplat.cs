using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
#if (UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using System.Threading.Tasks;
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
        /// A dictionary of key values pairs to be added every time Post is called.
        /// On platforms with native crash reporting, attributes are automatically synced to the native crash reporter.
        /// </summary>
        public IDictionary<string, string> Attributes
        {
            get
            {
                return clientSettings.Attributes;
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
        /// Upload Player.log when Post is called. On platforms whose native crash reporter takes
        /// attachments (Windows, macOS, and iOS), this also attaches or detaches it natively, leaving
        /// any file attached with AttachNativeLogFile in place.
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
                SetNativePlayerLogAttachment(value);
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
        /// A guard that prevents Exceptions from being posted in rapid succession and must be able to handle null - defaults to 1 report every 3 seconds.
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
            get
            {
                return clientSettings.Description;
            }
            set
            {
                clientSettings.Description = value;
                SetNativeDescription(value);
            }
        }

        /// <summary>
        /// A default email that can be overridden by call to Post
        /// </summary>
        public string Email
        {
            get
            {
                return clientSettings.Email;
            }
            set
            {
                clientSettings.Email = value;
                SetNativeEmail(value);
            }
        }

        /// <summary>
        /// A default key that can be overridden by call to Post
        /// </summary>
        public string Key
        {
            get
            {
                return clientSettings.Key;
            }
            set
            {
                clientSettings.Key = value;
                SetNativeKey(value);
            }
        }

        /// <summary>
        /// BugSplat truncates log files to this size in MB. Default is 10 MB.
        /// </summary>
        public int LogFileMaxSizeMB
        {
            get
            {
                return clientSettings.LogFileMaxSizeMB;
            }
            set
            {
                clientSettings.LogFileMaxSizeMB = value;
            }
        }

        /// <summary>
        /// A general purpose field that can be overridden by call to Post.
        /// </summary>
        public string Notes
        {
            get
            {
                return clientSettings.Notes;
            }
            set
            {
                clientSettings.Notes = value;
                SetNativeNotes(value);
            }
        }

        /// <summary>
        /// A default user that can be overridden by call to Post
        /// </summary>
        public string User
        {
            get
            {
                return clientSettings.User;
            }
            set
            {
                clientSettings.User = value;
                SetNativeUser(value);
            }
        }

        private static readonly StringComparer nativeAttachmentPathComparer =
#if UNITY_STANDALONE_WIN
            StringComparer.OrdinalIgnoreCase;
#else
            StringComparer.Ordinal;
#endif

        private IClientSettingsRepository clientSettings;
        internal IExceptionReporter exceptionReporter;
        internal IDotNetStandardFeedbackClient feedbackClient;
        private INativeCrashReportClient nativeCrashReportClient;
        private bool nativeCrashReportingEnabled;
        private readonly List<string> nativeAttachmentPaths = new List<string>();

        /// <summary>
        /// The Apple submission settings as constructed. Recorded on every platform, not just the
        /// Apple ones, so CreateFromOptions' mapping stays observable in the editor - the blocks
        /// that actually consume these are behind a platform #if and compile out there, which
        /// would otherwise leave the mapping untestable by the suite written to catch exactly a
        /// dropped argument.
        /// </summary>
        internal bool AutoSubmitCrashReportSetting { get; }
        internal bool AutoSubmitFatalHangReportSetting { get; }
        private readonly string consoleLogPath;
        private bool windowsWerEnabled;

        /// <summary>
        /// True when BugSplat's Windows Error Reporting handler is registered for this process.
        /// Fail-fast terminations — stack buffer overrun (0xC0000409), heap corruption (0xC0000374),
        /// and __fastfail — bypass BugSplat's crash handler entirely and are reported only when this
        /// is true. Registration requires BugSplatWer.dll next to the game executable and a
        /// machine-wide registry value naming its full path. Always false in the editor and on
        /// non-Windows platforms.
        /// </summary>
        public bool WindowsWerEnabled => windowsWerEnabled;

        /// <summary>
        /// Post Exceptions and minidump files to BugSplat
        /// </summary>
        /// <param name="database">The BugSplat database for your organization</param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        /// <param name="useNativeLibIos">Whether to use the native library for crash reporting on iOS</param>
        /// <param name="useNativeLibAndroid">Whether to use the native library for crash reporting on Android</param>
        /// <param name="useNativeLibMac">Whether to use the native library for crash reporting on macOS (requires IL2CPP)</param>
        /// <param name="useNativeLibWin">Whether to use the native library for crash reporting on Windows (works with Mono and IL2CPP)</param>
        /// <param name="capturePlayerLog">Whether to upload Player.log with reports. Applied while the native crash reporter initializes so that native crash reports honor it too.</param>
        public BugSplat(
            string database,
            string application,
            string version,
            bool useNativeLibIos,
            bool useNativeLibAndroid,
            bool useNativeLibMac = false,
            bool useNativeLibWin = false,
            bool capturePlayerLog = true,
            bool autoSubmitCrashReport = false,
            bool autoSubmitFatalHangReport = true
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

            // Resolved once here so AttachNativeLogFile and DetachNativeLogFile never touch the Unity API,
            // which is main-thread only.
            AutoSubmitCrashReportSetting = autoSubmitCrashReport;
            AutoSubmitFatalHangReportSetting = autoSubmitFatalHangReport;

            consoleLogPath = NormalizeNativeAttachmentPath(Application.consoleLogPath);

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            // Applied before -start, which is where bugsplat-apple decides whether a pending
            // report from the previous session gets a dialog or goes straight up.
            var autoSubmit = autoSubmitCrashReport ? 1 : 0;
            var autoSubmitHang = autoSubmitFatalHangReport ? 1 : 0;

            // Asking for a hang prompt only works if crashes are prompting too. Withholding the
            // hang's auto-submit flag routes it onto the normal submission path, and that path
            // then consults autoSubmitCrashReport - so leaving that on means the hang still
            // uploads without asking, which is the opposite of what was configured.
            if (!autoSubmitFatalHangReport && autoSubmitCrashReport)
            {
                Debug.LogWarning(
                    "BugSplat: AutoSubmitFatalHangReport is off, but AutoSubmitCrashReport is on, " +
                    "so fatal hangs will still upload without asking. Turn AutoSubmitCrashReport " +
                    "off as well to be prompted.");
            }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (useNativeLibWin)
            {
                if (BugSplat_Init(database, application, version) == 1)
                {
                    nativeCrashReportingEnabled = true;
                    // Show the native crash dialog by default; CreateFromOptions
                    // overrides this from WindowsShowCrashDialog.
                    BugSplat_SetQuietMode(0);
                    BugSplat_SetHangDetectionTimeout(0);
                    // Hard-terminate after a crash report uploads; a standalone Windows
                    // player's CRT shutdown can hang on the SDK's default exit() path.
                    // 1 == BUGSPLAT_CRASH_TERMINATE.
                    BugSplat_SetCrashCompletionBehavior(1);
                    // Tag native crashes as UnityNative (BugSplat crash type 15) so the
                    // backend applies LineNumberMappings.json to symbolicate C# frames.
                    BugSplat_SetCrashType(15);

                    SetNativePlayerLogAttachment(capturePlayerLog);

                    BugSplat_PostAllCrashesAsync();
                    ReportWindowsWerStatus();
                }
                else
                {
                    Debug.LogError("BugSplat error: failed to initialize native Windows crash reporting");
                }
            }

            UseDotNetHandler(database, application, version, capturePlayerLog);
#elif UNITY_WEBGL
            var webGLClientSettings = new WebGLClientSettingsRepository();
            var webGLExceptionClient = new WebGLExceptionClient(database, application, version);
            var webGLReporter = new WebGLReporter(webGLClientSettings, webGLExceptionClient);
            clientSettings = webGLClientSettings;
            exceptionReporter = webGLReporter;
#elif UNITY_IOS && !UNITY_EDITOR
            if (useNativeLibIos)
            {
                // Same ordering constraint as macOS: the delegate is queried while start processes
                // crash reports left by the previous session, so the player log has to be tracked
                // before start rather than attached after it.
                var logPath = capturePlayerLog ? consoleLogPath : null;
                _startBugSplat(database, application, version, logPath ?? "", autoSubmit, autoSubmitHang);
                nativeCrashReportingEnabled = true;

                if (logPath != null)
                {
                    // Uncontended - nothing else can reach this instance yet - but taken anyway so
                    // "every mutation of nativeAttachmentPaths happens under its lock" holds without
                    // exception. An invariant with two documented escapes is one nobody can audit.
                    lock (nativeAttachmentPaths)
                    {
                        nativeAttachmentPaths.Add(logPath);
                    }
                }
            }

            UseDotNetHandler(database, application, version, capturePlayerLog);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (useNativeLibMac)
            {
                // The delegate is queried while start processes crash reports left by the previous
                // session, so the player log has to be tracked before start rather than attached after it.
                var logPath = capturePlayerLog ? consoleLogPath : null;
                _startBugSplat(database, application, version, logPath ?? "", autoSubmit, autoSubmitHang);
                nativeCrashReportingEnabled = true;

                if (logPath != null)
                {
                    // Uncontended - nothing else can reach this instance yet - but taken anyway so
                    // "every mutation of nativeAttachmentPaths happens under its lock" holds without
                    // exception. An invariant with two documented escapes is one nobody can audit.
                    lock (nativeAttachmentPaths)
                    {
                        nativeAttachmentPaths.Add(logPath);
                    }
                }
            }

            UseDotNetHandler(database, application, version, capturePlayerLog);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (useNativeLibAndroid)
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                
                using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
                javaClass.CallStatic("initBugSplat", activity, database, application, version);
                nativeCrashReportingEnabled = true;
            }

            UseDotNetHandler(database, application, version, capturePlayerLog);
#else
            UseDotNetHandler(database, application, version, capturePlayerLog);
#endif
        }

        private void UseDotNetHandler(string database, string application, string version, bool capturePlayerLog)
        {
            var bugsplat = new BugSplatDotNetStandard.BugSplat(database, application, version)
            {
                MinidumpType = BugSplatDotNetStandard.BugSplat.MinidumpTypeId.UnityNativeWindows,
                ExceptionType = BugSplatDotNetStandard.BugSplat.ExceptionTypeId.Unity
            };
            var dotNetStandardClientSettings = new DotNetStandardClientSettingsRepository(bugsplat)
            {
                CapturePlayerLog = capturePlayerLog
            };
            var dotNetStandardClient = new DotNetStandardClient(bugsplat);
            var dotNetStandardExceptionReporter = new DotNetStandardExceptionReporter(dotNetStandardClientSettings, dotNetStandardClient);

            clientSettings = dotNetStandardClientSettings;
            exceptionReporter = dotNetStandardExceptionReporter;
            feedbackClient = dotNetStandardClient;
            nativeCrashReportClient = dotNetStandardClient;

            if (clientSettings.Attributes is NativeSyncDictionary<string, string> syncDict)
            {
                syncDict.SetCallback((key, value) => SetNativeAttribute(key, value));
            }
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
                options.UseNativeCrashReportingForAndroid,
                options.UseNativeCrashReportingForMac,
                options.UseNativeCrashReportingForWindows,
                options.CapturePlayerLog,
                options.AutoSubmitCrashReport,
                options.AutoSubmitFatalHangReport
            )
            {
                Description = options.Description,
                Email = options.Email,
                Key = options.Key,
                Notes = options.Notes,
                User = options.User,
                CaptureEditorLog = options.CaptureEditorLog,
                CapturePlayerLog = options.CapturePlayerLog,
                CaptureScreenshots = options.CaptureScreenshots,
                LogFileMaxSizeMB = options.LogFileMaxSizeMB,
                PostExceptionsInEditor = options.PostExceptionsInEditor
            };

            if (options.Attributes != null)
            {
                foreach (var attribute in options.Attributes)
                {
                    if (attribute == null || string.IsNullOrEmpty(attribute.Name))
                    {
                        continue;
                    }

                    bugSplat.Attributes[attribute.Name] = attribute.Value ?? string.Empty;
                }
            }

            bugSplat.SetWindowsCrashDialogEnabled(options.WindowsShowCrashDialog);

            if (options.WindowsHangDetectionTimeoutMs > 0)
            {
                bugSplat.SetWindowsHangDetectionTimeout(options.WindowsHangDetectionTimeoutMs);
            }

            if (options.PersistentDataFileAttachmentPaths != null)
			{
                foreach (var filePath in options.PersistentDataFileAttachmentPaths)
                {
                    var trimmedFilePath = filePath.TrimStart('/', '\\');
                    var fullFilePath = Path.Combine(Application.persistentDataPath, trimmedFilePath); 
                    var fileInfo = new FileInfo(fullFilePath);
                    var sizeLimit = 100 * 1024 * 1024; // 100 MB
                    if (!fileInfo.Exists)
                    {
                        Debug.LogWarning($"Persistent data file attachment does not exist at {fileInfo.FullName}, skipping...");
                        continue;
                    }
                    if (fileInfo.Length > sizeLimit)
                    {
                        Debug.LogWarning($"Persistent data file attachment {fileInfo.FullName} size limit exceeded. Limit is {sizeLimit}, size was {fileInfo.Length}. Skipping...");
                        continue;
                    }

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
        public IEnumerator LogMessageReceived(string logMessage, string stackTrace, LogType type)
        {
            yield return exceptionReporter.LogMessageReceived(logMessage, stackTrace, type);
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
        /// Post user feedback to BugSplat
        /// </summary>
        /// <param name="title">The feedback title, used as the stack key for grouping</param>
        /// <param name="description">Additional feedback context</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback invoked with the result</param>
        public IEnumerator PostFeedback(string title, string description = "", IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            if (feedbackClient == null)
            {
                Debug.LogError("BugSplat error: PostFeedback is not supported on this platform");
                callback?.Invoke(null);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                Debug.LogError("BugSplat error: PostFeedback title must not be null, empty, or whitespace");
                callback?.Invoke(null);
                yield break;
            }

            var task = feedbackClient.PostFeedback(title, description, options);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Debug.LogError($"BugSplat error posting feedback: {task.Exception?.GetBaseException()}");
                callback?.Invoke(null);
            }
            else if (task.IsCanceled)
            {
                Debug.LogError("BugSplat error: PostFeedback task was canceled");
                callback?.Invoke(null);
            }
            else
            {
                callback?.Invoke(task.Result);
            }
        }

        /// <summary>
        /// Post a minidump file to BugSplat
        /// </summary>
        /// <param name="minidump">The minidump file to post</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after the minidump is posted to BugSplat</param>
        public IEnumerator Post(FileInfo minidump, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            if (nativeCrashReportClient == null)
            {
                Debug.Log($"BugSplat info: Post is not implemented on this platform");
                yield return null;
                yield break;
            }

            options = options ?? new ReportPostOptions();
            options.SetNullOrEmptyValues(clientSettings);

            var task = nativeCrashReportClient.Post(minidump, options);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Debug.LogError($"BugSplat error: {task.Exception?.GetBaseException()}");
                callback?.Invoke(null);
            }
            else if (task.IsCanceled)
            {
                Debug.LogError("BugSplat error: Post task was canceled");
                callback?.Invoke(null);
            }
            else
            {
                callback?.Invoke(task.Result);
            }
        }
        /// <summary>
        /// Set a key-value attribute on the native crash reporter. Attributes are included in native crash reports.
        /// </summary>
        public void SetNativeAttribute(string key, string value)
        {
            if (!nativeCrashReportingEnabled) return;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _setNativeAttribute(key, value);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetAttribute(key, value);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
            javaClass.CallStatic("setAttribute", key, value);
#endif
        }

        /// <summary>
        /// Set the user name on the native crash reporter.
        /// </summary>
        public void SetNativeUser(string user)
        {
            if (!nativeCrashReportingEnabled) return;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _setNativeUser(user);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetUser(user);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
            javaClass.CallStatic("setAttribute", "BugSplatUser", user);
#endif
        }

        /// <summary>
        /// Set the user email on the native crash reporter.
        /// </summary>
        public void SetNativeEmail(string email)
        {
            if (!nativeCrashReportingEnabled) return;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _setNativeEmail(email);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetEmail(email);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
            javaClass.CallStatic("setAttribute", "BugSplatEmail", email);
#endif
        }

        /// <summary>
        /// Set notes on the native crash reporter.
        /// </summary>
        public void SetNativeNotes(string notes)
        {
            if (!nativeCrashReportingEnabled) return;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _setNativeNotes(notes);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetNotes(notes);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
            javaClass.CallStatic("setAttribute", "BugSplatNotes", notes);
#endif
        }

        /// <summary>
        /// Set the key on the native crash reporter. iOS, macOS, and Windows use the platform SDK's own
        /// key setter; Android's bridge has none, so there the key travels as the reserved
        /// BugSplatApplicationKey attribute that the backend promotes to the report's key.
        /// </summary>
        public void SetNativeKey(string key)
        {
            if (!nativeCrashReportingEnabled) return;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _setNativeKey(key);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetKey(key);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
            javaClass.CallStatic("setAttribute", "BugSplatApplicationKey", key);
#endif
        }

        /// <summary>
        /// Set the description on the native crash reporter.
        /// </summary>
        public void SetNativeDescription(string description)
        {
            if (!nativeCrashReportingEnabled) return;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _setNativeAttribute("BugSplatDescription", description);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetUserDescription(description);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplatBridge");
            javaClass.CallStatic("setAttribute", "BugSplatDescription", description);
#endif
        }

        /// <summary>
        /// Show or hide the BugSplat crash dialog when a native crash occurs on Windows.
        /// Defaults to shown. Windows only; no-op on other platforms.
        /// </summary>
        public void SetWindowsCrashDialogEnabled(bool show)
        {
            if (!nativeCrashReportingEnabled) return;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetQuietMode(show ? 0 : 1);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void ReportWindowsWerStatus()
        {
            try
            {
                windowsWerEnabled = BugSplat_IsWerEnabled() == 1;
            }
            catch (EntryPointNotFoundException)
            {
                // BugSplat.dll predates the BugSplat_IsWerEnabled export (added in 8.1.0).
                windowsWerEnabled = false;
            }

            if (windowsWerEnabled) return;

            var werDll = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                "BugSplatWer.dll");

            var message =
                "BugSplat: Windows Error Reporting is not armed, so fail-fast crashes — stack buffer " +
                "overrun (0xC0000409), heap corruption (0xC0000374), and __fastfail — will not be " +
                $"reported. They bypass BugSplat's crash handler entirely. To arm it, \"{werDll}\" must " +
                "exist and be named by a REG_DWORD value under HKLM\\SOFTWARE\\Microsoft\\Windows\\" +
                "Windows Error Reporting\\RuntimeExceptionHelperModules, which requires administrator " +
                "rights. Your installer should add that value and remove it on uninstall; for local " +
                "builds use BugSplat > Windows > Register WER Handler in the editor. All other crashes " +
                "are reported normally.";

            // The registry value is absent on virtually every end-user machine unless the installer
            // wrote it, and a player can do nothing about it — so only nag in development builds.
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }
#endif

        /// <summary>
        /// Set the native hang detection timeout in milliseconds on Windows. 0 disables hang detection (default).
        /// When a hang is detected, BugSplat uploads a hang report and terminates the process, so choose a
        /// timeout longer than your longest expected frame — long frames such as loading screens are otherwise
        /// falsely reported as hangs. Windows only; no-op on other platforms.
        /// </summary>
        public void SetWindowsHangDetectionTimeout(int ms)
        {
            if (!nativeCrashReportingEnabled) return;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetHangDetectionTimeout(ms);
#endif
        }

        /// <summary>
        /// Attach a log file to native crash reports. The file is read and included when a crash is uploaded.
        /// Attaching is additive and idempotent: a path that is already attached is ignored, and attaching a
        /// file never displaces one attached earlier — including the Player.log that CapturePlayerLog manages.
        /// Paths are resolved to full paths before they are compared, so the same file named two ways is
        /// attached once. Supported on Windows, macOS, and iOS; a no-op on Android, whose bridge has no
        /// attachment API. Safe to call from any thread.
        /// </summary>
        public void AttachNativeLogFile(string path)
        {
            if (!nativeCrashReportingEnabled) return;

            var fullPath = NormalizeNativeAttachmentPath(path);
            if (fullPath == null) return;

            lock (nativeAttachmentPaths)
            {
                if (IndexOfNativeAttachment(fullPath) >= 0) return;

                nativeAttachmentPaths.Add(fullPath);
                AddNativeAttachment(fullPath);
            }
        }

        /// <summary>
        /// Detach a log file previously attached with AttachNativeLogFile. Every other attachment is left
        /// in place. Detaching a file that is not attached does nothing. Safe to call from any thread.
        /// </summary>
        public void DetachNativeLogFile(string path)
        {
            if (!nativeCrashReportingEnabled) return;

            var fullPath = NormalizeNativeAttachmentPath(path);
            if (fullPath == null) return;

            lock (nativeAttachmentPaths)
            {
                var index = IndexOfNativeAttachment(fullPath);
                if (index < 0) return;

                // The native layer matches on the exact string it was given, which can differ in case
                // from the path this caller supplied.
                var attachedPath = nativeAttachmentPaths[index];
                nativeAttachmentPaths.RemoveAt(index);
                RemoveNativeAttachment(attachedPath);
            }
        }

        private void SetNativePlayerLogAttachment(bool attach)
        {
            if (attach)
            {
                AttachNativeLogFile(consoleLogPath);
            }
            else
            {
                DetachNativeLogFile(consoleLogPath);
            }
        }

        private int IndexOfNativeAttachment(string fullPath)
        {
            for (var i = 0; i < nativeAttachmentPaths.Count; i++)
            {
                if (nativeAttachmentPathComparer.Equals(nativeAttachmentPaths[i], fullPath))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeNativeAttachmentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"BugSplat warning: could not resolve native attachment path \"{path}\": {ex.Message}");
                return null;
            }
        }

        private void AddNativeAttachment(string path)
        {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _attachNativeLogFile(path);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_AddAttachment(path);
#endif
        }

        private void RemoveNativeAttachment(string path)
        {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _detachNativeLogFile(path);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_RemoveAttachment(path);
#endif
        }

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        // Both Apple bridges now export identical symbols with identical signatures.
        [DllImport("__Internal")]
        static extern void _startBugSplat(string database, string application, string version, string logFilePath, int autoSubmitCrashReport, int autoSubmitFatalHangReport);

        [DllImport("__Internal")]
        static extern void _setNativeAttribute(string key, string value);

        [DllImport("__Internal")]
        static extern void _setNativeUser(string user);

        [DllImport("__Internal")]
        static extern void _setNativeEmail(string email);

        [DllImport("__Internal")]
        static extern void _setNativeNotes(string notes);

        [DllImport("__Internal")]
        static extern void _setNativeKey(string key);

        [DllImport("__Internal")]
        static extern void _attachNativeLogFile(string path);

        [DllImport("__Internal")]
        static extern void _detachNativeLogFile(string path);

#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const string BugSplatDll = "BugSplat";

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern int BugSplat_Init(string database, string application, string version);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetKey(string key);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetUser(string user);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetEmail(string email);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetUserDescription(string description);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetNotes(string notes);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetAttribute(string key, string value);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern int BugSplat_AddAttachment(string path);

        [DllImport(BugSplatDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern int BugSplat_RemoveAttachment(string path);

        [DllImport(BugSplatDll, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetQuietMode(int quiet);

        [DllImport(BugSplatDll, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetHangDetectionTimeout(int ms);

        [DllImport(BugSplatDll, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetCrashCompletionBehavior(int behavior);

        [DllImport(BugSplatDll, CallingConvention = CallingConvention.Cdecl)]
        static extern void BugSplat_SetCrashType(int crashTypeId);

        [DllImport(BugSplatDll, CallingConvention = CallingConvention.Cdecl)]
        static extern int BugSplat_PostAllCrashesAsync();

        [DllImport(BugSplatDll, CallingConvention = CallingConvention.Cdecl)]
        static extern int BugSplat_IsWerEnabled();
#endif
    }
}