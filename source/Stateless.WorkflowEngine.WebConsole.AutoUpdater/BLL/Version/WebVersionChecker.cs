using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Models;
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
        private readonly HttpClient _client;

        public WebVersionChecker(IAppSettings appSettings, HttpClient client)
        {
            _appSettings = appSettings;
            _client = client;
            _client.DefaultRequestHeaders.Add("User-Agent", "Stateless.WorkflowEngine.WebConsole.AutoUpdater");
        }

        public HttpClient HttpClient {  get { return _client; } }

        public async Task<WebConsoleVersionInfo> GetVersionInfo()
        {
            string url = _appSettings.LatestVersionUrl;
            var result = await _client.GetAsync(url);
            result.EnsureSuccessStatusCode();
            var body = await result.Content.ReadAsStringAsync(); 
            dynamic releaseData = JsonConvert.DeserializeObject(body);
            WebConsoleVersionInfo versionInfo = new WebConsoleVersionInfo();
            versionInfo.VersionNumber = releaseData.tag_name;
            return versionInfo;
        }
    }
}
