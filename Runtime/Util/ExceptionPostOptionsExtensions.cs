using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Settings;

namespace BugSplatUnity.Runtime.Util
{
    internal static class ExceptionPostOptionsExtensions
    {
        public static void SetNullOrEmptyValues(this IExceptionPostOptions options, IClientSettingsRepository clientSettings)
        {
            if (string.IsNullOrEmpty(options.Description))
            {
                options.Description = clientSettings.Description;
            }

            if (string.IsNullOrEmpty(options.Email))
            {
                options.Email = clientSettings.Email;
            }

            if (string.IsNullOrEmpty(options.Key))
            {
                options.Key = clientSettings.Key;
            }

            if (string.IsNullOrEmpty(options.User))
            {
                options.User = clientSettings.User;
            }
        }
    }
}
