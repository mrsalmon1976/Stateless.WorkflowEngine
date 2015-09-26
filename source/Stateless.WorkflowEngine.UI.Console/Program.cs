using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleInjector;
using Stateless.WorkflowEngine.UI.Console.Forms;
using Stateless.WorkflowEngine.UI.Console.Models;
using Stateless.WorkflowEngine.UI.Console.AppCode.Providers;
using System.Windows;
using NLog;
using Stateless.WorkflowEngine.UI.Console.AppCode.Factories;
using Stateless.WorkflowEngine.UI.Console.AppCode.Services;

namespace Stateless.WorkflowEngine.UI.Console
{

    static class Program
    {
        static Logger _logger = LogManager.GetCurrentClassLogger();

        [STAThread]
        static void Main()
        {
            var container = Bootstrap();
            RunApplication(container);
        }

        private static void RunApplication(Container container)
        {
            try
            {
                var app = new App();
                var mainWindow = container.GetInstance<MainWindow>();
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                MessageBox.Show("A fatal error has occurred: " + ex.Message, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private static Container Bootstrap()
        {
            // Create the container as usual.
            var container = new Container();

            // factories
            container.Register<IDialogFactory, DialogFactory>();
            container.Register<IWorkflowProviderFactory, WorkflowProviderFactory>();

            // services
            container.Register<IUIConnectionService, UIConnectionService>();

            // windows and view models:
            container.Register<MainWindow>();
            container.Register<IFormConnection, FormConnection>();

            // models
            container.Register<UserSettings>(() =>
            {
                UserSettings settings = UserSettings.Load();
                return settings;
                }, Lifestyle.Singleton);

            // verify the objects
            container.Verify();

            return container;
        }


    }

}
