using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace BugSplatUnity.Runtime.Client
{
    public interface IFormDataParam
    {
        public string Name { get; set; }
        public HttpContent Content { get; set; }
        public string FileName { get; set; }
    }

    public interface IReportPostOptions
    {
        public List<FileInfo> AdditionalAttachments { get; }
        public List<IFormDataParam> AdditionalFormDataParams { get; }
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string User { get; set; }
        public int CrashTypeId { get; set; }
    }

    public class ReportPostOptions : IReportPostOptions
    {
        public List<FileInfo> AdditionalAttachments { get; } = new List<FileInfo>();
        public List<IFormDataParam> AdditionalFormDataParams { get; } = new List<IFormDataParam>();
        public string Description { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public string User { get; set; }
        public int CrashTypeId { get; set; }
    }
}
