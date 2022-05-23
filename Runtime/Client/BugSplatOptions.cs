using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace BugSplatUnity.Runtime.Client
{
	[CreateAssetMenu(menuName = "BugSplat Options")]
	public class BugSplatOptions : ScriptableObject
	{
		[Header("Required")]
		[Tooltip("The name of your BugSplat database.")]
		public string Database;

		[Header("Optional")]
		[Tooltip("The name of your BugSplat application. Defaults to Application.productName if no value is set.")]
		public string Application;

		[Tooltip("The version of your BugSplat application. Defaults to Application.version if no value is set.")]
		public string Version;

		[Tooltip("A default description that can be overridden by call to Post.")]
		public string Description;

		[Tooltip("A default email that can be overridden by call to Post.")]
		public string Email;

		[Tooltip("A default key that can be overridden by call to Post.")]
		public string Key;

		[Tooltip("A default user that can be overridden by call to Post")]
		public string User;

		[Tooltip("Upload Editor.log when Post is called")]
		public bool CaptureEditorLog;

		[Tooltip("Upload Player.log when Post is called")]
		public bool CapturePlayerLog;

		[Tooltip("Take a screenshot and upload it when Post is called")]
		public bool CaptureScreenshots;

		[Tooltip("Should BugSplat upload exceptions when in editor")]
		public bool PostExceptionsInEditor = true;

		[Tooltip("Paths to files (relative to Application.persistentDataPath) to upload with each report")]
		public List<string> PersistentDataFileAttachmentPaths;

		[Tooltip("Oath2 Application generated Client ID.")]
		public string ClientId;

		[Tooltip("Oath2 Application generated Client Secret.")]
		public string ClientSecret;
	}
}