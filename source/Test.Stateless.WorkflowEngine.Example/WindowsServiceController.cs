using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Reflection;
using System.Configuration.Install;

namespace Test.Stateless.WorkflowEngine.Example
{
	public class WindowsServiceController
	{
		public static void Install()
		{
			if (ServiceIsInstalled())
			{
				System.Console.WriteLine("Service is already installed");
			}
			else
			{
				ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
			}
		}

		/// <summary>
		/// Installs the service and start.
		/// </summary>
		public static void InstallAndStart()
		{
			Install();
			Start();
		}

		public static void Restart()
		{
			if (ServiceIsInstalled())
			{
				System.Console.WriteLine("Stopping...");
				Stop();
				System.Console.WriteLine("Starting...");
				Start();
			}
			else
			{
				System.Console.WriteLine("Service is not installed");
			}
		}

		/// <summary>
		/// Starts this instance.
		/// </summary>
		public static void Start()
		{
			if (ServiceIsInstalled())
			{
				var startController = new ServiceController(ProjectInstaller.ServiceName);
				startController.Start();
			}
			else
			{
				System.Console.WriteLine("Service is not installed");
			}
		}

		/// <summary>
		/// Stops this instance.
		/// </summary>
		public static void Stop()
		{
			if (ServiceIsInstalled() == false)
			{
				System.Console.WriteLine("Service is not installed");
			}
			else
			{
                var stopController = new ServiceController(ProjectInstaller.ServiceName);

				if (stopController.Status == ServiceControllerStatus.Running)
				{
					stopController.Stop();

					while (stopController.Status != ServiceControllerStatus.Stopped)
					{
						System.Threading.Thread.Sleep(500);
						stopController.Refresh();
					}
				}
			}
		}

		/// <summary>
		/// Ensures the service is stopped and uninstall.
		/// </summary>
		public static void EnsureStoppedAndUninstall()
		{
			if (ServiceIsInstalled() == false)
			{
				System.Console.WriteLine("Service is not installed");
			}
			else
			{
                var stopController = new ServiceController(ProjectInstaller.ServiceName);

				if (stopController.Status == ServiceControllerStatus.Running)
					stopController.Stop();

				ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
			}
		}

		/// <summary>
		/// Checks that the service is installed.
		/// </summary>
		/// <returns></returns>
		public static bool ServiceIsInstalled()
		{
            return (ServiceController.GetServices().Count(s => s.ServiceName == ProjectInstaller.ServiceName) > 0);
		}
	}
}
