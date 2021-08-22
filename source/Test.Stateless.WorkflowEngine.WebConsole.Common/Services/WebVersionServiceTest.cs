using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using Stateless.WorkflowEngine.WebConsole.Common.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.WebConsole.Common.MockUtils.Web;

namespace Test.Stateless.WorkflowEngine.WebConsole.Common.Services
{
    [TestFixture]
    public class WebVersionServiceTest
    {

        private const string GitHubLatestReleaseUrl = "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest";

        [Test]
        public void GetVersionInfo_OnRequest_BindResponseValuesCorrectly()
        {
            // setup
            IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

            string response = GetSampleGitHubReleaseJson();
            MockHttpMessageHandler httpMessageHandler = new MockHttpMessageHandler(HttpStatusCode.OK, response);
            HttpClient client = new HttpClient(httpMessageHandler);
            httpClientFactory.GetHttpClient().Returns(client);


            // execute
            IWebVersionService webVersionService = new WebVersionService(httpClientFactory);
            var result = webVersionService.GetVersionInfo(GitHubLatestReleaseUrl).GetAwaiter().GetResult();

            // assert
            Assert.AreEqual("2.2.1", result.VersionNumber);
        }

        [Test]
        public void GetVersionInfo_Integration_GetsLatestReleaseDataFromGitHub()
        {
            IWebVersionService webVersionService = new WebVersionService(new HttpClientFactory());
            var result = webVersionService.GetVersionInfo(GitHubLatestReleaseUrl).GetAwaiter().GetResult();
            string versionNumber = result.VersionNumber;
            System.Version version = System.Version.Parse(versionNumber);
            Assert.GreaterOrEqual(version.Major, 2);
        }

        private string GetSampleGitHubReleaseJson()
        {
            // Determine path
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream("Test.Stateless.WorkflowEngine.WebConsole.Common.Services.GitHubLatestVersionSample.json"))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
