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
        //private string _workflowTypeName = String.Empty;

        //public string WorkflowType { get; set; }

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

        //public string WorkflowClassName
        //{
        //    get
        //    {
        //        string className = this.WorkflowTypeName;
        //        int loc = className.LastIndexOf(".");
        //        if (loc > -1)
        //        {
        //            className = className.Substring(loc + 1);
        //        }
        //        return className;
        //    }
        //}

        //public string WorkflowTypeName
        //{
        //    get
        //    {
        //        if (String.IsNullOrEmpty(_workflowTypeName))
        //        {
        //            if (String.IsNullOrWhiteSpace(this.WorkflowType))
        //            {
        //                return String.Empty;
        //            }

        //            ParsedAssemblyQualifiedName p = new ParsedAssemblyQualifiedName(this.WorkflowType);
        //            _workflowTypeName = p.TypeName;
        //        }
        //        return _workflowTypeName;
        //    }
        //}

        //public string LastExceptionFriendly 
        //{
        //    get
        //    {
        //        string result = this.LastException ?? "";
        //        int idx = result.IndexOf('\n');
        //        if (idx > -1)
        //        {
        //            return result.Substring(0, idx).Trim();
        //        }
        //        return result;
        //    }
        //}


    }
}
