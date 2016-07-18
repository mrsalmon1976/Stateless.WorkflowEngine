using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Security
{
    public static class Roles
    {
        static Roles()
        {
            AllRoles = new List<string>(new string[] { Admin, User }).AsReadOnly();
        }

        public const string Admin = "Admin";
        public const string User = "User";

        public static IReadOnlyList<string> AllRoles { get; private set; }
    }
}
