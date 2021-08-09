using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Update
{
    public interface IUpdateLocationService
    {
        string BackupFolder { get; set; }

        string BaseFolder { get; set; }

        string UpdateTempFolder { get; set; }

        Task DeleteUpdateTempFolder();

        Task EnsureEmptyUpdateTempFolderExists();
    }

    public class UpdateLocationService : IUpdateLocationService
    {
        public UpdateLocationService()
        {
            this.BaseFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            this.BackupFolder = Path.Combine(this.BaseFolder, "Backup");
            this.UpdateTempFolder = Path.Combine(this.BaseFolder, "__UpdateTemp");
        }

        public string BackupFolder { get; set; }

        public string BaseFolder { get; set; }

        public string UpdateTempFolder { get; set; }

        public Task DeleteUpdateTempFolder()
        {
            return Task.Run(() =>
            {
                if (Directory.Exists(this.UpdateTempFolder))
                {
                    Directory.Delete(this.UpdateTempFolder, true);
                }
            });
        }

        public Task EnsureEmptyUpdateTempFolderExists()
        {
            return Task.Run(() =>
            {
                this.DeleteUpdateTempFolder();
                Directory.CreateDirectory(this.UpdateTempFolder);
            });
        }
    }
}
