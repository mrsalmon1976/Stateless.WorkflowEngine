using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Commands
{
    public interface ICommand
    {
    }

    public abstract class Command<T> : ICommand
    {
        public abstract T Execute();

        public virtual void Validate() {
        }
    }
}
