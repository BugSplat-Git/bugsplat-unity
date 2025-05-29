using BugSplatDotNetStandard;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

#if !UNITY_2022_1_OR_NEWER
using BugSplatUnity.Runtime.Util.Extensions;
#endif

namespace BugSplatUnity.Runtime.Client
{
    internal interface IDotNetStandardExceptionClient
    {
        Task<HttpResponseMessage> Post(string stackTrace, IReportPostOptions options = null);
        Task<HttpResponseMessage> Post(Exception ex, IReportPostOptions options = null);
        Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, IReportPostOptions options = null);
    }

    internal class DotNetStandardClient : INativeCrashReportClient, IDotNetStandardExceptionClient
    {
        private readonly BugSplatDotNetStandard.BugSplat _bugsplat;

        public DotNetStandardClient(BugSplatDotNetStandard.BugSplat bugsplat)
        {
            _bugsplat = bugsplat;
        }

        public Task<HttpResponseMessage> Post(string stackTrace, IReportPostOptions options = null)
        {
            return _bugsplat.Post(stackTrace, CreateExceptionPostOptions(options));
        }

        public Task<HttpResponseMessage> Post(Exception ex, IReportPostOptions options = null)
        {
            return _bugsplat.Post(ex, CreateExceptionPostOptions(options));
        }

        public Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, IReportPostOptions options = null)
        {
            return _bugsplat.Post(minidumpFileInfo, CreateMinidumpPostOptions(options));
        }

        private ExceptionPostOptions CreateExceptionPostOptions(IReportPostOptions options)
        {
            options ??= new ReportPostOptions();
            var exceptionPostOptions = new ExceptionPostOptions();
            
            foreach (var attribute in options.AdditionalAttributes) 
            {
                exceptionPostOptions.Attributes.TryAdd(attribute.Key, attribute.Value);
            }

            exceptionPostOptions.Attachments.AddRange(options.AdditionalAttachments);
            exceptionPostOptions.FormDataParams.AddRange(options.AdditionalFormDataParams);
            exceptionPostOptions.Description = options.Description;
            exceptionPostOptions.Email = options.Email;
            exceptionPostOptions.Key = options.Key;
            exceptionPostOptions.Notes = options.Notes;
            exceptionPostOptions.User = options.User;
            exceptionPostOptions.ExceptionType = (BugSplatDotNetStandard.BugSplat.ExceptionTypeId)options.CrashTypeId;
            return exceptionPostOptions;
        }

        private MinidumpPostOptions CreateMinidumpPostOptions(IReportPostOptions options)
        {
            options ??= new ReportPostOptions();
            var minidumpPostOptions = new MinidumpPostOptions();
            
            foreach (var attribute in options.AdditionalAttributes) 
            {
                minidumpPostOptions.Attributes.TryAdd(attribute.Key, attribute.Value);
            }

            minidumpPostOptions.Attachments.AddRange(options.AdditionalAttachments);
            minidumpPostOptions.FormDataParams.AddRange(options.AdditionalFormDataParams);
            minidumpPostOptions.Description = options.Description;
            minidumpPostOptions.Email = options.Email;
            minidumpPostOptions.Key = options.Key;
            minidumpPostOptions.Notes = options.Notes;
            minidumpPostOptions.User = options.User;
            minidumpPostOptions.MinidumpType = (BugSplatDotNetStandard.BugSplat.MinidumpTypeId)options.CrashTypeId;
            return minidumpPostOptions;
        }
    }
}
