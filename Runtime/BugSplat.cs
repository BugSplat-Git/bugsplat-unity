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
        /// attachments (Windows, macOS, iOS, and Android), this also attaches or detaches it natively,
        /// leaving any file attached with AttachNativeLogFile in place. Android is a special case: Unity
        /// writes no Player.log there, so consoleLogPath is empty and there is nothing to attach.
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
        private readonly List<string> nativeAttachmentPaths = new List<string>();

        /// <summary>
        /// The Apple submission settings as constructed. Recorded on every platform, not just the
        /// Apple ones, so CreateFromOptions' mapping stays observable in the editor - the blocks
        /// that actually consume these are behind a platform #if and compile out there, which
        /// would otherwise leave the mapping untestable by the suite written to catch exactly a
        /// dropped argument.
        /// </summary>
        internal bool? AutoSubmitCrashReportSetting { get; }
        internal bool? AutoSubmitFatalHangReportSetting { get; }

        /// <summary>
        /// Paths resolved from PersistentDataFileAttachmentPaths and passed to the constructor for native
        /// registration, de-duplicated the way the native list is. Registration itself happens only when
        /// native crash reporting is enabled for the platform, and is compiled out in the editor entirely,
        /// so this is the only part of that wiring a PlayMode test can observe.
        /// </summary>
        internal IReadOnlyList<string> NativePersistentDataAttachmentPaths => nativePersistentDataAttachmentPaths.AsReadOnly();
        private readonly List<string> nativePersistentDataAttachmentPaths = new List<string>();
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
        /// <param name="nativeAttachments">Files to attach to native crash reports, registered before the native reporter starts. On macOS and iOS that is the only time that counts: a report uploads at the next launch and its attachments are gathered while the reporter starts, so a path attached after construction never reaches it. Behaves exactly like AttachNativeLogFile on every platform otherwise.</param>
        public BugSplat(
            string database,
            string application,
            string version,
            bool useNativeLibIos,
            bool useNativeLibAndroid,
            bool useNativeLibMac = false,
            bool useNativeLibWin = false,
            bool capturePlayerLog = true,
            bool? autoSubmitCrashReport = null,
            bool? autoSubmitFatalHangReport = null,
            float? hangDetectionThresholdSeconds = null,
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

            AutoSubmitCrashReportSetting = autoSubmitCrashReport;
            AutoSubmitFatalHangReportSetting = autoSubmitFatalHangReport;

            // Resolved once here so AttachNativeLogFile and DetachNativeLogFile never touch the Unity API,
            // which is main-thread only.
            consoleLogPath = NormalizeNativeAttachmentPath(Application.consoleLogPath);

            // Seeded before the native reporter starts. On Apple the reporter gathers a pending report's
            // attachments synchronously inside start, once, and persists them with the report, so a path
            // registered after construction returns has already missed the only moment it could matter.
            var seededNativeAttachments = SeedNativeAttachments(nativeAttachments);

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            // Applied before -start, which is where bugsplat-apple decides whether a pending
            // report from the previous session gets a dialog or goes straight up.
            // Sentinels differ by type: for the two flags -1 means "no preference" while 0 is an
            // explicit false; for the threshold, which has no meaningful zero, 0 means "no
            // preference". A caller who passes nothing keeps bugsplat-apple's own defaults, which
            // differ per platform and are not this class's to override. CreateFromOptions always
            // passes the platform's configured values.
            var autoSubmit = autoSubmitCrashReport.HasValue ? (autoSubmitCrashReport.Value ? 1 : 0) : -1;
            var autoSubmitHang = autoSubmitFatalHangReport.HasValue ? (autoSubmitFatalHangReport.Value ? 1 : 0) : -1;
            var hangThreshold = hangDetectionThresholdSeconds ?? 0f;

            // Both warnings below are gated on this. With native reporting off these settings
            // never reach bugsplat-apple at all, so warning about them would be noise about
            // something that has no effect either way.
#if UNITY_IOS
            var nativeReportingForThisPlatform = useNativeLibIos;
#else
            var nativeReportingForThisPlatform = useNativeLibMac;
#endif

            // A configured value of zero or less cannot be honoured - the bridges only apply a
            // positive threshold - so it silently becomes bugsplat-apple's own default. Say so,
            // rather than letting someone who typed 0 expecting the 0.1s floor wonder why hangs
            // are being declared at two seconds.
            if (nativeReportingForThisPlatform &&
                hangDetectionThresholdSeconds.HasValue && hangDetectionThresholdSeconds.Value <= 0f)
            {
                Debug.LogWarning(
                    "BugSplat: a hang detection threshold of " + hangDetectionThresholdSeconds.Value +
                    "s is not usable, so bugsplat-apple's own default applies instead. Set a positive " +
                    "value to override it.");
            }

            // Asking for a hang prompt only works if crashes are prompting too. Withholding the
            // hang's auto-submit flag routes it onto the normal submission path, and that path
            // then consults autoSubmitCrashReport - so leaving that on means the hang still
            // uploads without asking, which is the opposite of what was configured.
            if (nativeReportingForThisPlatform &&
                autoSubmitFatalHangReport == false && autoSubmitCrashReport == true)
            {
                Debug.LogWarning(
                    "BugSplat: the fatal hang report option is off while the crash report option is on, " +
                    "so fatal hangs will still upload without asking. Turn auto-submit off for crash " +
                    "reports on this platform as well to be prompted.");
            }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (useNativeLibWin)
            {
                if (BugSplat_Init(database, application, version) == 1)
                {
                    nativeCrashReportingEnabled = true;

                    // Windows captures attachments at crash time, so after init is early enough.
                    foreach (var path in seededNativeAttachments)
                    {
                        AddNativeAttachment(path);
                    }
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

                // Before start, for the reason in the constructor comment above. The bridge only records
                // the path and installs its delegate here, so this is safe ahead of start.
                foreach (var path in seededNativeAttachments)
                {
                    AddNativeAttachment(path);
                }

                _startBugSplat(database, application, version, logPath ?? "", autoSubmit, autoSubmitHang, hangThreshold);
                nativeCrashReportingEnabled = true;

                if (logPath != null && IndexOfNativeAttachment(logPath) < 0)
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

                // Before start, for the reason in the constructor comment above. The bridge only records
                // the path and installs its delegate here, so this is safe ahead of start.
                foreach (var path in seededNativeAttachments)
                {
                    AddNativeAttachment(path);
                }

                // Named even when not captured, so a later CapturePlayerLog = true still gets the
                // crashed session's log rather than the live one.
                _setNativePlayerLogPath(consoleLogPath ?? "");

                _startBugSplat(database, application, version, logPath ?? "", autoSubmit, autoSubmitHang, hangThreshold);
                nativeCrashReportingEnabled = true;

                if (logPath != null && IndexOfNativeAttachment(logPath) < 0)
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
                // Guarded so a missing or mismatched AAR costs native reporting only. Left to
                // propagate, the exception would abandon the constructor before UseDotNetHandler
                // below, and managed exception reporting would be lost along with it.
                try
                {
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                    using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplat");
                    javaClass.CallStatic("init", activity, database, application, version);
                    nativeCrashReportingEnabled = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"BugSplat error: could not start native Android crash reporting: {ex.Message}. Native crashes will not be reported; managed exception reporting continues. This needs the bugsplat-android AAR bundled with this package (1.4.0 or later).");
                }

                if (nativeCrashReportingEnabled)
                {
                    // Android captures attachments at crash time, so after init is early enough.
                    foreach (var path in seededNativeAttachments)
                    {
                        AddNativeAttachment(path);
                    }
                }
            }

            UseDotNetHandler(database, application, version, capturePlayerLog);
#else
            UseDotNetHandler(database, application, version, capturePlayerLog);
#endif
        }

        /// <summary>
        /// Normalizes and de-duplicates the constructor's native attachments into the tracked list, and
        /// returns them so each platform can hand them to its reporter at the right moment.
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

            // Resolved before construction so the files reach the native reporter before it starts. On
            // macOS and iOS a pending report's attachments are gathered during start and never again, so
            // anything registered after the constructor returns is absent from that report.
            var persistentDataAttachments = ResolvePersistentDataAttachments(options.PersistentDataFileAttachmentPaths);
            var nativeAttachments = new List<string>();

            foreach (var fileInfo in persistentDataAttachments)
            {
                nativeAttachments.Add(fileInfo.FullName);
            }

            var bugSplat = new BugSplat(
                options.Database,
                application,
                version,
                options.UseNativeCrashReportingForIos,
                options.UseNativeCrashReportingForAndroid,
                options.UseNativeCrashReportingForMac,
                options.UseNativeCrashReportingForWindows,
                options.CapturePlayerLog,
#if UNITY_IOS
                options.IosAutoSubmitCrashReport,
                options.IosAutoSubmitFatalHangReport,
                options.IosHangDetectionThresholdSeconds,
#elif UNITY_STANDALONE_OSX
                options.MacAutoSubmitCrashReport,
                options.MacAutoSubmitFatalHangReport,
                options.MacHangDetectionThresholdSeconds,
#else
                null,
                null,
                null,
#endif
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

            bugSplat.SetWindowsCrashDialogEnabled(options.WindowsShowCrashDialog);

            if (options.WindowsHangDetectionTimeoutMs > 0)
            {
                bugSplat.SetWindowsHangDetectionTimeout(options.WindowsHangDetectionTimeoutMs);
            }

            foreach (var fileInfo in persistentDataAttachments)
            {
                // Managed reports read Attachments; native reports were handed these paths through the
                // constructor, before the native reporter started (see nativeAttachments there).
                // nativePersistentDataAttachmentPaths records what was handed over, because native
                // registration is compiled out in the editor and this is what a PlayMode test can observe.
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
            CallAndroid("setAttribute", key, value);
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
            CallAndroid("setUser", user);
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
            CallAndroid("setEmail", email);
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
            CallAndroid("setNotes", notes);
#endif
        }

        /// <summary>
        /// Set the key on the native crash reporter. Every native platform uses its own SDK's key setter.
        /// </summary>
        public void SetNativeKey(string key)
        {
            if (!nativeCrashReportingEnabled) return;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _setNativeKey(key);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_SetKey(key);
#elif UNITY_ANDROID && !UNITY_EDITOR
            CallAndroid("setKey", key);
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
            CallAndroid("setAttribute", "BugSplatDescription", description);
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
        /// attached once. Supported on Windows, macOS, iOS, and Android. Safe to call from any thread.
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

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Calls a static method on com.bugsplat.android.BugSplat, logging instead of throwing when the
        /// bundled AAR lacks it. Every Android call after init goes through here so a mismatched AAR
        /// degrades to missing data on a report rather than an exception in the game.
        /// </summary>
        private static void CallAndroid(string method, params object[] args)
        {
            try
            {
                using var javaClass = new AndroidJavaClass("com.bugsplat.android.BugSplat");
                javaClass.CallStatic(method, args);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"BugSplat warning: com.bugsplat.android.BugSplat.{method} failed: {ex.Message}. This needs bugsplat-android 1.4.0 or later.");
            }
        }
#endif

        private void AddNativeAttachment(string path)
        {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _attachNativeLogFile(path);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_AddAttachment(path);
#elif UNITY_ANDROID && !UNITY_EDITOR
            // CreateFromOptions attaches at startup, so an AAR without this method would otherwise
            // take the game down on launch; CallAndroid turns that into a warning.
            CallAndroid("addAttachment", path);
#endif
        }

        private void RemoveNativeAttachment(string path)
        {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            _detachNativeLogFile(path);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            BugSplat_RemoveAttachment(path);
#elif UNITY_ANDROID && !UNITY_EDITOR
            CallAndroid("removeAttachment", path);
#endif
        }

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        // Both Apple bridges now export identical symbols with identical signatures.
        [DllImport("__Internal")]
        static extern void _startBugSplat(string database, string application, string version, string logFilePath, int autoSubmitCrashReport, int autoSubmitFatalHangReport, float hangDetectionThresholdSeconds);

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

#if UNITY_STANDALONE_OSX
        // macOS bridge only: the Player-prev.log substitution lives there.
        [DllImport("__Internal")]
        static extern void _setNativePlayerLogPath(string path);
#endif

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