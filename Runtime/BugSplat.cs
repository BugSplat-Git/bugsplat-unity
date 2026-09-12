using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Native;
using BugSplatUnity.Runtime.Reporter;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("BugSplat.Unity.RuntimeTests")]
namespace BugSplatUnity
{
    /// <summary>
    /// BugSplat crash and exception reporting for Unity.
    ///
    /// Two layers over one native SDK. Native crashes, hangs and non-fatal captures are handled by
    /// bugsplat-native: an out-of-process BugSplatMonitor writes the dump on Windows, macOS, Linux
    /// and Android (an in-process handler does on iOS), BugSplatReporter shows the dialog on
    /// desktop, and every report uploads through BugSplat's presigned-URL flow. Managed exceptions
    /// are captured by the .NET handler and, in a player, posted through the same native SDK as
    /// structured reports; in the editor and on WebGL they post directly over HTTP.
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
        /// Attributes are synced to the native crash reporter as they change, so the value at the
        /// instant of a crash is what the crash report carries.
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
        /// Upload Player.log when Post is called. With native crash reporting running this also
        /// attaches or detaches it from native crash reports, so the two stay in agreement.
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
        /// A guard that prevents Exceptions from being posted in rapid succession and must be able to handle null - defaults to 1 crash every 3 seconds.
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

        /// <summary>
        /// The OS and hardware the game is running on, as a first-class report property next to
        /// User and Email - "Windows 11 (10.0.26200) x64", "Android 14 (API 34) arm64-v8a; Google
        /// Pixel 8". Detected by the native SDK; set it to override, or set null to restore
        /// detection. Empty when native crash reporting is not running.
        /// </summary>
        public string Environment
        {
            get
            {
                return environmentOverride ?? NativeCrashReporter.GetEnvironment() ?? string.Empty;
            }
            set
            {
                environmentOverride = value;
                NativeCrashReporter.SetEnvironment(value);
            }
        }

        private static readonly StringComparer nativeAttachmentPathComparer =
#if UNITY_STANDALONE_WIN
            StringComparer.OrdinalIgnoreCase;
#else
            StringComparer.Ordinal;
#endif

        // The same case rule as nativeAttachmentPathComparer, for the APIs that take a comparison.
        private const StringComparison nativeAttachmentPathComparison =
#if UNITY_STANDALONE_WIN
            StringComparison.OrdinalIgnoreCase;
#else
            StringComparison.Ordinal;
#endif

        private IClientSettingsRepository clientSettings;
        internal IExceptionReporter exceptionReporter;
        internal IDotNetStandardFeedbackClient feedbackClient;
        private INativeCrashReportClient nativeCrashReportClient;
        private bool nativeCrashReportingEnabled;
        private string environmentOverride;
        private readonly List<string> nativeAttachmentPaths = new List<string>();

        /// <summary>
        /// The native settings this instance was constructed with. Read-only in intent: the native
        /// SDK reads them once at startup, so changing a field afterwards has no effect.
        /// </summary>
        public NativeSettings NativeSettings { get; }

        /// <summary>
        /// Paths resolved from PersistentDataFileAttachmentPaths and passed to the constructor for native
        /// registration, de-duplicated the way the native list is. Registration itself is a no-op in the
        /// editor, so this is the only part of that wiring a PlayMode test can observe.
        /// </summary>
        internal IReadOnlyList<string> NativePersistentDataAttachmentPaths => nativePersistentDataAttachmentPaths.AsReadOnly();
        private readonly List<string> nativePersistentDataAttachmentPaths = new List<string>();
        private readonly string consoleLogPath;
        private bool windowsWerEnabled;

        /// <summary>
        /// True when bugsplat-native started for this process: native crashes, hangs and captures
        /// are reported, and managed reports post through the native SDK. Always false in the
        /// editor and on WebGL; false in a player when the native runtime is missing next to it
        /// (the error is logged at construction).
        /// </summary>
        public bool NativeCrashReportingEnabled => nativeCrashReportingEnabled;

        /// <summary>The bugsplat-native version behind this instance, or empty when it is not running.</summary>
        public string NativeVersion => nativeCrashReportingEnabled ? NativeCrashReporter.Version : string.Empty;

        /// <summary>
        /// True when BugSplat's Windows Error Reporting handler is registered for this process.
        /// Fail-fast terminations — stack buffer overrun (0xC0000409), heap corruption (0xC0000374),
        /// and __fastfail — bypass every in-process crash handler and are reported only when this
        /// is true. Registration requires BugSplatWer.dll next to the game executable and a
        /// machine-wide registry value naming its full path. Always false in the editor and on
        /// non-Windows platforms.
        /// </summary>
        public bool WindowsWerEnabled => windowsWerEnabled;

