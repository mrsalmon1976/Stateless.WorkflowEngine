using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Models;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    public interface IWebVersionService
    {
        Task<WebConsoleVersionInfo> GetVersionInfo();
    }

    public class WebVersionService : IWebVersionService
    {

        private readonly IAppSettings _appSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        public WebVersionService(IAppSettings appSettings, IHttpClientFactory httpClientFactory)
        {
            _appSettings = appSettings;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<WebConsoleVersionInfo> GetVersionInfo()
        {
            string url = _appSettings.LatestVersionUrl;
            HttpClient client = _httpClientFactory.GetHttpClient();
            var result = await client.GetAsync(url);
            result.EnsureSuccessStatusCode();
            var body = await result.Content.ReadAsStringAsync();
            GitHubReleaseResponse releaseData = JsonConvert.DeserializeObject<GitHubReleaseResponse>(body);
            WebConsoleVersionInfo versionInfo = new WebConsoleVersionInfo();
            versionInfo.VersionNumber = releaseData.TagName;

            GitHubAsset asset = releaseData.GetWebConsoleAsset();
            versionInfo.FileName = asset.Name;
            versionInfo.DownloadUrl = asset.DownloadUrl;
            versionInfo.Length = asset.Size;

            return versionInfo;
        }
    }
}
