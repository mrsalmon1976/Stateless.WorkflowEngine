using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.Configuration;

namespace Stateless.WorkflowEngine.WebConsole.Services
{
    public class WebConsoleVersionInfo
    {
        public string? VersionNumber { get; set; }

        public string? FileName { get; set; }

        public string? DownloadUrl { get; set; }

        public string? ReleaseUrl { get; set; }
    }

    public class VersionCheckResult
    {
        public string? InstalledVersionNumber { get; set; }

        public bool IsNewVersionAvailable { get; set; }

        public string? LatestReleaseVersionNumber { get; set; }

        public string? LatestReleaseUrl { get; set; }
    }

    public class GitHubReleaseResponse
    {
        [JsonProperty("tag_name")]
        public string? TagName { get; set; }

        [JsonProperty("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonProperty("assets")]
        public GitHubAsset[]? Assets { get; set; }

        public GitHubAsset? GetWebConsoleAsset()
        {
            return Assets?.FirstOrDefault(x => x.Name != null && x.Name.StartsWith("Stateless.WorkflowEngine.WebConsole", StringComparison.OrdinalIgnoreCase));
        }
    }

    public class GitHubAsset
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("browser_download_url")]
        public string? DownloadUrl { get; set; }
    }

    public interface IWebConsoleVersionService
    {
        string GetWebConsoleVersion();

        Task<WebConsoleVersionInfo> GetLatestVersion(string latestVersionUrl);
    }

    public class WebConsoleVersionService : IWebConsoleVersionService
    {
        public const string HttpClientName = "GitHub";

        private readonly IHttpClientFactory _httpClientFactory;

        public WebConsoleVersionService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public string GetWebConsoleVersion()
        {
            return typeof(WebConsoleVersionService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        public async Task<WebConsoleVersionInfo> GetLatestVersion(string latestVersionUrl)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(latestVersionUrl);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            GitHubReleaseResponse release = JsonConvert.DeserializeObject<GitHubReleaseResponse>(body) ?? new GitHubReleaseResponse();
            GitHubAsset? asset = release.GetWebConsoleAsset();

            return new WebConsoleVersionInfo
            {
                VersionNumber = release.TagName,
                ReleaseUrl = release.HtmlUrl,
                FileName = asset?.Name,
                DownloadUrl = asset?.DownloadUrl
            };
        }
    }

    public interface IVersionCheckService
    {
        Task<VersionCheckResult> CheckIfNewVersionAvailable();
    }

    public class VersionCheckService : IVersionCheckService
    {
        public const string CacheKey = "CheckIfNewVersionAvailable";

        /// <summary>
        /// How long a failed check is cached before GitHub is asked again. Unauthenticated GitHub API calls share a
        /// quota per IP address, so retrying on every page load can exhaust it.
        /// </summary>
        public static readonly TimeSpan FailureCacheInterval = TimeSpan.FromMinutes(10);

        private readonly IAppSettings _appSettings;
        private readonly IMemoryCache _memoryCache;
        private readonly IWebConsoleVersionService _webConsoleVersionService;

        public VersionCheckService(IAppSettings appSettings, IMemoryCache memoryCache, IWebConsoleVersionService webConsoleVersionService)
        {
            _appSettings = appSettings;
            _memoryCache = memoryCache;
            _webConsoleVersionService = webConsoleVersionService;
        }

        public async Task<VersionCheckResult> CheckIfNewVersionAvailable()
        {
            if (_memoryCache.TryGetValue(CacheKey, out VersionCheckResult? cached) && cached != null)
            {
                return cached;
            }

            string installedVersion = _webConsoleVersionService.GetWebConsoleVersion();
            WebConsoleVersionInfo latest;
            try
            {
                latest = await _webConsoleVersionService.GetLatestVersion(_appSettings.LatestVersionUrl);
            }
            catch
            {
                // cache the failure briefly so the next page loads don't call GitHub again; the caller still sees the exception
                _memoryCache.Set(CacheKey, new VersionCheckResult { InstalledVersionNumber = installedVersion }, FailureCacheInterval);
                throw;
            }

            var result = new VersionCheckResult
            {
                InstalledVersionNumber = installedVersion,
                LatestReleaseVersionNumber = NormaliseVersion(latest.VersionNumber),
                LatestReleaseUrl = latest.ReleaseUrl
            };
            if (Version.TryParse(installedVersion, out Version? vInstalled) && Version.TryParse(result.LatestReleaseVersionNumber, out Version? vLatest))
            {
                result.IsNewVersionAvailable = vInstalled < vLatest;
            }

            _memoryCache.Set(CacheKey, result, TimeSpan.FromMinutes(_appSettings.UpdateCheckIntervalInMinutes));
            return result;
        }

        /// <summary>
        /// Strips a leading "v" and any pre-release suffix from a release tag e.g. "v5.1.0-beta" becomes "5.1.0".
        /// </summary>
        public static string? NormaliseVersion(string? tag)
        {
            if (String.IsNullOrWhiteSpace(tag)) return tag;
            string version = tag.Trim().TrimStart('v', 'V');
            int dash = version.IndexOf('-');
            return dash > -1 ? version.Substring(0, dash) : version;
        }
    }
}
