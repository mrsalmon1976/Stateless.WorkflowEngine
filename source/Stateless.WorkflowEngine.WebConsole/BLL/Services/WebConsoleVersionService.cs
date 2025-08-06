using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Web;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    public interface IWebConsoleVersionService
    {
        string GetWebConsoleVersion();

        Task<WebConsoleVersionInfo> GetLatestVersion(string latestVersionUrl);
    }

    public class WebConsoleVersionService : IWebConsoleVersionService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public WebConsoleVersionService(IHttpClientFactory httpClientFactory) 
        {
            this._httpClientFactory = httpClientFactory;
        }
        public string GetWebConsoleVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        }

        public async Task<WebConsoleVersionInfo> GetLatestVersion(string latestVersionUrl)
        {
            var client = _httpClientFactory.GetHttpClient();

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
