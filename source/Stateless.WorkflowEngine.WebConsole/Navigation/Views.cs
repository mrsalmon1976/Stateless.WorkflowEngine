using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Navigation
{
    public class Views
    {
        public const string Login = "Views/LoginView.cshtml";

        public class Connection
        {
            public const string List = "Views/Connection/_ConnectionList.cshtml";
        }

        public class Dashboard
        {
            public const string Default = "Views/Dashboard/DashboardView.cshtml";
        }

        public class User
        {
            public const string ListPartial = "Views/User/_UserList.cshtml";
            public const string Default = "Views/User/UserView.cshtml";
        }
    }
}
