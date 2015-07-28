using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleInjector;
using Stateless.WorkflowEngine.UI.Console.Forms;
using Stateless.WorkflowEngine.UI.Console.Models;
using Stateless.WorkflowEngine.UI.Console.AppCode.Providers;

namespace Stateless.WorkflowEngine.UI.Console
{

    static class Program
    {
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
                //Log the exception and exit
            }
        }

        private static Container Bootstrap()
        {
            // Create the container as usual.
            var container = new Container();

            // windows and view models:
            container.Register<MainWindow>();
            container.Register<FormConnection>();

            // models
            //container.Register<UserSettings>(Lifestyle.Singleton);

            // services
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
