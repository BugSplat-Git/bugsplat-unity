using BugSplatUnity.Runtime.Client;
using BugSplatUnity.Runtime.Manager;
using NUnit.Framework;
using System;
using UnityEditor;
using UnityEngine;

namespace BugSplatUnity.RuntimeTests.Manager
{
    public class BugSplatManagerImplTest
    {
        [Test]
        public void Instantiate_WhenBugSplatOptionsIsNull_ShouldThrowArgumentException()
		{
            var bugSplatManagerImpl = new BugSplatManagerImpl(null, false, false, null);

            try
            {
                bugSplatManagerImpl.Instantiate();
                Assert.Fail();
            }
            catch (Exception ex) 
            {
                Assert.True(ex is ArgumentException);
                StringAssert.AreEqualIgnoringCase("BugSplat error: BugSplatOptions is null! BugSplat will not be initialized.", ex.Message);
            }          
        }

        [Test]
        public void Instantiate_WhenBugSplatOptionsIsNotNull_ShouldNotThrowException()
		{
            var fakeBugsplatOptions = ScriptableObject.CreateInstance<BugSplatOptions>();
            fakeBugsplatOptions.Database = "database";

            var bugSplatManagerImpl = new BugSplatManagerImpl(fakeBugsplatOptions, false, false, null);

            try
            {
                bugSplatManagerImpl.Instantiate();
            }
            catch
			{
                Assert.Fail();
			}
        }

        [Test]
        public void Instantiate_WhenBugSplatOptionsIsNotNull_ShouldBeNonNull()
        {
            var fakeBugsplatOptions = ScriptableObject.CreateInstance<BugSplatOptions>();
            fakeBugsplatOptions.Database = "database";

            var bugSplatManagerImpl = new BugSplatManagerImpl(fakeBugsplatOptions, false, false, null);
            bugSplatManagerImpl.Instantiate();

            Assert.NotNull(bugSplatManagerImpl.BugSplat);
        }
    }
}

