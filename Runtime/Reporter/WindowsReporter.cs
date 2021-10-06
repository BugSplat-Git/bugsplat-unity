
#if UNITY_STANDALONE_WIN || UNITY_WSA
using BugSplatDotNetStandard;
using BugSplatUnity.Runtime.Client;
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
        private static readonly string sentinelFileName = "BugSplatPostSuccess.txt";

        private readonly IExceptionReporter _exceptionReporter;
        private readonly INativeCrashReportClient _nativeCrashReporter;

        public WindowsReporter(IExceptionReporter exceptionReporter, INativeCrashReportClient nativeCrashReporter)
        {
            _exceptionReporter = exceptionReporter;
            _nativeCrashReporter = nativeCrashReporter;
        }

        public void LogMessageReceived(string logMessage, string stackTrace, LogType type, Action callback = null)
        {
           _exceptionReporter.LogMessageReceived(logMessage, stackTrace, type, callback);
        }

        public IEnumerator Post(Exception exception, IReportPostOptions options = null, Action callback = null)
        {
            return _exceptionReporter.Post(exception, options, callback);
        }

        public IEnumerator PostAllCrashes(MinidumpPostOptions options = null, Action<List<HttpResponseMessage>> callback = null)
        {
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
        }

        public IEnumerator PostCrash(DirectoryInfo crashFolder, MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
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
        }

        public IEnumerator PostMostRecentCrash(MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            var folder = new DirectoryInfo(CrashReporting.crashReportFolder);
            var crashFolder = folder.GetDirectories()
                .OrderBy(dir => dir.LastWriteTime)
                .FirstOrDefault();

            yield return PostCrash(crashFolder, options, callback);
        }

        public IEnumerator Post(FileInfo minidump, MinidumpPostOptions options = null, Action<HttpResponseMessage> callback = null)
        {
            // TODO BG set options defaults!
            options ??= new MinidumpPostOptions();

            yield return Task.Run(
                async () =>
                {
                    try
                    {
                        var result = await _nativeCrashReporter.Post(minidump, options);
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