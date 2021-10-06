using BugSplatDotNetStandard;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BugSplatUnity.Runtime.Client
{
    public class ReportPostOptions : IReportPostOptions
    {
        public List<FileInfo> AdditionalAttachments { get; }
        public List<IFormDataParam> AdditionalFormDataParams { get; }
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string User { get; set; }
        public int CrashTypeId { get; set; }

#if UNITY_STANDALONE_WIN || UNITY_WSA
        public ExceptionPostOptions ToExceptionPostOptions()
        {
            // TODO BG move IFormDataParam to BugSplatDotNetStandard
            var formDataParams = AdditionalFormDataParams.Select(param =>
            {
                return new FormDataParam()
                {
                    Content = param.Content,
                    FileName = param.FileName,
                    Name = param.Name
                };
            });
            var exceptionPostOptions = new ExceptionPostOptions();
            exceptionPostOptions.AdditionalAttachments.AddRange(AdditionalAttachments);
            exceptionPostOptions.AdditionalFormDataParams.AddRange(formDataParams);
            exceptionPostOptions.Description = Description;
            exceptionPostOptions.Email = Email;
            exceptionPostOptions.Key = Key;
            exceptionPostOptions.User = User;
            exceptionPostOptions.ExceptionType = (BugSplatDotNetStandard.BugSplat.ExceptionTypeId)CrashTypeId;
            return exceptionPostOptions;
        }

        public MinidumpPostOptions ToMinidumpPostOptions()
        {
            // TODO BG move IFormDataParam to BugSplatDotNetStandard
            var formDataParams = AdditionalFormDataParams.Select(param =>
            {
                return new FormDataParam()
                {
                    Content = param.Content,
                    FileName = param.FileName,
                    Name = param.Name
                };
            });
            var minidumpPostOptions = new MinidumpPostOptions();
            minidumpPostOptions.AdditionalAttachments.AddRange(AdditionalAttachments);
            minidumpPostOptions.AdditionalFormDataParams.AddRange(formDataParams);
            minidumpPostOptions.Description = Description;
            minidumpPostOptions.Email = Email;
            minidumpPostOptions.Key = Key;
            minidumpPostOptions.User = User;
            minidumpPostOptions.MinidumpType = (BugSplatDotNetStandard.BugSplat.MinidumpTypeId)CrashTypeId;
            return minidumpPostOptions;
        }
    }
#endif
    }
