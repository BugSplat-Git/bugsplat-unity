using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using BugSplatUnity.Runtime.Settings;
using BugSplatUnity.Runtime.Util;

namespace BugSplatUnity.Runtime.Client
{
    internal class WebAssemblyCrashReporter
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void BugSplatWebGL_Configure(string configJson, string attributesJson);

        [DllImport("__Internal")]
        private static extern void BugSplatWebGL_Disable();
#endif

        private readonly string database;
        private readonly string application;
        private readonly string version;
        private readonly IClientSettingsRepository clientSettings;

        internal bool Enabled { get; private set; }

        internal WebAssemblyCrashReporter(string database, string application, string version, IClientSettingsRepository clientSettings)
        {
            this.database = database;
            this.application = application;
            this.version = version;
            this.clientSettings = clientSettings;
        }

        internal void Enable()
        {
            Enabled = true;
            Sync();
        }

        internal void Disable()
        {
            Enabled = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            BugSplatWebGL_Disable();
#endif
        }

        internal void Sync()
        {
            if (!Enabled)
            {
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            BugSplatWebGL_Configure(ConfigJson(), AttributesJson.Serialize(clientSettings.Attributes));
#endif
        }

        internal string ConfigJson()
        {
            return AttributesJson.Serialize(new Dictionary<string, string>
            {
                { "database", database },
                { "application", application },
                { "version", version },
                { "key", clientSettings.Key },
                { "user", clientSettings.User },
                { "email", clientSettings.Email },
                { "description", clientSettings.Description },
                { "notes", clientSettings.Notes }
            });
        }
    }
}
