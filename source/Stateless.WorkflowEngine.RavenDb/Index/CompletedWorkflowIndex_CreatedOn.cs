using Raven.Client.Documents.Indexes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.RavenDb.Index
{
    public class CompletedWorkflowIndex_CreatedOn : AbstractIndexCreationTask<RavenCompletedWorkflow>
    {

        public class Result
        {
            public DateTime CreatedOn { get; set; }
        }

        public CompletedWorkflowIndex_CreatedOn()
        {
            Map = workflows => from wf in workflows
                            select new Result
                            {
                                CreatedOn = wf.Workflow.CreatedOn
                            };
        }

    }
}
