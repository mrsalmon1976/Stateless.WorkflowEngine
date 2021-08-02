using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version
{
    public interface IWebVersionChecker
    {
        Task<WebConsoleVersionInfo> GetVersionInfo();
    }

    public class WebVersionChecker : IWebVersionChecker
    {

        private readonly IAppSettings _appSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        public WebVersionChecker(IAppSettings appSettings, IHttpClientFactory httpClientFactory)
        {
            _appSettings = appSettings;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<WebConsoleVersionInfo> GetVersionInfo()
        {
            string url = _appSettings.LatestVersionUrl;
            using (HttpClient client = _httpClientFactory.GetHttpClient())
            {
                var result = await client.GetAsync(url);
                result.EnsureSuccessStatusCode();
                var body = await result.Content.ReadAsStringAsync();
                GitHubReleaseResponse releaseData = JsonConvert.DeserializeObject<GitHubReleaseResponse>(body);
                WebConsoleVersionInfo versionInfo = new WebConsoleVersionInfo();
                versionInfo.VersionNumber = releaseData.TagName;
                return versionInfo;
            }
        }
    }
}
