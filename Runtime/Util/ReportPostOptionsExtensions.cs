using BugSplatUnity.Runtime.Settings;
using System.Collections.Generic;

#if !UNITY_2022_1_OR_NEWER
using BugSplatUnity.Runtime.Util.Extensions;
#endif

namespace BugSplatUnity.Runtime.Util
{
    internal static class ReportPostOptionsExtensions
    {
        public static void SetNullOrEmptyValues(this IReportPostOptions options, IClientSettingsRepository clientSettings)
        {
            if (clientSettings.Attachments?.Count != 0)
            {
                options.AdditionalAttachments.AddRange(clientSettings.Attachments);
            }

            foreach (var attribute in clientSettings.Attributes)
            {
                options.AdditionalAttributes.TryAdd(attribute.Key, attribute.Value);
            }

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

            if (string.IsNullOrEmpty(options.Notes))
            {
                options.Notes = clientSettings.Notes;
            }

            if (string.IsNullOrEmpty(options.User))
            {
                options.User = clientSettings.User;
            }
        }
    }
}
