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
        }

        public class Connection
        {
            public const string List = "/connection/list";
            public const string Save = "/connection/save";
        }

        public class Login
        {
            public const string Default = "/login";
            public const string Logout = "/logout";
        }

        public class User
        {
            public const string Default = "/user";

            public const string ChangePassword = "/user/changepassword";

            public const string List = "/user/list";

        }
    }
}
