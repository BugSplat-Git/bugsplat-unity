using BugSplatUnity.Runtime.Client;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests.Reporter.Fakes
{
    class FakeDotNetFeedbackClient : IDotNetStandardFeedbackClient
    {
        public List<FakeFeedbackClientPostCall> Calls { get; } = new List<FakeFeedbackClientPostCall>();

        private readonly Task<HttpResponseMessage> _result;

        public FakeDotNetFeedbackClient(HttpResponseMessage result)
        {
            _result = Task.FromResult(result);
        }

        public FakeDotNetFeedbackClient(Task<HttpResponseMessage> result)
        {
            _result = result;
        }

        public Task<HttpResponseMessage> PostFeedback(string title, string description, IReportPostOptions options = null)
        {
            Calls.Add(
                new FakeFeedbackClientPostCall()
                {
                    Title = title,
                    Description = description,
                    Options = options
                }
            );
            return _result;
        }
    }

    public class FakeFeedbackClientPostCall
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public IReportPostOptions Options { get; set; }
    }
}
