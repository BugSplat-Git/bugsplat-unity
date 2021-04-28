using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BugSplatDotNetStandard;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.Windows;
using System.Linq;

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
    /// A guard that prevents Exceptions from being posted in rapid succession - defaults to 1 crash every 10 seconds.
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

    private BugSplatDotNetStandard.BugSplat bugsplat;
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
    }

    /// <summary>
    /// Post an Exception to BugSplat
    /// </summary>
    /// <param name="ex">The Exception that will be serialized and posted to BugSplat</param>
    /// <param name="options">Optional parameters that will override the defaults if provided</param>
    /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after exception is posted to BugSplat</param>
    public IEnumerator Post(Exception ex, BugSplatPostOptions options = null, Action<HttpResponseMessage> callback = null)
    {
        if (!ShouldPostException(ex))
        {
            yield break;
        }

        options = options ?? new BugSplatPostOptions();

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
#else
            Debug.Log($"BugSplat info: CaptureEditorLog is not implemented on this platform");
#endif
            // TODO BG mac
            // TODO BG linux
        }

        if (CapturePlayerLog)
        {
#if UNITY_STANDALONE_WIN
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
#else
            Debug.Log($"BugSplat info: CapturePlayerLog is not implemented on this platform");
#endif
            // TODO BG mac
            // TODO BG linux
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
                    var result = await bugsplat.Post(ex, options);
                    if (callback != null)
                    {
                        callback(result);
                    }
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
    public IEnumerator PostAllCrashes(BugSplatPostOptions options = null, Action<List<HttpResponseMessage>> callback = null)
    {
#if UNITY_STANDALONE_WIN
        options = options ?? new BugSplatPostOptions();

        var folder = new DirectoryInfo(CrashReporting.crashReportFolder);
        var crashFolders = folder.GetDirectories();
        var results = new List<HttpResponseMessage>();

        foreach (var crashFolder in crashFolders)
        {
            yield return new WaitForSeconds(1);
            yield return PostCrash(crashFolder, options, (response) => results.Add(response));
        }

        callback(results);
#else
        Debug.Log($"BugSplat info: PostAllCrashes is not implemented on this platform");
#endif
    }

    /// <summary>
    /// Post a specifc crash to BugSplat and will skip crashes that have already been posted 
    /// </summary>
    /// <param name="options">Optional parameters that will override the defaults if provided</param>
    /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after the crash is posted to BugSplat</param>
    public IEnumerator PostCrash(DirectoryInfo crashFolder, BugSplatPostOptions options = null, Action<HttpResponseMessage> callback = null)
    {
#if UNITY_STANDALONE_WIN
        options = options ?? new BugSplatPostOptions();

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
        var attachments = crashFiles.Where(file => file.Extension != ".dmp");
        options.AdditionalAttachments.AddRange(attachments);

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
            callback(response);
        });
#else
        Debug.Log($"BugSplat info: PostCrash is not implemented on this platform");
#endif
    }

    /// <summary>
    /// Post the most recent Player crash to BugSplat and will skip crashes that have already been posted 
    /// </summary>
    /// <param name="options">Optional parameters that will override the defaults if provided</param>
    /// <param name="callback">Optional callback that will be invoked with an HttpResponseMessage after the crash is posted to BugSplat</param>
    public IEnumerator PostMostRecentCrash(BugSplatPostOptions options = null, Action<HttpResponseMessage> callback = null)
    {
#if UNITY_STANDALONE_WIN
        options = options ?? new BugSplatPostOptions();

        var folder = new DirectoryInfo(CrashReporting.crashReportFolder);
        var crashFolder = folder.GetDirectories()
            .OrderBy(dir => dir.LastWriteTime)
            .FirstOrDefault();

        yield return PostCrash(crashFolder, options, callback);
#else
        Debug.Log($"BugSplat info: PostMostRecentCrash is not implemented on this platform");
#endif
    }

    private byte[] CaptureInMemoryPngScreenshot()
    {
        try
        {
            // TODO BG the example calls Destory on texture, do we need to do that?
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            return texture.EncodeToPNG();
        }
        catch (Exception ex)
        {
            Debug.LogError($"BugSplat error: Could not create a screenshot {ex.Message}");
            return null;
        }
    }

    private IEnumerator Post(FileInfo minidump, BugSplatPostOptions options = null, Action<HttpResponseMessage> callback = null)
    {
#if UNITY_STANDALONE_WIN
        yield return Task.Run(
            async () =>
            {
                try
                {
                    var result = await bugsplat.Post(minidump, options);
                    if (callback != null)
                    {
                        callback(result);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"BugSplat error: {ex}");
                }
            }
        );
#else
        Debug.Log($"BugSplat info: Post is not implemented on this platform");
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