        /// <summary>
        /// Post Exceptions, native crashes and feedback to BugSplat
        /// </summary>
        /// <param name="database">The BugSplat database for your organization</param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        /// <param name="useNativeCrashReporting">Start bugsplat-native for native crash, hang and capture reporting. Has no effect in the editor or on WebGL.</param>
        /// <param name="capturePlayerLog">Whether to upload Player.log with reports, managed and native alike.</param>
        /// <param name="nativeSettings">Upload policy, dump type, hang detection and the other settings the native SDK reads at startup. Null uses the defaults.</param>
        /// <param name="nativeAttachments">Files to attach to native crash reports from the first moment the reporter runs. Behaves exactly like AttachNativeLogFile afterwards.</param>
        public BugSplat(
            string database,
            string application,
            string version,
            bool useNativeCrashReporting = true,
            bool capturePlayerLog = true,
            NativeSettings nativeSettings = null,
            IEnumerable<string> nativeAttachments = null
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

            NativeSettings = (nativeSettings ?? new NativeSettings()).Clone();

            // Resolved once here so AttachNativeLogFile and DetachNativeLogFile never touch the Unity API,
            // which is main-thread only. Empty on Android, which has no Player.log.
            consoleLogPath = NormalizeNativeAttachmentPath(Application.consoleLogPath);

            var startupAttachments = new List<string>(SeedNativeAttachments(nativeAttachments));

            if (capturePlayerLog && consoleLogPath != null && IndexOfNativeAttachment(consoleLogPath) < 0)
            {
                lock (nativeAttachmentPaths)
                {
                    nativeAttachmentPaths.Add(consoleLogPath);
                }
                startupAttachments.Add(consoleLogPath);
            }

            if (useNativeCrashReporting && NativeCrashReporter.IsSupportedPlatform)
            {
                // Attachments are handed over at init so a crash in the very first frame carries
                // them; everything registered later reaches the reporter through the same
                // per-session manifest, on every platform.
                nativeCrashReportingEnabled = NativeCrashReporter.Initialize(
                    database, application, version, NativeSettings,
                    null, null, null, null, null, null, startupAttachments);

                if (nativeCrashReportingEnabled)
                {
                    windowsWerEnabled = NativeCrashReporter.HasCapability(BugSplatNative.Capability.Wer);
                    ReportWindowsWerStatus();

                    // Reports an earlier session could not send (offline, MANUAL policy that was
                    // never drained) go out now, on a background thread.
                    NativeCrashReporter.PostPendingReportsAsync();
                }
                else
                {
                    // A packaging problem, not a runtime one: the player was built without the
                    // BugSplat runtime next to it. Managed exception reporting continues over HTTP.
                    Debug.LogError(
                        $"BugSplat error: native crash reporting could not start: {NativeCrashReporter.LastError} " +
                        "Native crashes will not be reported; managed exception reporting continues.");
                }
            }

            UseDotNetHandler(database, application, version, capturePlayerLog);
        }

        /// <summary>
        /// Normalizes and de-duplicates the constructor's native attachments into the tracked list, and
        /// returns them so they can be handed to the native reporter at startup.
        /// </summary>
        private string[] SeedNativeAttachments(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return Array.Empty<string>();
            }

            var seeded = new List<string>();

            lock (nativeAttachmentPaths)
            {
                foreach (var path in paths)
                {
                    var fullPath = NormalizeNativeAttachmentPath(path);

                    if (fullPath == null || IndexOfNativeAttachment(fullPath) >= 0)
                    {
                        continue;
                    }

                    nativeAttachmentPaths.Add(fullPath);
                    seeded.Add(fullPath);
                }
            }

            return seeded.ToArray();
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
            var httpClient = new DotNetStandardClient(bugsplat);

            IDotNetStandardExceptionClient exceptionClient = httpClient;
            IDotNetStandardFeedbackClient feedback = httpClient;
            INativeCrashReportClient minidumpClient = httpClient;

            if (nativeCrashReportingEnabled)
            {
                // One upload path per player: managed exceptions and feedback go through the native
                // SDK's store and uploader. Minidump files posted by hand keep the HTTP client.
                var nativeClient = new NativeReportClient(dotNetStandardClientSettings, httpClient, NativeSettings.ManagedReportFormat);
                exceptionClient = nativeClient;
                feedback = nativeClient;
                minidumpClient = nativeClient;
            }

            clientSettings = dotNetStandardClientSettings;
            exceptionReporter = new DotNetStandardExceptionReporter(dotNetStandardClientSettings, exceptionClient);
            feedbackClient = feedback;
            nativeCrashReportClient = minidumpClient;

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

