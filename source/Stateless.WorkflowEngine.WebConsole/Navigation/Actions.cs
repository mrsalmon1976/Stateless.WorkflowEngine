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
            public const string Delete = "/connection/delete";
            public const string Info = "/connection/info";
            public const string List = "/connection/list";
            public const string Save = "/connection/save";
            public const string Test = "/connection/test";
        }

        public class Login
        {
            public const string Default = "/login";
            public const string Logout = "/logout";
        }

        public class Store
        {
            public const string Default = "/store";

            public const string List = "/store/list";

            public const string Remove = "/store/remove";

            public const string Suspend = "/store/suspend";
            
            public const string Unsuspend = "/store/unsuspend";
            
            public const string Workflow = "/store/workflow";

        }

        public class User
        {
            public const string Default = "/user";

            public const string ChangePassword = "/user/changepassword";

            public const string List = "/user/list";

            public const string Save = "/user/save";
        }
    }
}
