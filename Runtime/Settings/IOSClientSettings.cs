using System;
using System.Collections.Generic;
using System.IO;
using BugSplatUnity.Runtime.Settings;

public class IOSClientSettings : IClientSettingsRepository
{
	public List<FileInfo> Attachments { get; } = new List<FileInfo>();
	public bool CaptureEditorLog { get; set; }
	public bool CapturePlayerLog { get; set; }
	public bool CaptureScreenshots { get; set; }
	public bool PostExceptionsInEditor { get; set; }
	public Func<Exception, bool> ShouldPostException { get; set; }
	public string Description { get; set; }
	public string Email { get; set; }
	public string Key { get; set; }
	public string User { get; set; }
}
