using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace BugSplatUnity.Runtime.Client
{
	[CreateAssetMenu(menuName = "BugSplat Configuration Options")]
	public class BugSplatConfigurationOptions : ScriptableObject
	{
		[Header("Database Settings")]
		[Tooltip("The name of your BugSplat database.")]
		public string Database;

		[Header("User Settings")]
		[Tooltip("A default email that can be overridden by call to Post.")]
		public string Email;

		[Tooltip("A default key that can be overridden by call to Post.")]
		public string Key;

		[Tooltip("A default user that can be overridden by call to Post")]
		public string User;

		[Header("Reporting Options")]
		[Tooltip("Upload Editor.log when Post is called")]
		public bool CaptureEditorLog;

		[Tooltip("Upload Player.log when Post is called")]
		public bool CapturePlayerLog;

		[Tooltip("Take a screenshot and upload it when Post is called")]
		public bool CaptureScreenshots;

		[Header("File Attachments")]
		[Tooltip("Paths for files within Application.persistentDataPath (ex. Player.log => /Player.log")]
		public List<string> PersistentDataFileAttachmentPaths;
	}
}


