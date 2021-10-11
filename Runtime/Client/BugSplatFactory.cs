using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BugSplatUnity.Runtime.Client
{
	public static class BugSplatFactory
	{
		public static BugSplat CreateBugSplatFromConfigurationOptions(BugSplatConfigurationOptions configurationOptions, string application, string version)
		{			
			var bugSplat = new BugSplat(configurationOptions.Database, application, version);

			bugSplat.Email = configurationOptions?.Email;
			bugSplat.Key = configurationOptions?.Key;
			bugSplat.User = configurationOptions?.User;
			bugSplat.CaptureEditorLog = configurationOptions.CaptureEditorLog;
			bugSplat.CapturePlayerLog = configurationOptions.CapturePlayerLog;
			bugSplat.CaptureScreenshots = configurationOptions.CaptureScreenshots;

			var paths = configurationOptions.PersistentDataFileAttachmentPaths
				.Select(fileAttachment => UnityEngine.Application.persistentDataPath + fileAttachment)
				.ToList();

			foreach( var filePath in configurationOptions.PersistentDataFileAttachmentPaths )
			{
				var fileInfo = new FileInfo(filePath);
				bugSplat.Attachments.Add(fileInfo);
			}

			return bugSplat;
		}
	}
}


