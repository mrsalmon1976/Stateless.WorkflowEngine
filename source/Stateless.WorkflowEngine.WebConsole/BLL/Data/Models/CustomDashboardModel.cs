using System;
using System.Collections.Generic;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Data.Models
{
    public class CustomDashboardModel
    {
        public CustomDashboardModel()
        {
            this.Id = Guid.NewGuid();
            this.ConnectionIds = new List<string>();
            this.WorkflowQualifiedNames = new List<string>();
        }

        public Guid Id { get; set; }

        public string Name { get; set; }

        public List<string> ConnectionIds { get; set; }

        public List<string> WorkflowQualifiedNames { get; set; }
    }
}
