using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Web
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
                Assert.AreEqual(1, client.DefaultRequestHeaders.Count());
            }
        }
    }
}
