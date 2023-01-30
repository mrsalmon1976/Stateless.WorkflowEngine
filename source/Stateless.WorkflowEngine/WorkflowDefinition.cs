using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine
{
    public class WorkflowDefinition
    {
        public WorkflowDefinition() 
        {
            this.Id = Guid.NewGuid();
        } 

        public Guid Id { get; set; }

        public string Name { get; set; }

        public string QualifiedName { get; set; }

        public string Graph { get; set; }

        public DateTime LastUpdatedUtc { get; set; }

    }
}
