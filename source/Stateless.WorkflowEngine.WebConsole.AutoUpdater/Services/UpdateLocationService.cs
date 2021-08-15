using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    public interface IUpdateLocationService
    {
        string BackupFolder { get; }

        string ApplicationFolder { get; }

        string DataFolder { get; }

        string UpdateTempFolder { get; }

        string UpdateEventLogFilePath { get; }

        void DeleteUpdateTempFolder();

        void EnsureEmptyUpdateTempFolderExists();
    }

    public class UpdateLocationService : IUpdateLocationService
    {
        private string _applicationFolder;

        public UpdateLocationService()
        {
            this.ApplicationFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // TODO : remove!
            this.ApplicationFolder = "C:\\Temp\\Stateless";
        }

        public string BackupFolder { get; private set; }

        public string ApplicationFolder
        {
            get
            {
                return _applicationFolder;
            }
            set
            {
                this._applicationFolder = value;

                this.BackupFolder = Path.Combine(this._applicationFolder, "Backup");
                this.DataFolder = Path.Combine(this._applicationFolder, "Data");
                this.UpdateTempFolder = Path.Combine(this._applicationFolder, "__UpdateTemp");
                this.UpdateEventLogFilePath = Path.Combine(this.ApplicationFolder, "UpdateLog.log");
            }
        }

        public string DataFolder { get; private set; }

        public string UpdateTempFolder { get; private set; }
        public string UpdateEventLogFilePath { get; private set; }

        public void DeleteUpdateTempFolder()
        {
            if (Directory.Exists(this.UpdateTempFolder))
            {
                Directory.Delete(this.UpdateTempFolder, true);
            }
        }

        public void EnsureEmptyUpdateTempFolderExists()
        {
            this.DeleteUpdateTempFolder();
            Directory.CreateDirectory(this.UpdateTempFolder);
        }
    }
}
