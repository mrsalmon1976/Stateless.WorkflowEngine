using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Common.MockUtils.Web
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _resultBody;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string resultBody)
        {
            _statusCode = statusCode;
            _resultBody = resultBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage() { StatusCode = _statusCode, Content = new StringContent(_resultBody) });
        }
    }
}
