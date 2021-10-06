using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests.Reporter
{
    class WebGLReporterTests
    {
        [Test]
        public void LogMessageReceived_WhenTypeNotException_ShouldNotCallPost()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void LogMessageReceived_WhenShouldPostExceptionFalse_ShouldNotCallPost()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void LogMessageReceived_WhenExceptionAndShouldPost_ShouldCallPostWithStackTraceAndOptions()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WhenShouldPostExceptionFalse_ShouldNotCallPost()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WithoutOptions_ShouldCallPostWithExceptionAndDefaultedOptions()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WithOptions_ShouldCallPostWithExceptionAndOverriddenOptions()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WithoutCallback_ShouldNotThrow()
        {
            throw new NotImplementedException();
        }

        [UnityTest]
        public IEnumerator Post_WithCallback_ShouldInvokeCallback()
        {
            throw new NotImplementedException();
        }
    }
}
