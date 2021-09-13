using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Common.Diagnostics
{
    public interface IProcessWrapper : IDisposable
    {
        ProcessStartInfo StartInfo { get; set; }

        int ExitCode { get; }

        bool Start();

        void WaitForExit();

    }
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;

        public ProcessWrapper()
        {
            _process = new Process();
        }

        public int ExitCode
        {
            get
            {
                return _process.ExitCode;
            }
        }

        public ProcessStartInfo StartInfo 
        { 
            get
            {
                return _process.StartInfo;
            }
            set
            {
                _process.StartInfo = value;
            }
        }

        public void Dispose()
        {
            if (_process != null)
            {
                _process.Dispose();
            }
        }

        public bool Start()
        {
            return _process.Start();
        }

        public void WaitForExit()
        {
            _process.WaitForExit();
        }
    }
}
