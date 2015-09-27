using Stateless.WorkflowEngine.UI.Console.AppCode.Factories;
using Stateless.WorkflowEngine.UI.Console.AppCode.Models.Workflow;
using Stateless.WorkflowEngine.UI.Console.AppCode.Providers;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using Stateless.WorkflowEngine.UI.Console.Services.Workflow;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.AppCode.Services
{
    public interface IUIConnectionService
    {
        void RunAsyncConnection(WorkflowStoreConnection conn, Action<ConnectionResult> callback);
    }

    public class UIConnectionService : IUIConnectionService
    {

        private Action<ConnectionResult> _callback;
        private UserSettings _userSettings;
        private IWorkflowProviderFactory _workflowProviderFactory;

        public UIConnectionService(UserSettings userSettings, IWorkflowProviderFactory workflowProviderFactory)
        {
            _userSettings = userSettings;
            _workflowProviderFactory = workflowProviderFactory;
        }

        public void RunAsyncConnection(WorkflowStoreConnection conn, Action<ConnectionResult> callback)
        {
            _callback = callback;

            using (BackgroundWorker bgw = new BackgroundWorker())
            {
                bgw.DoWork += OnBackgroundConnectionDoWork;
                bgw.RunWorkerCompleted += OnBackgroundConnectionRunWorkerCompleted;
                bgw.RunWorkerAsync(conn);
            };
        }

        private void OnBackgroundConnectionRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Exception ex = e.Result as Exception;
            IWorkflowProvider provider = e.Result as IWorkflowProvider;

            ConnectionResult result = new ConnectionResult();
            if (ex != null)
            {
                result.Exception = ex;
            }
            else if (provider != null)
            {
                result.WorkflowProvider = provider;

                // add the connection to the user settings and save
                if (!_userSettings.Connections.Contains(provider.Connection))
                {
                    _userSettings.Connections.Add(provider.Connection);
                    _userSettings.Save();
                }

            }

            _callback(result);

        }

        private void OnBackgroundConnectionDoWork(object sender, DoWorkEventArgs e)
        {
            // try and connect
            try
            {
                WorkflowStoreConnection conn = (WorkflowStoreConnection)e.Argument;
                IWorkflowProvider provider = _workflowProviderFactory.GetWorkflowService(conn);
                provider.GetActive(1);

                e.Result = provider;
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

    }
}
