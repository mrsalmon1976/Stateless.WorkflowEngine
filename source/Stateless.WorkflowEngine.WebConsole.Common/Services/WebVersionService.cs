using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Common.Services
{
    public interface IWebVersionService
    {
        Task<WebConsoleVersionInfo> GetVersionInfo(string latestVersionUrl);
    }

    public class WebVersionService : IWebVersionService
    {

        private readonly IHttpClientFactory _httpClientFactory;

        public WebVersionService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<WebConsoleVersionInfo> GetVersionInfo(string latestVersionUrl)
        {
            HttpClient client = _httpClientFactory.GetHttpClient();
            var result = await client.GetAsync(latestVersionUrl);
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
