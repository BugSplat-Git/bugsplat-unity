using BugSplatUnity.Runtime.Client;
using NUnit.Framework;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Client
{
	public class BugSplatManagerTest
	{
		[Test]
		public void BugSplatManager_WillRegisterCallback_WhenRegisterLogMessageRecievedConfigurationOption_IsTrue()
		{
			var configOptions = new BugSplatConfigurationOptions();
			configOptions.RegisterLogMessageRecieved = true;

			var testObj = new GameObject();
			testObj.AddComponent(typeof(BugSplatManager));

		}

		[Test]
		public void BugSplatManager_WillNotCallback_WhenRegisterLogMessageRecievedConfigurationOption_IsFalse()
		{

		}

		[Test]
		public void BugSplatManager_WillDestroyOnLoad_WhenDestroyManagerOnSceneLoadConfigurationOption_IsTrue()
		{

		}

		[Test]
		public void BugSplatManager_WillNotDestroyOnLoad_WhenDestroyManagerOnSceneLoadConfigurationOption_IsFalse()
		{

		}


	}
}
