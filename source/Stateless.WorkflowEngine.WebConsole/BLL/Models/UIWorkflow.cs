using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine.WebConsole.BLL.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Models
{
    [BsonIgnoreExtraElements]
    public class UIWorkflow
    {
        public virtual DateTime CreatedOn { get; set; }

        public virtual string CurrentState { get; set; }

        public Guid Id { get; set; }

        public bool IsSuspended { get; set; }

        public virtual string Name { get; set; }

        public virtual int Priority { get; set; }

        public virtual string QualifiedName {  get; set; }

        public virtual DateTime ResumeOn { get; set; }

        public virtual string ResumeTrigger { get; set; }

        public virtual int RetryCount { get; set; }

        public virtual int[] RetryIntervals { get; set; }

        public virtual string WorkflowGraph { get; set; }


    }
}
