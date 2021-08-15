using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging
{
    public interface IUpdateEventLogger
    {
        string UpdateEventLogFilePath { get; set; }

        void ClearLogFile();

        void Log(string message);

        void LogLine(string message);

    }

    public class UpdateEventLogger : IUpdateEventLogger
    {
        private readonly IUpdateLocationService _updateLocationService;

        public UpdateEventLogger(IUpdateLocationService updateLocationService)
        {
            _updateLocationService = updateLocationService;
        }

        public string UpdateEventLogFilePath { get; set; }

        public void ClearLogFile()
        {
            File.WriteAllText(_updateLocationService.UpdateEventLogFilePath, "");
        }

        public void Log(string message)
        {
            Console.Write(message);
            File.AppendAllText(_updateLocationService.UpdateEventLogFilePath, message);
        }

        public void LogLine(string message)
        {
            string msg = String.Format("{0}{1}", message, Environment.NewLine);
            Log(msg);
        }
    }
}
