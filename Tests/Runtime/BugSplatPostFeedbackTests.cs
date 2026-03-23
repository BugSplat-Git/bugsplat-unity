using BugSplatUnity.RuntimeTests.Reporter.Fakes;
using NUnit.Framework;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace BugSplatUnity.RuntimeTests
{
    class BugSplatPostFeedbackTests
    {
        [UnityTest]
        public IEnumerator PostFeedback_Success_ShouldInvokeCallbackWithResponse()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var fakeFeedbackClient = new FakeDotNetFeedbackClient(response);
            var sut = new BugSplat("database", "application", "version", false, false);
            sut.feedbackClient = fakeFeedbackClient;

            HttpResponseMessage callbackResult = null;
            var completed = new Task<bool>(() => true);
            yield return sut.PostFeedback("title", "description", callback: (r) =>
            {
                callbackResult = r;
                completed.Start();
            });
            yield return completed.AsCoroutine();

            Assert.IsNotNull(callbackResult);
            Assert.AreEqual(HttpStatusCode.OK, callbackResult.StatusCode);
            Assert.AreEqual(1, fakeFeedbackClient.Calls.Count);
            Assert.AreEqual("title", fakeFeedbackClient.Calls[0].Title);
            Assert.AreEqual("description", fakeFeedbackClient.Calls[0].Description);
        }

        [UnityTest]
        public IEnumerator PostFeedback_WithOptions_ShouldForwardOptions()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var fakeFeedbackClient = new FakeDotNetFeedbackClient(response);
            var sut = new BugSplat("database", "application", "version", false, false);
            sut.feedbackClient = fakeFeedbackClient;

            var options = new ReportPostOptions
            {
                Description = "opt-description",
                Email = "test@bugsplat.com",
                User = "tester"
            };

            HttpResponseMessage callbackResult = null;
            var completed = new Task<bool>(() => true);
            yield return sut.PostFeedback("title", "desc", options, (r) =>
            {
                callbackResult = r;
                completed.Start();
            });
            yield return completed.AsCoroutine();

            Assert.IsNotNull(callbackResult);
            Assert.AreEqual(1, fakeFeedbackClient.Calls.Count);
            Assert.AreEqual(options, fakeFeedbackClient.Calls[0].Options);
        }

        [UnityTest]
        public IEnumerator PostFeedback_NullTitle_ShouldLogErrorAndInvokeCallbackWithNull()
        {
            var fakeFeedbackClient = new FakeDotNetFeedbackClient(new HttpResponseMessage());
            var sut = new BugSplat("database", "application", "version", false, false);
            sut.feedbackClient = fakeFeedbackClient;

            HttpResponseMessage callbackResult = new HttpResponseMessage();
            yield return sut.PostFeedback(null, callback: (r) => callbackResult = r);

            LogAssert.Expect(UnityEngine.LogType.Error, "BugSplat error: PostFeedback title must not be null, empty, or whitespace");
            Assert.IsNull(callbackResult);
            Assert.IsEmpty(fakeFeedbackClient.Calls);
        }

        [UnityTest]
        public IEnumerator PostFeedback_EmptyTitle_ShouldLogErrorAndInvokeCallbackWithNull()
        {
            var fakeFeedbackClient = new FakeDotNetFeedbackClient(new HttpResponseMessage());
            var sut = new BugSplat("database", "application", "version", false, false);
            sut.feedbackClient = fakeFeedbackClient;

            HttpResponseMessage callbackResult = new HttpResponseMessage();
            yield return sut.PostFeedback("  ", callback: (r) => callbackResult = r);

            LogAssert.Expect(UnityEngine.LogType.Error, "BugSplat error: PostFeedback title must not be null, empty, or whitespace");
            Assert.IsNull(callbackResult);
            Assert.IsEmpty(fakeFeedbackClient.Calls);
        }

        [UnityTest]
        public IEnumerator PostFeedback_NullFeedbackClient_ShouldLogErrorAndInvokeCallbackWithNull()
        {
            var sut = new BugSplat("database", "application", "version", false, false);
            sut.feedbackClient = null;

            HttpResponseMessage callbackResult = new HttpResponseMessage();
            yield return sut.PostFeedback("title", callback: (r) => callbackResult = r);

            LogAssert.Expect(UnityEngine.LogType.Error, "BugSplat error: PostFeedback is not supported on this platform");
            Assert.IsNull(callbackResult);
        }

        [UnityTest]
        public IEnumerator PostFeedback_FaultedTask_ShouldLogErrorAndInvokeCallbackWithNull()
        {
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            tcs.SetException(new System.Exception("network failure"));
            var fakeFeedbackClient = new FakeDotNetFeedbackClient(tcs.Task);
            var sut = new BugSplat("database", "application", "version", false, false);
            sut.feedbackClient = fakeFeedbackClient;

            HttpResponseMessage callbackResult = new HttpResponseMessage();
            yield return sut.PostFeedback("title", callback: (r) => callbackResult = r);

            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("BugSplat error posting feedback:.*network failure"));
            Assert.IsNull(callbackResult);
        }

        [UnityTest]
        public IEnumerator PostFeedback_CanceledTask_ShouldLogErrorAndInvokeCallbackWithNull()
        {
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            tcs.SetCanceled();
            var fakeFeedbackClient = new FakeDotNetFeedbackClient(tcs.Task);
            var sut = new BugSplat("database", "application", "version", false, false);
            sut.feedbackClient = fakeFeedbackClient;

            HttpResponseMessage callbackResult = new HttpResponseMessage();
            yield return sut.PostFeedback("title", callback: (r) => callbackResult = r);

            LogAssert.Expect(UnityEngine.LogType.Error, "BugSplat error: PostFeedback task was canceled");
            Assert.IsNull(callbackResult);
        }
    }
}
