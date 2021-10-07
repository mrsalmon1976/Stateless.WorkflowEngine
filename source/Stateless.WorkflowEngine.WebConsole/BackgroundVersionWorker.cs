using NLog;
using Stateless.WorkflowEngine.WebConsole.BLL.Caching;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Stateless.WorkflowEngine.WebConsole
{
    /*
    public interface IBackgroundVersionWorker : IDisposable
    {
        void Start();
    }

    public class BackgroundVersionWorker : IBackgroundVersionWorker
    {
        private IVersionCheckService _versionCheckService;
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private BackgroundWorker _worker;
        private static bool _isEnabled = true;

        public BackgroundVersionWorker(IVersionCheckService versionCheckService)
        {
            _versionCheckService = versionCheckService;
        }

        public void Start()
        {
            _worker = new BackgroundWorker();
            _worker.DoWork += _worker_DoWork;
            _worker.RunWorkerAsync();
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (_isEnabled)
            {
                VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();
                Console.WriteLine("Background version worker ticking");
                _logger.Info("Background version worker ticking");
                Thread.Sleep(5000);
            }
        }

        public void Dispose()
        {
            _isEnabled = false;
            _logger.Info("Background version worker disposing");
            _worker.Dispose();
        }
    }
    */
}
