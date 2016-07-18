using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Login
{
    public class LoginViewModel
    {
        public string ReturnUrl { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public bool Success { get; set; }
    }
}
