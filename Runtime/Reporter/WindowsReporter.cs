
#if UNITY_STANDALONE_WIN || UNITY_WSA
using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Windows;

namespace BugSplatUnity.Runtime.Reporter
{
    internal class WindowsReporter: IExceptionReporter, INativeCrashReporter
    {
        public IDirectoryInfoFactory DirectoryInfoFactory { get; set; } = new DirectoryInfoFactory();
        public IFileContentsWriter FileContentsWriter { get; set; } = new FileContentsWriter();

        public static readonly string SentinelFileName = "BugSplatPostSuccess.txt";

        private readonly IClientSettingsRepository _clientSettings;
        private readonly IExceptionReporter _exceptionReporter;
        private readonly INativeCrashReportClient _nativeCrashReportClient;

        private readonly string noCrashFolderFoundString = "BugSplat info: unity crash folder {0} was not found";

        public WindowsReporter(
            IClientSettingsRepository clientSettings,
            IExceptionReporter exceptionReporter,
            INativeCrashReportClient nativeCrashReportClient
        )
        {
            _clientSettings = clientSettings;
            _exceptionReporter = exceptionReporter;
            _nativeCrashReportClient = nativeCrashReportClient;
        }

        public void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action<ExceptionReporterPostResult> callback = null)
        {
           _exceptionReporter.LogMessageReceived(logMessage, stackTrace, type, callback);
        }

        public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action<ExceptionReporterPostResult> callback = null)
        {
            return _exceptionReporter.Post(exception, options, callback);
        }

        public IEnumerator PostAllCrashes(IReportPostOptions options = null, Action<List<HttpResponseMessage>> callback = null)
        {
            var unityCrashesFolder = DirectoryInfoFactory.CreateDirectoryInfo(CrashReporting.crashReportFolder);
            if (!unityCrashesFolder.Exists)
            {
                Debug.Log(string.Format(noCrashFolderFoundString, CrashReporting.crashReportFolder));
                yield break;
            }

            var results = new List<HttpResponseMessage>();
            var crashFolders = unityCrashesFolder.GetDirectories();
            var crashFoldersCount = crashFolders.Count();

            for (var i = 0; i < crashFoldersCount; i++)
            {
                var crashFolder = crashFolders[i];

                yield return PostCrash(crashFolder, options, (response) => results.Add(response));
                
                if (crashFoldersCount > 1 && i < crashFoldersCount - 1)
                {
                    yield return new WaitForSeconds(1);
                }
            }

            callback?.Invoke(results);
        }

        public IEnumerator PostCrash(IDirectoryInfo crashFolder, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            options = options ?? new ReportPostOptions();

            if (!crashFolder.Exists)
            {
                Debug.LogError($"BugSplat error: folder {crashFolder.Name} was not found");
                yield break;
            }

            var crashFiles = crashFolder.GetFiles();
            if (crashFiles.Any(file => file.Name == SentinelFileName))
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
            if (attachments?.Count() > 0)
            {
                options.AdditionalAttachments.AddRange(attachments);
            }

            Debug.Log($"BugSplat info: Posting {crashFolder.Name}");
            yield return Post(minidump, options, async (response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Debug.Log("BugSplat info: Crash post success, writing crash post sentinel file...");
                    var sentinelFilePath = Path.Combine(crashFolder.FullName, SentinelFileName);
                    var sentinelFileContents = string.Empty;
                    if (response.Content != null)
                    {
                        sentinelFileContents = await response.Content.ReadAsStringAsync();
                    }
                    FileContentsWriter.WriteAllText(sentinelFilePath, sentinelFileContents);
                }
                callback?.Invoke(response);
            });
        }

        public IEnumerator PostMostRecentCrash(IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            var unityCrashesFolder = DirectoryInfoFactory.CreateDirectoryInfo(CrashReporting.crashReportFolder);
            if (!unityCrashesFolder.Exists)
            {
                Debug.Log(string.Format(noCrashFolderFoundString, CrashReporting.crashReportFolder));
                yield break;
            }

            var crashFolder = unityCrashesFolder.GetDirectories()
                .OrderByDescending(dir => dir.LastWriteTime)
                .FirstOrDefault();

            yield return PostCrash(crashFolder, options, callback);
        }

        public IEnumerator Post(FileInfo minidump, IReportPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            options = options ?? new ReportPostOptions();
            options.SetNullOrEmptyValues(_clientSettings);

            yield return Task.Run(
                async () =>
                {
                    try
                    {
                        var result = await _nativeCrashReportClient.Post(minidump, options);
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
#endif