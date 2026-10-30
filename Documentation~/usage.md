[&larr; BugSplat for Unity](../README.md)

# ⌨️ Usage

BugSplat initializes itself from the options asset selected in **Edit > Project Settings > BugSplat** and configures an `Application.logMessageReceived` handler that posts reports when it encounters a log message of type `Exception`. You can also extend your BugSplat integration and [customize report metadata](#adding-metadata), [report exceptions in try/catch blocks](#trycatch-reporting), [prevent repeated reports](#preventing-repeated-reports), and [upload windows minidumps](windows.md) from native crashes.

## Adding Metadata

Your instance of `BugSplat` is `BugSplat.Instance`. It is set before the first scene loads, so it is safe to read in any `Awake`:

```cs
var bugsplat = BugSplat.Instance;
```

You can extend `BugSplat` by setting the following properties:

```cs
bugsplat.Attachments.Add(new FileInfo("/path/to/attachment.txt"));
bugsplat.Description = "description!";
bugsplat.Email = "fred@bugsplat.com";
bugsplat.Key = "key!";
bugsplat.Notes = "notes!";
bugsplat.User = "Fred";
bugsplat.CaptureEditorLog = true;
bugsplat.CapturePlayerLog = false;
bugsplat.CaptureScreenshots = true;
```

You can use the `Notes` field to capture arbitrary data such as system information:

```cs
void Start()
{
    bugsplat = BugSplat.Instance;
    bugsplat.Notes = GetSystemInfo();
}

private string GetSystemInfo()
{
    var info = new Dictionary<string, string>();
    info.Add("OS", SystemInfo.operatingSystem);
    info.Add("CPU", SystemInfo.processorType);
    info.Add("MEMORY", $"{SystemInfo.systemMemorySize} MB");
    info.Add("GPU", SystemInfo.graphicsDeviceName);
    info.Add("GPU MEMORY", $"{SystemInfo.graphicsMemorySize} MB");

    var sections = info.Select(section => $"{section.Key}: {section.Value}");
    return string.Join(Environment.NewLine, sections);
}
```

## Try/Catch Reporting

Exceptions can be sent to BugSplat in a try/catch block by calling `Post`.

```cs
try
{
    throw new Exception("BugSplat rocks!");
}
catch (Exception ex)
{
    StartCoroutine(bugsplat.Post(ex));
}
```

The default values specified on the instance of `BugSplat` can be overridden in the call to `Post`. Additionally, you can provide a `callback` to `Post` that will be invoked with the result once the upload is complete.

```cs
var options = new ReportPostOptions()
{
    Description = "a new description",
    Email = "barney@bugsplat.com",
    Key = "a new key!",
    Notes = "some new notes!",
    User = "Barney"
};

options.AdditionalAttachments.Add(new FileInfo("/path/to/additional.txt"));

static void callback()
{
    Debug.Log($"Exception post callback!");
};

StartCoroutine(bugsplat.Post(ex, options, callback));
```

## Preventing Repeated Reports

By default, BugSplat prevents reports from being sent at a rate greater than 1 per every 3 seconds. You can override the default crash report throttling implementation by setting `ShouldPostException` on your BugSplat instance. To override `ShouldPostException`, assign the property a new `Func<Exception, bool>` value. Be sure your new implementation can handle a null value for `Exception`!

The following example demonstrates how you could implement your own time-based report throttling mechanism:

```cs
var lastPost = new DateTime(0);

bugsplat.ShouldPostException = (ex) =>
{
    var now = DateTime.Now;

    if (now - lastPost < TimeSpan.FromSeconds(3))
    {
        Debug.LogWarning("ShouldPostException returns false. Skipping BugSplat report...");
        return false;
    }

    Debug.Log("ShouldPostException returns true. Posting BugSplat report...");
    lastPost = now;

    return true;
};
```

## Background Thread Exceptions

Unity raises `Application.logMessageReceived` only for logs written on the main thread. An unhandled exception on a background thread is written to the player log but never reaches that callback, so BugSplat captures these through `Application.logMessageReceivedThreaded` instead, buffers them, and posts them from the main thread on the next frame.

This is on by default. Uncheck **Capture Exceptions On Background Threads** on your `BugSplatOptions` asset to restore the previous behavior of reporting only main-thread exceptions.

Because the threaded callback also fires for main-thread logs that `logMessageReceived` already delivered, BugSplat ignores anything raised on the main thread there — main-thread exceptions are reported exactly once either way.

At most 64 background exceptions are buffered at a time. A thread failing in a tight loop can produce them faster than they can be uploaded, so the excess is dropped and a single warning is logged rather than queueing unbounded work.

## Unobserved Task Exceptions

A `Task` that faults with nobody awaiting it never writes to the Unity log at all, so neither log callback sees it. BugSplat subscribes to `TaskScheduler.UnobservedTaskException` and posts these through the same main-thread queue as background thread exceptions. Each exception inside the `AggregateException` is reported separately, so unrelated failures land in separate buckets rather than one.

This is on by default. Uncheck **Capture Unobserved Task Exceptions** on your `BugSplatOptions` asset to disable it.

Two things are worth knowing about the timing. The runtime raises this event only when a garbage collection notices the faulted `Task`, so reports arrive well after the failure and a `Task` that is never collected is never reported. And BugSplat deliberately does **not** call `SetObserved()` on these — marking the exception observed would suppress whatever your project does with it next, and reporting a failure must not change whether that failure happens.

## Windows Crashes

BugSplat captures native Windows crashes via [BugSplat for Windows](https://docs.bugsplat.com/introduction/getting-started/integrations/desktop/cplusplus). See the [Windows](windows.md) section for setup details.

> [!IMPORTANT]
> `Utils.ForceCrash` goes through Unity's internal crash pipeline and will **not** be captured by native crash reporters on iOS, macOS, or Android. On those platforms, use a real native crash (such as a null pointer dereference in native code) to test crash reporting. The BugSplat sample app uses real native crashes to test native crash reporting.

## Support Response

BugSplat has the ability to display a support response to users who encounter a crash. You can show your users a generalized support response for all crashes, or a custom support response that corresponds to the type of crash that occurred. Defining a support response allows you to alert users that bug has been fixed in a new version, or that they need to update their graphics drivers.

Next, pass a callback to `bugsplat.Post`. In the callback handler add code to open the support response in the user's browser:

```cs
private string infoUrl = "";

public void CatchExceptionThenPostNewBugSplat()
{
    try
    {
        MethodThatThrows();
    }
    catch (Exception ex)
    {
        var options = new ReportPostOptions()
        {
            Description = "a new description"
        };

        StartCoroutine(bugsplat.Post(ex, options, ExceptionCallback));
    }
}

void ExceptionCallback(ExceptionReporterPostResult result)
{
    UnityEngine.Debug.Log($"Exception post callback result: {result.Message}");

    if (result.Response == null) {
        return;
    }

    UnityEngine.Debug.Log($"BugSplat Status: {result.Response.status}");
    UnityEngine.Debug.Log($"BugSplat Crash ID: {result.Response.crashId}");
    UnityEngine.Debug.Log($"BugSplat Support URL: {result.Response.infoUrl}");

    infoUrl = result.Response.infoUrl;
}

private void OpenUrl(string url)
{
    var escaped = url.Replace("?", "\\?").Replace("&", "\\&").Replace(" ", "%20").Replace("!", "\\!");

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    Process.Start(url);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    Process.Start("open", escaped);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    Process.Start("xdg-open", escaped);
#else
    UnityEngine.Debug.Log($"OpenUrl unsupported platform: {Application.platform}");
#endif
}
```

When an exception occurs, a page similar to the following will open in the user's browser on Windows, macOS, and Linux.

<img width="1086" alt="image" src="https://github.com/user-attachments/assets/3a3d6f82-e3bf-42bc-ae7f-582ba35cd499">

More information on support responses can be found [here](https://docs.bugsplat.com/introduction/production/setting-up-custom-support-responses).
