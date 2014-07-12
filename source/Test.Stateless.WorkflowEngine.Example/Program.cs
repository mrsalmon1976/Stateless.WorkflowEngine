using Stateless.WorkflowEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            //BootStrapper.Boot();
            ProjectInstaller.ServiceName = "Workflow Engine Example";

            if (!Environment.UserInteractive && args.Length == 0)
            {
                ServiceBase.Run(new WorkflowEngineExampleService());
            }
            else
            {
                string argument = GetArgument(args);

                switch (argument)
                {
                    case "install":
                        WindowsServiceController.Install();
                        System.Console.WriteLine();
                        System.Console.WriteLine("Install command executed. Press enter to quit.");
                        System.Console.ReadLine();
                        break;

                    case "installandstart":
                        WindowsServiceController.InstallAndStart();
                        break;

                    case "restart":
                        WindowsServiceController.Restart();
                        break;

                    case "start":
                        WindowsServiceController.Start();
                        break;

                    case "stop":
                        WindowsServiceController.Stop();
                        break;

                    case "uninstall":
                        WindowsServiceController.EnsureStoppedAndUninstall();
                        System.Console.WriteLine();
                        System.Console.WriteLine("Uninstall command executed. Press enter to quit.");
                        System.Console.ReadLine();
                        break;

                    case "run":
                        WorkflowEngineExampleService service = new WorkflowEngineExampleService();
                        service.Start();
                        System.Console.WriteLine("Running. Press enter to stop.");
                        System.Console.ReadLine();
                        break;

                    default:
                        PrintUsage(argument);
                        break;
                }
            }
        }

        private static string GetArgument(string[] args)
        {
            if (args.Length == 0)
                return "debug";
            if (args[0].StartsWith("/") == false)
                return "help";
            return args[0].Substring(1).ToLower();
        }

        /// <summary>
        /// Prints the usage.
        /// </summary>
        private static void PrintUsage(string previousCommand)
        {
            Console.Clear();

            if (!String.IsNullOrEmpty(previousCommand) && !previousCommand.Equals("debug", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine("Invalid command! Please see a list of valid commands below.");
                Console.WriteLine();
            }

            string friendlyName = System.AppDomain.CurrentDomain.FriendlyName;
            System.Console.WriteLine(String.Format(MANUAL, friendlyName));
            string command = Console.ReadLine();
            Console.WriteLine();
            Main(new string[] { command });
        }

        public const string MANUAL = @"{0}
----------------------------------------
Command line options:

    /installandstart    - installs and starts the service
    /install            - installs the service
    /uninstall          - stops and uninstalls the service
    /start              - starts the previously installed service
    /stop               - stops the previously installed service
    /restart	        - restarts the previously installed service
    /run                - runs the service as a console application

Please enter a command:
";
    }
}
