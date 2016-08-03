using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.MongoDb
{
    [BsonIgnoreExtraElements]
    internal class MongoDbWorkflowContainer
    {
        public MongoDbWorkflowContainer() { }
        public MongoDbWorkflowContainer(MongoDbWorkflow workflow)
        {
            this.Id = workflow.Id;
            this.Workflow = workflow;
            this.WorkflowType = workflow.GetType().AssemblyQualifiedName;
        }

        public Guid Id { get; set; }

        public MongoDbWorkflow Workflow { get; set; }

        public string WorkflowType { get; set; }
    }
}
