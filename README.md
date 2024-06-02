[![bugsplat-github-banner-basic-outline](https://user-images.githubusercontent.com/20464226/149019306-3186103c-5315-4dad-a499-4fd1df408475.png)](https://bugsplat.com)
<br/>
# <div align="center">BugSplat</div> 
### **<div align="center">Crash and error reporting built for busy developers.</div>**
<div align="center">
    <a href="https://twitter.com/BugSplatCo">
        <img alt="Follow @bugsplatco on Twitter" src="https://img.shields.io/twitter/follow/bugsplatco?label=Follow%20BugSplat&style=social">
    </a>
    <a href="https://discord.gg/K4KjjRV5ve">
        <img alt="Join BugSplat on Discord" src="https://img.shields.io/discord/664965194799251487?label=Join%20Discord&logo=Discord&style=social">
    </a>
    <br/>
    <a href="https://openupm.com/packages/com.bugsplat.unity/">
        <img alt="BugSplatUnity on OpenUPM" src="https://img.shields.io/npm/v/com.bugsplat.unity?label=openupm&registry_uri=https://package.openupm.com">
    </a>
</div>

## 👋 Introduction

BugSplat's `com.bugsplat.unity` package provides crash and exception reporting for Unity projects. BugSplat provides you with invaluable insight into the issues tripping up your users. Our Unity integration collects screenshots, log files, exceptions, and Windows minidumps so that you can fix bugs and deliver a better user experience.

Before you proceed, please make sure you have completed the following checklist:
* [Sign Up](https://app.bugsplat.com/v2/sign-up) as a new BugSplat user
* [Log In](https://app.bugsplat.com/cognito/login) to your account

## 🏗 Installation

BugSplat's `com.bugsplat.unity` package can be added to your project via [OpenUPM](https://openupm.com/packages/com.bugsplat.unity/) or a URL to our git [repository](https://github.com/BugSplat-Git/bugsplat-unity.git).

### OpenUPM
Information on installing OpenUPM can be found [here](https://openupm.com). After installing OpenUPM, run the following command to add BugSplat to your project.

```sh
openupm add com.bugsplat.unity
```

### Git
Information on adding a Unity package via a git URL can be found [here](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

```sh
https://github.com/BugSplat-Git/bugsplat-unity.git
```

## 🧑‍🏫 Sample

After installing `com.bugsplat.unity`, you can import a sample project to help you get started with BugSplat. Click here if you'd like to skip the sample project and get straight to the [usage](#usage) instructions.

To import the sample, click the carrot next to **Samples** to reveal the **my-unity-crasher** sample. Click **Import** to add the sample to your project.

![Importing the Sample](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/b7a39388-eb76-413a-a92f-72fd39c9a7d6)


In the Project Assets browser, open the **Sample** scene from `Samples > BugSplat > Version > my-unity-crasher > Scenes`.

Next, select `Samples > BugSplat > Version > my-unity-crasher` to reveal the **BugSplatOptions** object. Click BugSplatOptions and replace the database value with your BugSplat database.

![Finding the Sample](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/ba9aa64a-1d85-45a8-b11f-565520c30bcf)

![Configuring BugSplat](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/a6250cea-a4da-44a8-b6cb-ff2467b0d978)

Click **Play** and click or tap one of the buttons to send an error report to BugSplat. To view the error report, navigate to the BugSplat [Dashboard](https://app.bugsplat.com/v2/dashboard) and ensure you have selected the correct database.

![Running the Sample](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/4418b736-dc88-496a-ada6-a27ad19032f1)

Navigate to the [Crashes](https://app.bugsplat.com/v2/crashes) page, and click the value in the ID column to see the details of your report, including the call stack, log file, and screenshot of your app when the error occurred.

![BugSplat Crash Page](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/f108d7e9-ee90-4a09-a7b4-8a9b5d764942)

## ⚙️ Configuration

BugSplat's Unity integration is flexible and can be used in various ways. The easiest way to get started is to attach the `BugSplatManager` Monobehaviour to a GameObject.

![BugSplat Manager](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/ef5240a6-9676-43c6-a482-51216cb34401)

`BugSplatManager` needs to be initialized with a `BugSplatOptions` serialized object. A new instance of `BugSplatOptions` can be created through the Asset Create menu.

![BugSplat Create Options](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec402d1-4b8a-49cf-96e9-00d951717771)

Configure fields as appropriate. Note that if Application or Version are left empty, `BugSplat` will  default these values to `Application.productName` and `Application.version`, respectively.

![BugSplat Options](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/be7ee217-9170-48b4-b780-fcb47e221f77)

Finally, provide a valid `BugSplatOptions` to `BugSplatManager`. 

![BugSplat Manager Configured](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/67bed7b5-e2a9-4f52-b5bb-bdc8eebd35a0)

## ⌨️ Usage

If you're using `BugSplatOptions` and `BugSplatManager`, BugSplat automatically configures an `Application.logMessageReceived` handler that will post reports when it encounters a log message of type `Exception`. You can also extend your BugSplat integration and [customize report metadata](#adding-metadata), [report exceptions in try/catch blocks](#trycatch-reporting), [prevent repeated reports](#preventing-repeated-reports), and [upload windows minidumps](#windows) from native crashes.

### Adding Metadata

First, find your instance of `BugSplat`. The following is an example of how to find an instance of `BugSplat` via `BugSplatManager`:

```cs
var bugsplat = FindObjectOfType<BugSplatManager>().BugSplat;
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
    bugsplat = FindObjectOfType<BugSplatManager>().BugSplat;\
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

### Try/Catch Reporting

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

### Preventing Repeated Reports

By default, BugSplat prevents reports from being sent at a rate greater than 1 per every 60 seconds. You can override the default crash report throttling implementation by setting `ShouldPostException` on your BugSplat instance. To override `ShouldPostException`, assign the property a new `Func<Exception, bool>` value. Be sure your new implementation can handle a null value for `Exception`!

The following example demonstrates how you could implement your own time-based report throttling mechanism:

```cs
var lastPost = DateTime.Now;

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

## 🪟 Windows

BugSplat can be configured to upload Windows minidumps created by the `UnityCrashHandler`. BugSplat will automatically pull Unity Player symbols from the [Unity Symbol Server](https://docs.unity3d.com/Manual/WindowsDebugging.html). If your game contains Native Windows C++ plugins, `.dll` and `.pdb` files in the `Assets/Plugins/x86` and `Assets/Plugins/x86_64` folders will be uploaded by BugSplat's PostBuild script and used in symbolication.

### Symbols

To enable the uploading of plugin symbols, generate an OAuth2 Client ID and Client Secret on the BugSplat [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page. Add your Client ID and Client Secret to the `BugSplatOptions` object you generated in the [Configuration](#⚙️-configuration) section. Once configured, plugins will be uploaded automatically the next time you build your project.

### Minidumps (Crashes)

The methods `PostCrash`, `PostMostRecentCrash`, and `PostAllCrashes` can be used to upload minidumps to BugSplat. We recommend running `PostMostRecentCrash` when your game launches.

```cs
StartCoroutine(bugsplat.PostCrash(new FileInfo("/path/to/crash/folder")));
StartCoroutine(bugsplat.PostMostRecentCrash());
StartCoroutine(bugsplat.PostAllCrashes());
```

Each of the methods that post crashes to BugSplat also accept a `MinidumpPostOptions` parameter and a `callback`. The usage of `MinidumpPostOptions` and `callback` are nearly identical to the `ExceptionPostOptions` example listed above.

You can generate a test crash on Windows with any of the following methods.

```cs
Utils.ForceCrash(ForcedCrashCategory.Abort);
Utils.ForceCrash(ForcedCrashCategory.AccessViolation);
Utils.ForceCrash(ForcedCrashCategory.FatalError);
Utils.ForceCrash(ForcedCrashCategory.PureVirtualFunction);
```

## 🌎 UWP

To use BugSplat in a Universal Windows Platform application, you will need to add some capabilities to the `Package.appxmanifest` file in the solution directory that Unity generates at build time.

### Capabilities

Reporting exceptions, crashes, and uploading log files requires the `Internet (Client)` capability.

### Minidumps (Crashes)

We found that restricted capabilities were required in order to generate minidumps. Please see this Microsoft [document](https://docs.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps) that describes how to configure your system to generate minidumps for UWP native crashes.

To upload minidumps from `%LOCALAPPDATA%\CrashDumps` you will also need to add the `broadFileSystemAccess` restricted capability. To add access to the file system you will need to add the following to your `Package.appxmanifest`:

```xml
<Package xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" ... >
```

Under the `Capabilities` section add the `broadFileSystemAccess` capability:

```xml
<Capabilities>
    <rescap:Capability Name="broadFileSystemAccess" />
</Capabilities>
```

Finally, ensure that your application has access to the file system. The following is a snippet illustrating where this is set for our [my-unity-crasher](https://github.com/BugSplat-Git/my-unity-crasher) sample:

![Unity file system access](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/17e21073-8992-4ef3-88ca-e024486ebc9f)

## 🤖 Android

The bugsplat-unity plugin supports crash reporting for native C++ crashes on Android via Crashpad. To configure crash reporting for Android, set the `UseNativeCrashReportingForAndroid` and `UploadDebugSymbolsForAndroid` properties to `true` on the BugSplatManager instance.

You'll also need to configure the scripting backend to use IL2CPP, and target ARM64 (ARMV7a is not supported)

![Android Player Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/9ec8f5b7-8dfd-43db-84e0-7e7d1229324a)

When you build your app for Android, be sure to set `Create symbols.zip` to `Debugging`

![Android Build Settings](https://github.com/BugSplat-Git/bugsplat-unity/assets/2646053/0181f2a8-8fb2-4745-b336-3e7f210aa55e)

## 🍎 iOS

The bugsplat-unity plugin supports crash reporting for native C++ crashes on iOS via bugsplat-ios. To configure crash reporting for iOS, set the `UseNativeCrashReportingForIos` and `UploadDebugSymbolsForIos` properties to `true` on the BugSplatManager instance.

## 🧩 API

The following API methods are available to help you customize BugSplat to fit your needs.

### BugSplatManager

| Setting | Description |
| --------------- | --------------- |
| DontDestroyManagerOnSceneLoad | Should the BugSplat Manager persist through scene loads? | 
| RegisterLogMessageRecieved | Register a callback function and allow BugSplat to capture instances of LogType.Exception.|

### BugSplat Options

| Option | Description |
| --------------- | --------------- |
| Database  | The name of your BugSplat database. | 
| Application| The name of your BugSplat application. Defaults to Application.productName if no value is set.|
| Version | The version of your BugSplat application. Defaults to Application.version if no value is set.|
| Description | A default description that can be overridden by call to Post.|
| Email | A default email that can be overridden by call to Post.|
| Key | A default key that can be overridden by call to Post.|
| Notes | A default general purpose field that can be overridden by call to post |
| User | A default user that can be overridden by call to Post |
| CaptureEditorLog| Should BugSplat upload Editor.log when Post is called|
| CapturePlayerLog| Should BugSplat upload Player.log when Post is called |
| CaptureScreenshots | Should BugSplat a screenshot and upload it when Post is called |
| PostExceptionsInEditor | Should BugSplat upload exceptions when in editor |
| PersistentDataFileAttachmentPaths |  Paths to files (relative to Application.persistentDataPath) to upload with each report |
| ShouldPostException | Settable guard function that is called before each BugSplat report is posted |
| SymbolUploadClientId | An OAuth2 Client ID value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page
| SymbolUploadClientSecret | An OAuth2 Client Secret value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page

## 🧑‍💻 Contributing

BugSplat ❤️s open source! If you feel that this package can be improved, please open an [Issue](https://github.com/BugSplat-Git/bugsplat-unity/issues). If you have an awesome new feature you'd like to implement, we'd love to merge your [Pull Request](https://github.com/BugSplat-Git/bugsplat-unity/pulls). You can also send us an [email](mailto:support@bugsplat.com), join us on [Discord](https://discord.gg/K4KjjRV5ve), or message us via the in-app chat on [bugsplat.com](https://bugsplat.com).
