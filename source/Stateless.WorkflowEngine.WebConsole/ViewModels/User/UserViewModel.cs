using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.User
{
    public class UserViewModel : UserModel
    {
        public UserViewModel()
        {
            this.ValidationErrors = new List<string>();
            this.CategoryOptions = new List<MultiSelectItem>();
            this.Roles = new List<string>();
        }

        public string FormAction { get; set; }

        public string ConfirmPassword { get; set; }

        /// <summary>
        /// Determines whether to show the permissions options (role, categories, etc).
        /// </summary>
        public bool IsPermissionPanelVisible { get; set; }

        public List<string> ValidationErrors { get; private set; }

        public List<MultiSelectItem> CategoryOptions { get; private set; }
        
        public List<string> Roles { get; private set; }

        public Guid[] CategoryIds { get; set; }

        public string SelectedRole { get; set; }

    }
}
