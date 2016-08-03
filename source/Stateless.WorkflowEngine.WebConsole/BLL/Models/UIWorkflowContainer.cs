using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine.WebConsole.BLL.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Models
{
    /// <summary>
    /// Wrapper class for WorkflowContainer so we can bind to the screen easily.
    /// </summary>
    [BsonIgnoreExtraElements] 
    public class UIWorkflowContainer
    {
        public string WorkflowType { get; set; }

        public UIWorkflow Workflow { get; set; }

    }
}
