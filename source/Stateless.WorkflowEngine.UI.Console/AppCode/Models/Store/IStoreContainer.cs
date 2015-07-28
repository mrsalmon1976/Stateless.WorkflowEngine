using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Models.Store
{
    public interface IStoreContainer
    {
        IWorkflowStore GetStore();

        string Name { get; }
    }
}
