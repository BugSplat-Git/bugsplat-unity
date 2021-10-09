using BugSplatUnity.Runtime.Client;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Client
{
	public class BugSplatManagerTest
	{
		BugSplatConfigurationOptions configurationOptions;
		GameObject managerObject;

		[SetUp]
		public void Setup()
		{
			configurationOptions = new BugSplatConfigurationOptions();
			managerObject = new GameObject();
			managerObject.AddComponent(typeof(BugSplatManager));
		}
	}
}
