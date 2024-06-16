using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.Common.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Common.Web
{
    [TestFixture]
    public class HttpClientFactoryTest
    {
        [Test]
        public void GetHttpClient_OnExecute_SetsDefaultHeaders()
        {
            IHttpClientFactory clientFactory = new HttpClientFactory();
            using (HttpClient client = clientFactory.GetHttpClient())
            {
                Assert.That(client.DefaultRequestHeaders.Count(), Is.EqualTo(1));
            }
        }
    }
}