            // Resolved before construction so the files reach the native reporter before it starts.
            var persistentDataAttachments = ResolvePersistentDataAttachments(options.PersistentDataFileAttachmentPaths);
            var nativeAttachments = new List<string>();

            foreach (var fileInfo in persistentDataAttachments)
            {
                nativeAttachments.Add(fileInfo.FullName);
            }

            var nativeSettings = new NativeSettings
            {
                UploadPolicy = options.UploadPolicy,
                DumpType = options.DumpType,
                HangDetectionTimeoutMs = options.HangDetectionTimeoutMs,
                HangPolicy = options.HangPolicy,
                OpenSupportUrl = options.OpenSupportUrl,
                ManagedReportFormat = options.ManagedReportFormat,
            };

            var bugSplat = new BugSplat(
                options.Database,
                application,
                version,
                options.UseNativeCrashReporting,
                options.CapturePlayerLog,
                nativeSettings,
                nativeAttachments
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

            foreach (var fileInfo in persistentDataAttachments)
            {
                // Managed reports read Attachments; native reports were handed these paths through the
                // constructor, before the native reporter started (see nativeAttachments there).
                // nativePersistentDataAttachmentPaths records what was handed over, because native
                // registration is a no-op in the editor and this is what a PlayMode test can observe.
                bugSplat.Attachments.Add(fileInfo);

                var alreadyRecorded = bugSplat.nativePersistentDataAttachmentPaths.FindIndex(
                    recorded => nativeAttachmentPathComparer.Equals(recorded, fileInfo.FullName)) >= 0;
                if (!alreadyRecorded)
                {
                    bugSplat.nativePersistentDataAttachmentPaths.Add(fileInfo.FullName);
                }
            }

            return bugSplat;
        }

