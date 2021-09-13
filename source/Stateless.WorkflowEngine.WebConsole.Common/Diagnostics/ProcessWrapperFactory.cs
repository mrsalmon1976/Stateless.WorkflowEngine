using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Common.Diagnostics
{
    public interface IProcessWrapperFactory
    {
        IProcessWrapper GetProcess();
    }

    public class ProcessWrapperFactory : IProcessWrapperFactory
    {
        public IProcessWrapper GetProcess()
        {
            return new ProcessWrapper();
        }
    }
}
