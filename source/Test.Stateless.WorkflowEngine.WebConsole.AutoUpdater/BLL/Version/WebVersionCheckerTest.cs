using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.MockUtils.Web;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version
{
    [TestFixture]
    public class WebVersionCheckerTest
    {

        private const string GitHubLatestReleaseUrl = "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest";

        [Test]
        public void Constructor_OnConstruct_SetsDefaultHeaders()
        {
            WebVersionChecker webVersionChecker = new WebVersionChecker(new AppSettings(), new System.Net.Http.HttpClient());
            Assert.AreEqual(1, webVersionChecker.HttpClient.DefaultRequestHeaders.Count());
        }

        [Test]
        public void GetVersionInfo_OnRequest_BindResponseValuesCorrectly()
        {
            // setup
            IAppSettings appSettings = Substitute.For<IAppSettings>();
            appSettings.LatestVersionUrl.Returns(GitHubLatestReleaseUrl);

            string response = GetSampleGitHubReleaseJson();
            MockHttpMessageHandler httpMessageHandler = new MockHttpMessageHandler(HttpStatusCode.OK, response);
            HttpClient client = new HttpClient(httpMessageHandler);
            WebVersionChecker webVersionChecker = new WebVersionChecker(appSettings, client);

            // execute
            var result = webVersionChecker.GetVersionInfo().GetAwaiter().GetResult();

            // assert
            Assert.AreEqual("2.2.1", result.VersionNumber);
        }

        [Test]
        public void GetVersionInfo_Integration_GetsLatestReleaseDataFromGitHub()
        {
            IAppSettings appSettings = Substitute.For<IAppSettings>();
            appSettings.LatestVersionUrl.Returns(GitHubLatestReleaseUrl);

            WebVersionChecker webVersionChecker = new WebVersionChecker(appSettings, new System.Net.Http.HttpClient());
            var result = webVersionChecker.GetVersionInfo().GetAwaiter().GetResult();
            string versionNumber = result.VersionNumber;
            System.Version version = System.Version.Parse(versionNumber);
            Assert.GreaterOrEqual(version.Major, 2);
        }

        private string GetSampleGitHubReleaseJson()
        {
            // Determine path
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream("Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version.GitHubLatestVersionSample.json"))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
