namespace Stateless.WorkflowEngine.WebConsole.BLL.Diagnostics
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
