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

In order to use this package please make sure you have completed the following checklist:
* [Sign Up](https://app.bugsplat.com/v2/sign-up) as a new BugSplat user
* [Log In](https://app.bugsplat.com/auth0/login) to your account

Additionally, you can check out our [my-unity-crasher](https://github.com/BugSplat-Git/my-unity-crasher) sample that demonstrates how to use `com.bugsplat.unity`.

## 🏗 Installation

BugSplat's `com.bugsplat.unity` package can be added to your project via [OpenUPM](https://openupm.com/packages/com.bugsplat.unity/) or a URL to our git [repository](https://github.com/BugSplat-Git/bugsplat-unity.git).

### OpenUPM
Information on how to install the OpenUPM package for node.js can be found [here](https://openupm.com).

```sh
openupm add com.bugsplat.unity
```

### Git
Information on adding a Unity package via a git URL can be found [here](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

```sh
https://github.com/BugSplat-Git/bugsplat-unity.git
```

## 🧑‍🏫 Sample

After installing `com.bugsplat.unity` you'll have the opportunity to import a sample project that's fully configured to post error reports to BugSplat. Click here if you'd like to skip the sample project and get straight to the [usage](#usage) instructions.

To import the sample, click the carrot next to **Samples** to reveal the **my-unity-crasher** sample. Click **Import** to add the sample to your project.

![Importing the Sample](https://bugsplat-public.s3.amazonaws.com/unity/import-sample.png)

In the Project Assets browser, open the **Sample** scene from `Samples > BugSplat > Version > my-unity-crasher > Scenes`.

Next, select `Samples > BugSplat > Version > my-unity-crasher` to reveal the **BugSplatOptions** object. Click BugSplatOptions and replace the value of database with your BugSplat database.

![Finding the Sample](https://bugsplat-public.s3.amazonaws.com/unity/bugsplat-options.png)

![Configuring BugSplat](https://bugsplat-public.s3.amazonaws.com/unity/bugsplat-manager.png)

Click **Play** and click or tap one of the buttons to send an error report to BugSplat. To view the error report, navigate to the BugSplat [Dashboard] (https://app.bugsplat.com/v2/dashboard) and ensure that you have the correct database selected.

![Running the Sample](https://bugsplat-public.s3.amazonaws.com/unity/sample-scene.png)

## ⚙️ Configuration

BugSplat's Unity integration is flexible and can be used in a variety of ways. The easiest way to get started is to attach the `BugSplatManager` Monobehaviour to a GameObject.

![BugSplat Manager](https://bugsplat-public.s3.amazonaws.com/unity/BugSplatManager.png)

`BugSplatManager` needs be initialized with a `BugSplatOptions` serialized object. A new instance of `BugSplatOptions` can be created through the Asset create menu.

![BugSplat Create Options](https://bugsplat-public.s3.amazonaws.com/unity/BugSplatOptions.png)

Configure fields as appropriate. Note that if Application or Version are left empty, `BugSplat` will by default configure these values with `Application.productName` and `Application.version`, respectively.

![BugSplat Options](https://bugsplat-public.s3.amazonaws.com/unity/BugSplatOptionsObject.png)

Finally, provide a valid `BugSplatOptions` to `BugSplatManager`. 

![BugSplat Manager Configured](https://bugsplat-public.s3.amazonaws.com/unity/ConfiguredBugSplatManager.png)

### BugSplat Manager Settings
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
| User | A default user that can be overridden by call to Post |
| CaptureEditorLog| Should BugSplat upload Editor.log when Post is called|
| CapturePlayerLog| Should BugSplat upload Player.log when Post is called |
| CaptureScreenshots | Should BugSplat a screenshot and upload it when Post is called |
| PostExceptionsInEditor | Should BugSplat upload exceptions when in editor |
| PersistentDataFileAttachmentPaths |  Paths to files (relative to Application.persistentDataPath) to upload with each report |
| SymbolUploadClientId | An OAuth2 Client ID value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page
| SymbolUploadClientSecret | An OAuth2 Client Secret value used for uploading [symbol files](https://docs.bugsplat.com/introduction/development/working-with-symbol-files) generated via BugSplat's [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page

### Configuring in Code
If your application requires special configuration, you may optionally create your own script to manage and instantiate `BugSplat`. To do so, create a new script and attach it to a GameObject. In your script, add a using statement for BugSplatUnity.

```cs
using BugSplatUnity;
```

Next, create a new instance of `BugSplat` passing it your `database`, `application`, and `version`. Use `Application.productName`, and `Application.version` for application and version respectively.

```cs
var bugsplat = new BugSplat(database, Application.productName, Application.version);
```

You can set the defaults for a variety of properties on the `BugSplat` instance. These default values will be used in exception and crash posts. Additionally, you can tell BugSplat to capture a screenshot, include the Player.log file, and include the Editor.log file when an exception is recorded.

```cs
bugsplat.Attachments.Add(new FileInfo("/path/to/attachment.txt"));
bugsplat.Description = "description!";
bugsplat.Email = "fred@bugsplat.com";
bugsplat.Key = "key!";
bugsplat.User = "Fred";
bugsplat.CaptureEditorLog = true;
bugsplat.CapturePlayerLog = false;
bugsplat.CaptureScreenshots = true;
```

Alternatively, a new instance of BugSplat can be created with `BugSplatOptions`.

```cs
[SerializeField]
BugSplatOptions bugSplatOptions;
...
var bugsplat = BugSplat.CreateFromOptions(bugSplatOptions);
```

## ⌨️ Usage

First, find your instance of BugSplat. For example, using the BugSplatManager:

```cs
var bugsplat = FindObjectOfType<BugSplatManager>().BugSplat;
```

You can send exceptions to BugSplat in a try/catch block by calling `Post`.

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
    User = "Barney"
};

options.AdditionalAttachments.Add(new FileInfo("/path/to/additional.txt"));

static void callback()
{
    Debug.Log($"Exception post callback!");
};

StartCoroutine(bugsplat.Post(ex, options, callback));
```

You can also configure a global `LogMessageRecieved` callback. When the BugSplat instance recieves a logging event where the type is `Exception` it will upload the exception. Note that the `BugSplatManager` can be configured to register this callback at startup.

```cs
Application.logMessageReceived += bugsplat.LogMessageReceived;
```

### Windows Minidumps

BugSplat can be configured to upload Windows minidumps created by the `UnityCrashHandler`. BugSplat will automatically pull Unity Player symbols from the [Unity Symbol Server](https://docs.unity3d.com/Manual/WindowsDebugging.html). If your game contains Native Windows C++ plugins, `.dll` and `.pdb` files in the `Assets/Plugins/x86` and `Assets/Plugins/x86_64` folders will be uploaded by BugSplat's PostBuild script and used in symbolication.

To enable uploading of plugin symbols, generate an OAuth2 Client ID and Client Secret on the BugSplat [Integrations](https://app.bugsplat.com/v2/settings/database/integrations) page. Add your Client ID and Client Secret to the `BugSplatOptions` object you generated in the [Configuration](#⚙️-configuration) section. Once configured, plugins will be uploaded automatically the next time you build your project.

The methods `PostCrash`, `PostMostRecentCrash`, and `PostAllCrashes` can be used to upload minidumps to BugSplat. We recommend running `PostMostRecentCrash` when your game launches.

```cs
StartCoroutine(bugsplat.PostCrash(new FileInfo("/path/to/crash/folder")));
StartCoroutine(bugsplat.PostMostRecentCrash());
StartCoroutine(bugsplat.PostAllCrashes());
```

Each of the methods that post crashes to BugSplat also accept a `MinidumpPostOptions` parameter and a `callback`. The usage of `MinidumpPostOptions` and `callback` are nearly identically to the `ExceptionPostOptions` example listed above.

You can generate a test crash on Windows with any of the following methods.

```cs
Utils.ForceCrash(ForcedCrashCategory.Abort);
Utils.ForceCrash(ForcedCrashCategory.AccessViolation);
Utils.ForceCrash(ForcedCrashCategory.FatalError);
Utils.ForceCrash(ForcedCrashCategory.PureVirtualFunction);
```

Once you've posted an exception or a minidump to BugSplat click the link in the **ID** column on either the [Dashboard](https://app.bugsplat.com/v2/dashboard) or [Crashes](https://app.bugsplat.com/v2/crashes) pages to see details about your crash.

![BugSplat crash page](https://bugsplat-public.s3.amazonaws.com/unity/my-unity-crasher.png)

## 🌎 UWP

In order to use BugSplat in a Universal Windows Platform application you will need to add some capabilities to the `Package.appxmanifest` file in the solution directory that Unity generates at build time.

### Exceptions and Log Files

In order to report exceptions and upload log files you will need to add the `Internet (Client)` capability.

### Windows Minidumps

To upload minidumps created on Windows you will need to add the `Internet (Client)` capability.

Additionally, we found there were some restricted capabilities that were required in order to generate minidumps. Please see this Microsoft [document](https://docs.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps) that describes how to configure your system to generate minidumps for UWP native crashes.

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

![Unity file system access](https://bugsplat-public.s3.amazonaws.com/unity/unity-file-system-access.png)

## 🧑‍💻 Contributing

BugSplat ❤️s open source! If you feel that this package can be improved, please open an [Issue](https://github.com/BugSplat-Git/bugsplat-unity/issues). If you have an awesome new feature you'd like to implement, we'd love to merge your [Pull Request](https://github.com/BugSplat-Git/bugsplat-unity/pulls). You can also send us an [email](mailto:support@bugsplat.com), join us on [Discord](https://discord.gg/K4KjjRV5ve), or message us via the in-app chat on [bugsplat.com](https://bugsplat.com).
