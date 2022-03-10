using Raven.Client.Indexes;
using Stateless.WorkflowEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.RavenDb.Index
{
    public class WorkflowIndex_Priority_RetryCount_CreatedOn : AbstractIndexCreationTask<WorkflowContainer>
    {

        public class Result
        {
            public int Priority { get; set; }

            public int RetryCount { get; set; }

            public DateTime CreatedOn { get; set; }
        }

        public WorkflowIndex_Priority_RetryCount_CreatedOn()
        {
            Map = workflows => from wf in workflows
                            select new Result
                            {
                                Priority = wf.Workflow.Priority,
                                RetryCount = wf.Workflow.RetryCount,
                                CreatedOn = wf.Workflow.CreatedOn
                            };
        }

    }
}
