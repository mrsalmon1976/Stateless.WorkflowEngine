using Stateless.WorkflowEngine.WebConsole.Common;
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

        string ApplicationFolder { get; set; }

        string DataFolder { get; }

        string UpdateTempFolder { get; }

        string AutoUpdaterFolder { get; }

        string AutoUpdaterShadowCopyFolder { get; }

        string UpdateEventLogFilePath { get; }

        void DeleteUpdateTempFolder();

        void EnsureEmptyUpdateTempFolderExists();
    }

    public class UpdateLocationService : IUpdateLocationService
    {
        private string _applicationFolder;

        public UpdateLocationService()
        {
            DirectoryInfo di = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            this.ApplicationFolder = di.FullName;
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

                this.BackupFolder = Path.Combine(this._applicationFolder, UpdateConstants.BackupFolderName);
                this.DataFolder = Path.Combine(this._applicationFolder, UpdateConstants.DataFolderName);
                this.UpdateTempFolder = Path.Combine(this._applicationFolder, UpdateConstants.UpdateTempFolderName);
                this.AutoUpdaterFolder = Path.Combine(this._applicationFolder, UpdateConstants.AutoUpdaterFolderName);
                this.AutoUpdaterShadowCopyFolder = Path.Combine(this.ApplicationFolder, UpdateConstants.AutoUpdaterShadowCopyFolderName);
                this.UpdateEventLogFilePath = Path.Combine(this.ApplicationFolder, UpdateConstants.UpdateEventLogFileName);
            }
        }

        public string DataFolder { get; private set; }

        public string UpdateTempFolder { get; private set; }

        public string UpdateEventLogFilePath { get; private set; }

        public string AutoUpdaterFolder { get; private set; }

        public string AutoUpdaterShadowCopyFolder { get; private set; }

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
