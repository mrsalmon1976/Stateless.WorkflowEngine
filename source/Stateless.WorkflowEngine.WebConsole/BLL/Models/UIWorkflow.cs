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
    public class UIWorkflow : Workflow
    {
        private string _workflowTypeName = String.Empty;

        public string WorkflowType { get; set; }

        public string WorkflowClassName
        {
            get
            {
                string className = this.WorkflowTypeName;
                int loc = className.LastIndexOf(".");
                if (loc > -1)
                {
                    className = className.Substring(loc + 1);
                }
                return className;
            }
        }

        public string WorkflowTypeName
        {
            get
            {
                if (String.IsNullOrEmpty(_workflowTypeName))
                {
                    if (String.IsNullOrWhiteSpace(this.WorkflowType))
                    {
                        return String.Empty;
                    }

                    ParsedAssemblyQualifiedName p = new ParsedAssemblyQualifiedName(this.WorkflowType);
                    _workflowTypeName = p.TypeName;
                }
                return _workflowTypeName;
            }
        }


        //public string ResumeOnFriendly
        //{
        //    get
        //    {
        //        if (!ResumeOn.HasValue || ResumeOn.Value < new DateTime(1900, 1, 1))
        //        {
        //            return "-";
        //        }
        //        return this.ResumeOn.Value.ToString("yyyy-MM-dd HH:mm:ss");
        //    }
        //}

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

        public override void Fire(string triggerName)
        {
            // does nothing!
        }

        public override void Initialise(string initialState)
        {
            // does nothing!
        }
    }
}
