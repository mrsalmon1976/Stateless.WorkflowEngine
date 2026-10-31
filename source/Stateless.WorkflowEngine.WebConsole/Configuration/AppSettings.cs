namespace Stateless.WorkflowEngine.WebConsole.Configuration
{
    public interface IAppSettings
    {
        /// <summary>
        /// Gets the absolute path of the folder where users, connections and data protection keys are stored.
        /// </summary>
        string DataPath { get; }

        /// <summary>
        /// Gets the GitHub API url used to check for the latest release.
        /// </summary>
        string LatestVersionUrl { get; }

        /// <summary>
        /// Gets how long the result of the version check is cached for.
        /// </summary>
        int UpdateCheckIntervalInMinutes { get; }
    }

    public class AppSettings : IAppSettings
    {
        public const string DefaultLatestVersionUrl = "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest";

        public AppSettings(IConfiguration configuration, string contentRootPath)
        {
            string dataPath = configuration["DataPath"] ?? "Data";
            DataPath = Path.IsPathRooted(dataPath) ? dataPath : Path.Combine(contentRootPath, dataPath);
            LatestVersionUrl = configuration["LatestVersionUrl"] ?? DefaultLatestVersionUrl;
            UpdateCheckIntervalInMinutes = configuration.GetValue("UpdateCheckIntervalInMinutes", 60);
        }

        public string DataPath { get; }

        public string LatestVersionUrl { get; }

        public int UpdateCheckIntervalInMinutes { get; }
    }
}
