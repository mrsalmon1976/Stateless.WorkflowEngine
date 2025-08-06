using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Models
{
    public class GitHubReleaseResponse
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("assets")]
        public GitHubAsset[] Assets { get; set; }

        public GitHubAsset GetWebConsoleAsset()
        {
            if (this.Assets == null || this.Assets.Length == 0)
            {
                return null;
            }
            return this.Assets.FirstOrDefault(x => x.Name.StartsWith("Stateless.WorkflowEngine.WebConsole"));
        }

    }
}
