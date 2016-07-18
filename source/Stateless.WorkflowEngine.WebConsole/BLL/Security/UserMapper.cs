using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Security
{
    public class UserMapper : IUserMapper
    {
        private IUserStore _userStore;

        public UserMapper(IUserStore userStore)
        {
            this._userStore = userStore;
        }

        public virtual IUserIdentity GetUserFromIdentifier(Guid identifier, NancyContext context)
        {
            UserIdentity ui = null;
            UserModel user = _userStore.Users.SingleOrDefault(x => x.Id == identifier);
            if (user != null)
            {
                ui = new UserIdentity();
                ui.Id = user.Id;
                ui.Claims = new string[] { Roles.User };
                ui.UserName = user.UserName;
            }
            return ui;
        }
    }
}
