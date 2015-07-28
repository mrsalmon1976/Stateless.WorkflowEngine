using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.AppCode.Utils
{
    public class AppUtils
    {
		public static string AppVersion()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(assembly.Location);
            return String.Format("{0}.{1}.{2}", info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
		}

        public static string BaseDirectory()
        {
            string loc = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(loc);
        }

        public static string CurrentWindowsIdentity()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return (identity == null ? String.Empty : identity.Name);
        }

    }
}
