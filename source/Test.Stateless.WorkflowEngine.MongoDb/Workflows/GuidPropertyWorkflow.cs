using System;
using System.Collections.Generic;
using Test.Stateless.WorkflowEngine.Workflows.Basic;

namespace Test.Stateless.WorkflowEngine.MongoDb.Workflows
{
    /// <summary>
    /// Workflow with custom Guid properties, used to test that Guids are serialised in the same format
    /// as previous versions of the store (driver 2.x).
    /// </summary>
    public class GuidPropertyWorkflow : BasicWorkflow
    {
        public Guid CorrelationId { get; set; }

        public Guid? OptionalId { get; set; }

        public List<Guid> RelatedIds { get; set; }
    }
}
