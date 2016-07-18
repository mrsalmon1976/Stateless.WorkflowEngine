using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Navigation
{
    public class Actions
    {
        public class Dashboard
        {
            public const string Default = "/dashboard";
            public const string Connections = "/dashboard/connections";
        }

        public class Login
        {
            public const string Default = "/login";
            public const string Logout = "/logout";
        }

        public class User
        {
            public const string Default = "/user";

            public const string List = "/user/list";

        }
    }
}
