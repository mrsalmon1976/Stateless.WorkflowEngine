using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Models.Workflow
{
    [BsonIgnoreExtraElements]
    public class UIWorkflow
    {
        [BsonElement("_id")]
        public Guid Id { get; set; }

        public int RetryCount { get; set; }

        public int[] RetryIntervals { get; set; }

        public string RetryIntervalsFriendly
        {
            get
            {
                return String.Join(",", this.RetryIntervals);
            }
        }

        public DateTime? ResumeOn { get; set; }

        public string ResumeOnFriendly
        {
            get
            {
                if (!ResumeOn.HasValue || ResumeOn.Value < new DateTime(1900, 1, 1))
                {
                    return "-";
                }
                return this.ResumeOn.Value.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        public bool IsSuspended { get; set; }

        public string LastException { get; set; }

        public string LastExceptionFriendly 
        {
            get
            {
                string result = this.LastException ?? "";
                int idx = result.IndexOf('\n');
                if (idx > -1)
                {
                    return result.Substring(0, idx).Trim();
                }
                return result;
            }
        }
    }
}
