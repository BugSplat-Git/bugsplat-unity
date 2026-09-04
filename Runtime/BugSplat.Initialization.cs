using System;
using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Manager;
using UnityEngine;

namespace BugSplatUnity
{
    public partial class BugSplat
    {
        /// <summary>
        /// The live client, or null before <see cref="Initialize(BugSplatOptions)"/> has run. With
        /// <see cref="BugSplatOptions.InitializeAutomatically"/> on, the default, it is set before the
        /// first scene loads, so it is ready inside any Awake.
        /// </summary>
        public static BugSplat Instance { get; private set; }

        /// <summary>
        /// Whether <see cref="Instance"/> is set.
        /// </summary>
        public static bool IsInitialized => Instance != null;

        private const string NotConfiguredMessage =
            "BugSplat is not configured, so nothing will be reported. " + BugSplatOptions.ConfigureHint +
            " If your code calls BugSplat.Initialize itself, define " + BugSplatOptions.ManualInitializeDefine +
            " and this warning goes away.";

        private const string AlreadyInitializedMessage =
            "BugSplat.Initialize was called but BugSplat is already initialized; the existing instance is " +
            "kept. If Initialize Automatically is on in Edit > Project Settings > BugSplat, there is nothing " +
            "to call - BugSplat.Instance is ready before the first scene loads.";

        private static BugSplatRuntime runtime;

        /// <summary>
        /// Builds the client from <paramref name="options"/>, starts the native reporter where one is
        /// enabled, installs the log hooks, and spawns the object that posts from the main thread.
        /// Only needed when <see cref="BugSplatOptions.InitializeAutomatically"/> is off - to wait for
        /// a consent screen, say. Idempotent: a second call logs a warning and returns the existing
        /// instance rather than throwing, because reporting a crash must not itself become one.
        /// </summary>
        public static BugSplat Initialize(BugSplatOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return Initialize(
                options,
                new BugSplatRuntime.Settings(
                    options.RegisterLogMessageReceived,
                    options.CaptureExceptionsOnBackgroundThreads,
                    options.CaptureUnobservedTaskExceptions));
        }

        // The obsolete BugSplatManager still carries its own copies of the capture flags, authored in
        // 4.x scenes, and those have to win over the asset's when it is the one initializing.
        internal static BugSplat Initialize(BugSplatOptions options, BugSplatRuntime.Settings hostSettings)
        {
            if (Instance != null)
            {
                Debug.LogWarning(AlreadyInitializedMessage);
                return Instance;
            }

            var bugSplat = CreateFromOptions(options);
            runtime = BugSplatRuntime.Create(bugSplat, hostSettings);
            Instance = bugSplat;
            return bugSplat;
        }

        /// <summary>
        /// Unhooks the log callbacks, destroys the host object, and clears <see cref="Instance"/>.
        /// Internal rather than public: the native reporters cannot be uninstalled once started, so a
        /// public Shutdown would promise more than it can deliver. Tests and the obsolete manager use it.
        /// </summary>
        internal static void Shutdown()
        {
            if (runtime != null)
            {
                // Synchronously, so the next Initialize never overlaps a host whose Destroy is still
                // pending at end of frame.
                runtime.Detach();
                UnityEngine.Object.Destroy(runtime.gameObject);
            }

            runtime = null;
            Instance = null;
        }

        // Enter Play Mode Options without domain reload keeps statics across plays. The previous
        // play's host object was destroyed on exit, so without this Instance would point at a client
        // whose hooks are gone.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            runtime = null;
            Instance = null;
        }

        // BeforeSceneLoad is earlier than any Awake, which is what the native reporters need: on
        // macOS and iOS a report from the previous session is processed while the reporter starts,
        // and a native crash during the first scene's load is only caught if the reporter is already
        // running. Preloaded assets are loaded by AfterAssembliesLoaded, so the options asset is
        // available here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void AutoInitialize()
        {
#if BUGSPLAT_MANUAL_INITIALIZE || BUGSPLAT_DISABLED
            // BUGSPLAT_MANUAL_INITIALIZE: the project calls Initialize itself.
            // BUGSPLAT_DISABLED: BugSplat is off for this build target.
            // Either way nothing here runs, and nothing reads the options asset.
            return;
#else
            if (Instance != null)
            {
                return;
            }

            var options = BugSplatOptions.ResolveConfigured();
            if (options == null)
            {
                Debug.LogWarning(NotConfiguredMessage);
                return;
            }

            if (!options.Enabled)
            {
                // Said out loud where a developer will see it, so a build that reports nothing is
                // never a mystery. Kept out of release players, which is where Enabled is usually on
                // anyway.
                if (Debug.isDebugBuild)
                {
                    Debug.Log(
                        $"BugSplat is turned off on {options.name} (Enabled is unchecked), so nothing will be " +
                        "reported. Check Enabled in Edit > Project Settings > BugSplat to turn it back on.");
                }

                return;
            }

            if (!options.InitializeAutomatically)
            {
                return;
            }

            if (string.IsNullOrEmpty(options.Database))
            {
                Debug.LogWarning(
                    $"BugSplat: {options.name} has an empty Database, so nothing will be reported. {BugSplatOptions.ConfigureHint}");
                return;
            }

            Initialize(options);
#endif
        }
    }
}
