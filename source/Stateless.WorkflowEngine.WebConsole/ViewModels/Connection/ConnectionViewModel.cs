using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Connection
{
    public class ConnectionViewModel
    {
        public WorkflowStoreType WorkflowStoreType { get; set; }

        public Guid Id { get; set; }

        public string Host { get; set; }

        public string Database { get; set; }

        public string User { get; set; }

        public string Password { get; set; }

        public string PasswordConfirm { get; set; }

        public int? Port { get; set; }

        public string ActiveCollection { get; set; }

        public string CompletedCollection { get; set; }

        public string ReplicaSet { get; set; }

    }
}
