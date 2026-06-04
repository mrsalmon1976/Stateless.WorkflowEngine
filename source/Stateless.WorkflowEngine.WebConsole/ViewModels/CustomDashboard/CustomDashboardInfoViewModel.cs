using System.Collections.Generic;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.CustomDashboard
{
    public class CustomDashboardInfoViewModel
    {
        public CustomDashboardInfoViewModel()
        {
            this.WorkflowTypeCounts = new List<WorkflowTypeCountViewModel>();
        }

        public string ConnectionError { get; set; }

        public List<WorkflowTypeCountViewModel> WorkflowTypeCounts { get; set; }
    }

    public class WorkflowTypeCountViewModel
    {
        public string QualifiedName { get; set; }

        public string ShortName { get; set; }

        public long? ActiveCount { get; set; }

        public long? SuspendedCount { get; set; }

        public long? CompletedCount { get; set; }
    }
}
