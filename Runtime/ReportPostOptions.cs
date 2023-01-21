using BugSplatDotNetStandard;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace BugSplatUnity
{
    public interface IReportPostOptions
    {
        List<FileInfo> AdditionalAttachments { get; }
        List<FormDataParam> AdditionalFormDataParams { get; }
        string Description { get; set; }
        string Email { get; set; }
        string Key { get; set; }
        string Notes { get; set; }
        string User { get; set; }
        int CrashTypeId { get; set; }
    }

    public class FormDataParam : IFormDataParam
    {
        public string Name { get; set; }
        public HttpContent Content { get; set; }
        public string FileName { get; set; }
    }

    public class ReportPostOptions : IReportPostOptions
    {
        public List<FileInfo> AdditionalAttachments { get; } = new List<FileInfo>();
        public List<FormDataParam> AdditionalFormDataParams { get; } = new List<FormDataParam>();
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string Notes { get; set; }
        public string User { get; set; }
        public int CrashTypeId { get; set; }
    }
}
