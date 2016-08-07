using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Validators
{
    public interface IUserValidator
    {
        ValidationResult Validate(UserModel model);
    }

    public class UserValidator : IUserValidator
    {
        private IUserStore _userStore;

        public UserValidator(IUserStore userStore)
        {
            this._userStore = userStore;
        }

        public ValidationResult Validate(UserModel model)
        {
            ValidationResult result = new ValidationResult();

            if (String.IsNullOrWhiteSpace(model.UserName))
            {
                result.Messages.Add("User name cannot be empty");
            }
            if (String.IsNullOrWhiteSpace(model.Password))
            {
                result.Messages.Add("Password cannot be empty");
            }
            if (String.IsNullOrWhiteSpace(model.Role))
            {
                result.Messages.Add("Role cannot be empty");
            }

            UserModel existinguser = _userStore.GetUser(model.UserName);
            if (existinguser != null && existinguser.Id != model.Id) 
            {
                result.Messages.Add("A user with the supplied user name already exists.");
            }

            return result;
        }
    }
}
