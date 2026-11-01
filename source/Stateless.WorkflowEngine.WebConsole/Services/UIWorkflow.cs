using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine.MongoDb;

namespace Stateless.WorkflowEngine.WebConsole.Services
{
    /// <summary>
    /// Lightweight representation of a workflow for display purposes - this allows workflows to be read
    /// without the console needing a reference to the assemblies containing the workflow types.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class UIWorkflow
    {
        public DateTime CreatedOn { get; set; }

        public string? CurrentState { get; set; }

        [BsonGuidRepresentation(MongoDbWorkflowStore.IdGuidRepresentation)]
        public Guid Id { get; set; }

        public bool IsSuspended { get; set; }

        public string? Name { get; set; }

        public int Priority { get; set; }

        public string? QualifiedName { get; set; }

        public DateTime ResumeOn { get; set; }

        public string? ResumeTrigger { get; set; }

        public int RetryCount { get; set; }

        public int[]? RetryIntervals { get; set; }

        public string? WorkflowGraph { get; set; }
    }

    /// <summary>
    /// Wrapper class for WorkflowContainer.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class UIWorkflowContainer
    {
        public string? WorkflowType { get; set; }

        public UIWorkflow? Workflow { get; set; }
    }
}
