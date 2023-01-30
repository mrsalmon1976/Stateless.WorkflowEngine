using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.MongoDb
{
    [BsonIgnoreExtraElements]
    public class MongoWorkflow
    {
        public MongoWorkflow() { }
        public MongoWorkflow(Workflow workflow)
        {
            this.Id = workflow.Id;
            this.Workflow = workflow;
            this.WorkflowType = workflow.GetType().AssemblyQualifiedName;
        }

        public Guid Id { get; set; }

        public Workflow Workflow { get; set; }

        public string WorkflowType { get; set; }
    }
}
