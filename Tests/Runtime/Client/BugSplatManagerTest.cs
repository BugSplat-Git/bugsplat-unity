using BugSplatUnity.Runtime.Client;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Client
{
    public class BugSplatManagerTest
    {
        private readonly string configureBugSplatMethodName = "ConfigureBugSplat";
        private readonly string bugSplatOptionsVariableName = "bugSplatOptions";

        private MethodInfo configureBugSplatMethod;
        private BugSplatManager bugSplatManager;
        private Type scriptType;

        [SetUp]
        public void Setup()
        {
            GameObject rootGameObject = new GameObject();
            bugSplatManager = rootGameObject.AddComponent<BugSplatManager>();
            scriptType = bugSplatManager.GetType();
            configureBugSplatMethod = scriptType.GetMethod(configureBugSplatMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [Test]
        public void ConfigureBugSplat_WhenBugSplatOptionsIsNull_ShouldThrowInvalidOperationException()
		{
            try
            {
                configureBugSplatMethod.Invoke(bugSplatManager, new object[] { });
                Assert.Fail();
            }
            catch(Exception ex)
			{
                if (ex.InnerException is InvalidOperationException)
				{
                    return;
				}

                Assert.Fail();
			}
        }

        [Test]
        public void ConfigureBugSplat_WhenBugSplatOptionsIsNotNull_ShouldNotThrowException()
		{
            var fakeBugsplatOptions = ScriptableObject.CreateInstance<BugSplatOptions>();
            fakeBugsplatOptions.Database = "database";

            var so = new SerializedObject(bugSplatManager);
            so.FindProperty(bugSplatOptionsVariableName).objectReferenceValue = fakeBugsplatOptions;
            so.ApplyModifiedProperties();

            try
            {
                configureBugSplatMethod.Invoke(bugSplatManager, new object[] { });
                return;
            }
            catch
            {
                Assert.Fail();
            }
        }

        [Test]
        public void BugSplat_WhenBugSplatOptionsIsNotNull_ShouldBeNonNull()
        {
            var fakeBugsplatOptions = ScriptableObject.CreateInstance<BugSplatOptions>();
            fakeBugsplatOptions.Database = "database";

            var so = new SerializedObject(bugSplatManager);
            so.FindProperty(bugSplatOptionsVariableName).objectReferenceValue = fakeBugsplatOptions;
            so.ApplyModifiedProperties();

            configureBugSplatMethod.Invoke(bugSplatManager, new object[] { });

            Assert.NotNull(bugSplatManager.BugSplat);
        }

        [Test]
        public void BugSplat_WhenBugSplatOptionsIsNull_ShouldBeNull()
        {
            try
            {
                configureBugSplatMethod.Invoke(bugSplatManager, new object[] { });
                Assert.Fail();
            }
            catch
            {
                Assert.Null(bugSplatManager.BugSplat);
            }           
        }
    }
}

