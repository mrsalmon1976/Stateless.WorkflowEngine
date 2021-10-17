using NLog;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Jobs
{
    public class AutoUpdaterCleanupJob : IDisposable
    {
        private BackgroundWorker _worker;
        private readonly string _applicationRootPath;
        private readonly IFileUtility _fileUtility;
        private ILogger _logger = LogManager.GetCurrentClassLogger();

        public AutoUpdaterCleanupJob(string applicationRootPath, IFileUtility fileUtility)
        {
            _applicationRootPath = applicationRootPath;
            this.AutoUpdaterPath = Path.Combine(_applicationRootPath, UpdateConstants.AutoUpdaterFolderName);
            _fileUtility = fileUtility;

            this.RetryTime = 5000;
        }

        /// <summary>
        /// RetryTime in milliseconds if a file cleanup fails
        /// </summary>
        public int RetryTime { get; set; }

        public string AutoUpdaterPath { get; set; }

        public void Dispose()
        {
            if (_worker != null)
            {
                _worker.Dispose();
            }
        }

        public void Start()
        {
            _logger.Info("AutoUpdaterCleanupJob starting...");
            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += CleanUpTempFiles;
            _worker.RunWorkerAsync();
        }

        public void Stop()
        {
            if (_worker != null)
            {
                _worker.CancelAsync();
            }
        }

        public void CleanUpTempFiles(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (e.Cancel)
                {
                    _logger.Info("AutoUpdaterCleanupJob exiting due to cancellation notice");
                    return;
                }

                if (!_fileUtility.DirectoryExists(AutoUpdaterPath))
                {
                    _logger.Info($"AutoUpdaterCleanupJob exiting as AutoUpdater folder {AutoUpdaterPath} does not exist");
                    return;
                }

                string[] tempFiles = _fileUtility.GetFiles(AutoUpdaterPath, SearchOption.AllDirectories, "*" + UpdateConstants.AutoUpdaterNewFileExtension);
                if (tempFiles.Length == 0)
                {
                    // no files found - we can exit
                    _logger.Info($"AutoUpdaterCleanupJob exiting - no more temp files found in {AutoUpdaterPath}");
                    return;
                }

                foreach (string source in tempFiles)
                {
                    try
                    {
                        string target = Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source));
                        _fileUtility.MoveFile(source, target, true);
                        _logger.Info($"Renamed temp file '{source}' to '{target}'");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Failed to rename temp file '{source}': {ex.Message}, sleeping for {RetryTime} milliseconds");
                        Thread.Sleep(RetryTime);
                    }
                }


            }
        }
    }
}
