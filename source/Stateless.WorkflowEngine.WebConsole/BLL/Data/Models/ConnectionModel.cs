using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Data.Models
{
    public class ConnectionModel
    {
        public ConnectionModel()
        {
            this.Id = Guid.NewGuid();
        }

        public Guid Id { get; set; }

        public WorkflowStoreType WorkflowStoreType { get; set; }

        public string Host { get; set; }

        public string Database { get; set; }

        public string User { get; set; }

        public string Password { get; set; }

        /// <summary>
        /// Gets/sets the key used to generate the password (Base-64-encoded byte array)
        /// </summary>
        public string Key { get; set; }

        public int? Port { get; set; }
        
        public string ActiveCollection { get; set; }
        
        public string CompletedCollection { get; set; }

        public string WorkflowDefinitionCollection { get; set; }
    }
}
