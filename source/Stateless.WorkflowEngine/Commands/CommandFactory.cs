using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Commands
{
    public interface ICommandFactory
    {
        T CreateCommand<T>() where T : ICommand;
    }

    public class CommandFactory : ICommandFactory
    {
        public T CreateCommand<T>() where T : ICommand
        {
            return Activator.CreateInstance<T>();
        }

    }
}
