using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Security
{
    public class UserIdentity : IUserIdentity
    {
        public UserIdentity()
        {
            this.Claims = Enumerable.Empty<string>();
        }

        public IEnumerable<string> Claims { get; set; }

        public string UserName { get; set; }

        public Guid Id { get; set; }

        public string Email
        {
            get { return UserName; }
            set { UserName = value; }
        }

    }
}
