using Newtonsoft.Json;
using Stateless.WorkflowEngine.UI.Console.AppCode.Utils;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.AppCode.Providers
{
    public class UserSettings
    {
        public UserSettings()
        {
            this.Connections = new List<WorkflowStoreConnection>();
        }

        public List<WorkflowStoreConnection> Connections { get; set; }

        private static string GetSettingsPath()
        {
            return Path.Combine(AppUtils.BaseDirectory(), "user.settings");
        }

        public static UserSettings Load()
        {
            string path = GetSettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<UserSettings>(json);
            }
            return new UserSettings();
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            string path = GetSettingsPath();
            File.WriteAllText(path, json);
        }

    }
}
