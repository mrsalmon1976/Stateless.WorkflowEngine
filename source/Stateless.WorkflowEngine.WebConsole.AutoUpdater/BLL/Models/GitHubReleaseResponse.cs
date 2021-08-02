using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Models
{
    public class GitHubReleaseResponse
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("assets")]
        public Asset[] Assets { get; set; }

        public class Asset
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("browser_download_url")]
            public string DownloadUrl { get; set; }

            [JsonProperty("size")]
            public int Size { get; set; }

        }

    }
}