        /// <summary>
        /// Resolves PersistentDataFileAttachmentPaths against persistentDataPath, dropping entries that are
        /// absolute, missing, or over the size limit, with a warning for each.
        /// </summary>
        private static List<FileInfo> ResolvePersistentDataAttachments(List<string> persistentDataFileAttachmentPaths)
        {
            var attachments = new List<FileInfo>();

            if (persistentDataFileAttachmentPaths != null)
            {
                foreach (var filePath in persistentDataFileAttachmentPaths)
                {
                    // An empty row in the Inspector list is not an attempt to attach anything.
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        continue;
                    }

                    // Entries are relative to Application.persistentDataPath. An absolute path is rejected rather
                    // than resolved: it belongs to the machine that authored the options asset, so it would not
                    // exist on a teammate's machine, in CI, or on a player's device, and the sandboxed platforms
                    // cannot read outside their own container at all.
                    if (Path.IsPathRooted(filePath))
                    {
                        Debug.LogWarning($"Persistent data file attachment \"{filePath}\" is not a relative path, skipping... Paths are relative to Application.persistentDataPath (\"{Application.persistentDataPath}\"), for example \"logs/session.log\".");
                        continue;
                    }

                    var fullFilePath = Path.Combine(Application.persistentDataPath, filePath);
                    var fileInfo = new FileInfo(fullFilePath);

                    // Path.Combine resolves "../outside.log" to a sibling of persistentDataPath, so a
                    // relative-looking entry can still name a file outside it. That is the same problem
                    // as a rooted entry - a path that exists on the authoring machine and nowhere else,
                    // and unreadable on the sandboxed platforms - so it is refused the same way.
                    var persistentDataRoot = new DirectoryInfo(Application.persistentDataPath).FullName
                        .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!fileInfo.FullName.StartsWith(persistentDataRoot, nativeAttachmentPathComparison))
                    {
                        Debug.LogWarning($"Persistent data file attachment \"{filePath}\" resolves to \"{fileInfo.FullName}\", outside Application.persistentDataPath (\"{Application.persistentDataPath}\"), skipping... Paths may not escape it, for example with \"..\".");
                        continue;
                    }

                    var sizeLimit = 100 * 1024 * 1024; // 100 MB
                    if (!fileInfo.Exists)
                    {
                        Debug.LogWarning($"Persistent data file attachment \"{filePath}\" does not exist at \"{fileInfo.FullName}\", skipping...");
                        continue;
                    }
                    if (fileInfo.Length > sizeLimit)
                    {
                        Debug.LogWarning($"Persistent data file attachment \"{filePath}\" (\"{fileInfo.FullName}\") size limit exceeded. Limit is {sizeLimit}, size was {fileInfo.Length}. Skipping...");
                        continue;
                    }

                    attachments.Add(fileInfo);
                }
            }

            return attachments;
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
        /// <param name="callback">Optional callback that will be invoked with the result after the exception is posted to BugSplat</param>
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
        /// Post a minidump file to BugSplat over HTTP. Native crashes captured by bugsplat-native
        /// upload themselves; this is for dumps produced elsewhere.
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
        /// Dump the live process out of process and report it like a crash, without crashing. The
        /// game keeps running. Use it to capture the state behind a condition you can detect but
        /// not explain. Returns false when native crash reporting is not running.
        /// </summary>
        public bool CaptureReport()
        {
            return nativeCrashReportingEnabled && NativeCrashReporter.CaptureReport();
        }

        /// <summary>
        /// Tell the hang detector the main thread is alive. BugSplatManager calls this every frame;
        /// call it yourself when you drive BugSplat without the manager and have hang detection on.
        /// </summary>
        public void Heartbeat()
        {
            if (nativeCrashReportingEnabled) NativeCrashReporter.Heartbeat();
        }

        /// <summary>
        /// Watch the calling thread for hangs in addition to the main thread; the thread then calls
        /// Heartbeat at least once per hang timeout. The name appears in the hang report.
        /// </summary>
        public bool WatchThread(string name)
        {
            return nativeCrashReportingEnabled && NativeCrashReporter.WatchThread(name);
        }

        public void UnwatchThread()
        {
            if (nativeCrashReportingEnabled) NativeCrashReporter.UnwatchThread();
        }

        /// <summary>
        /// Upload reports left on disk by earlier sessions (offline, or the MANUAL upload policy),
        /// on a background thread. Runs automatically at construction.
        /// </summary>
        public void PostPendingReportsAsync()
        {
            if (nativeCrashReportingEnabled) NativeCrashReporter.PostPendingReportsAsync();
        }

        /// <summary>
        /// Set a key-value attribute on the native crash reporter. Attributes are included in native crash reports.
        /// </summary>
        public void SetNativeAttribute(string key, string value)
        {
            if (!nativeCrashReportingEnabled) return;
            NativeCrashReporter.SetAttribute(key, value);
        }

        /// <summary>
        /// Set the user name on the native crash reporter.
        /// </summary>
        public void SetNativeUser(string user)
        {
            if (!nativeCrashReportingEnabled) return;
            NativeCrashReporter.SetUser(user);
        }

        /// <summary>
        /// Set the user email on the native crash reporter.
        /// </summary>
        public void SetNativeEmail(string email)
        {
            if (!nativeCrashReportingEnabled) return;
            NativeCrashReporter.SetEmail(email);
        }

        /// <summary>
        /// Set notes on the native crash reporter.
        /// </summary>
        public void SetNativeNotes(string notes)
        {
            if (!nativeCrashReportingEnabled) return;
            NativeCrashReporter.SetNotes(notes);
        }

        /// <summary>
        /// Set the key on the native crash reporter.
        /// </summary>
        public void SetNativeKey(string key)
        {
            if (!nativeCrashReportingEnabled) return;
            NativeCrashReporter.SetKey(key);
        }

        /// <summary>
        /// Set the description on the native crash reporter.
        /// </summary>
        public void SetNativeDescription(string description)
        {
            if (!nativeCrashReportingEnabled) return;
            NativeCrashReporter.SetDescription(description);
        }

        /// <summary>
        /// Show or hide the BugSplat crash dialog when a native crash occurs on desktop platforms.
        /// Defaults to the upload policy the instance was constructed with. No-op on mobile, where
        /// there is no dialog at crash time.
        /// </summary>
        public void SetCrashDialogEnabled(bool show)
        {
            if (!nativeCrashReportingEnabled) return;
            NativeCrashReporter.SetQuietMode(!show);
        }

        private void ReportWindowsWerStatus()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowsWerEnabled) return;

            var werDll = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                "BugSplatWer.dll");

            var message =
                "BugSplat: Windows Error Reporting is not armed, so fail-fast crashes — stack buffer " +
                "overrun (0xC0000409), heap corruption (0xC0000374), and __fastfail — will not be " +
                $"reported. They bypass every in-process crash handler. To arm it, \"{werDll}\" must " +
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
#endif
        }

        /// <summary>
        /// Attach a log file to native crash reports. The file is copied into the report by the
        /// monitor right after the dump, so it can be attached before anything has written to it,
        /// and attaching at any point in the session counts. Attaching is additive and idempotent: a
        /// path that is already attached is ignored, and attaching a file never displaces one attached
        /// earlier — including the Player.log that CapturePlayerLog manages. Paths are resolved to
        /// full paths before they are compared, so the same file named two ways is attached once.
        /// Safe to call from any thread.
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
                NativeCrashReporter.AddAttachment(fullPath);
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
                NativeCrashReporter.RemoveAttachment(attachedPath);
            }
        }

        private void SetNativePlayerLogAttachment(bool attach)
        {
            if (consoleLogPath == null) return;

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
    }
}
