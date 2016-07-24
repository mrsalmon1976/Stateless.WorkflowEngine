using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Data.Models
{
    public class UserModel
    {
        public UserModel()
        {
            this.Connections = new List<ConnectionModel>();
        }

        public Guid Id { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string Role { get; set; }

        public List<ConnectionModel> Connections { get; set; }
    }
}
