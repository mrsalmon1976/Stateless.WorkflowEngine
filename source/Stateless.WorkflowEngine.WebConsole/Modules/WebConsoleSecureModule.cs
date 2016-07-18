using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Security;
using Nancy.Authentication.Forms;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class WebConsoleSecureModule : WebConsoleModule
    {
        public WebConsoleSecureModule()
        {
            this.RequiresAuthentication();
        }

    }
}
