using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Models.Workflow
{
    [BsonIgnoreExtraElements]
    public class Workflow
    {
        [BsonElement("_id")]
        public Guid Id { get; set; }

        public int RetryCount { get; set; }

        public int[] RetryIntervals { get; set; }

        public DateTime? ResumeOn { get; set; }

        public bool IsSuspended { get; set; }
    }
}
