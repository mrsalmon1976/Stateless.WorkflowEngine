using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Models.Workflow
{
    [BsonIgnoreExtraElements] 
    public class WorkflowContainer
    {
        public string WorkflowType { get; set; }

        public string WorkflowTypeName
        {
            get
            {
                if (String.IsNullOrWhiteSpace(this.WorkflowType))
                {
                    return String.Empty;
                }

                ParsedAssemblyQualifiedName.ParsedAssemblyQualifiedName p = new ParsedAssemblyQualifiedName.ParsedAssemblyQualifiedName(this.WorkflowType);
                return p.TypeName;

            }
        }

        public Workflow Workflow { get; set; }
    }
}
