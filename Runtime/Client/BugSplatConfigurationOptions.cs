using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace BugSplatUnity.Runtime.Client
{
	[CreateAssetMenu(menuName = "BugSplat Config Settings")]
	public class BugSplatConfigurationOptions : ScriptableObject
	{
		//TODO Check for more helpful titles, maybe there's some standards we're using that would fit better here.
		[Header("Database Settings")]
		[Tooltip("The name of your BugSplat database.")]
		public string Database;

		[Tooltip("The name of your application.")]
		public string Application;

		[Tooltip("The version of the application")]
		public string Version;

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

		[Header("BugSplatManager Options")]
		[Tooltip("Should the BugSplatManager be destroyed when a new scene is loaded?")]
		public bool DestroyManagerOnSceneLoad = false;

		[Tooltip("Register BugSplat to capture LogType.Exceptions on initialization")]
		public bool RegisterLogMessageRecieved;

		public List<string> PersistentDataFileAttachmentPaths => persistentDataFileAttachmentPaths
			.Select(fileAttachment => UnityEngine.Application.persistentDataPath + fileAttachment)
			.ToList();

		[SerializeField]
		[Header("File Attachments")]
		[Tooltip("Paths for files that should be attached during a crash, which will be prepended by Application.persistentDataPath")]
		private List<string> persistentDataFileAttachmentPaths;
	}
}


