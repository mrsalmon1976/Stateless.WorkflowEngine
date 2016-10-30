using Nancy;
using Nancy.Authentication.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.ModelBinding;
using Nancy.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.User;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using AutoMapper;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class UserModule : WebConsoleSecureModule
    {
        private IUserStore _userStore;
        private IPasswordProvider _passwordProvider;
        private IUserValidator _userValidator;

        public UserModule(IUserStore userStore, IUserValidator userValidator, IPasswordProvider passwordProvider) : base()
        {
            _userStore = userStore;
            _userValidator = userValidator;
            _passwordProvider = passwordProvider;

            Get[Actions.User.Default] = (x) =>
            {
                AddScript(Scripts.UserView);
                return Default();
            };
            Get[Actions.User.List] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.UserList });
                return List();
            };
            Post[Actions.User.ChangePassword] = (x) =>
            {
                return ChangePassword();
            };
            Post[Actions.User.Save] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.UserAdd });
                return Save();
            };
        }

        public dynamic ChangePassword()
        {
            string password = Request.Form["Password"];
            string confirmPassword = Request.Form["ConfirmPassword"];
            if (password.Length < 6)
            {
                return Response.AsJson<BasicResult>(new BasicResult(false, "Passwords must be at least 6 characters in length"));
            }
            if (password != confirmPassword)
            {
                return Response.AsJson<BasicResult>(new BasicResult(false, "Password and confirmation password do not match"));
            }

            // all ok - update the password
            var currentUser = _userStore.Users.Where(x => x.UserName == this.Context.CurrentUser.UserName).Single();
            currentUser.Password = _passwordProvider.HashPassword(password, _passwordProvider.GenerateSalt());
            _userStore.Save();

            return Response.AsJson<BasicResult>(new BasicResult(true));
        }

        public dynamic Default()
        {
            UserViewModel model = new UserViewModel();
            model.Roles.AddRange(Roles.AllRoles);
            model.SelectedRole = Roles.User;
            return this.View[Views.User.Default, model];

        }


        public dynamic List()
        {
            UserListViewModel model = new UserListViewModel();
            model.Users.AddRange(_userStore.Users);
            return this.View[Views.User.ListPartial, model];
        }

        public dynamic Save()
        {

            var model = this.Bind<UserViewModel>();
            UserModel user = Mapper.Map<UserViewModel, UserModel>(model);

            // do first level validation - if it fails then we need to exit
            ValidationResult validationErrors = this._userValidator.Validate(user);
            if (model.Password != model.ConfirmPassword)
            {
                validationErrors.Messages.Add("Password does not match confirmation password");
            }
            if (validationErrors.Messages.Count > 0)
            {
                var vresult = new BasicResult(false, validationErrors.Messages.ToArray());
                return Response.AsJson(vresult);
            }

            // validation is done - hash the password
            user.Password = _passwordProvider.HashPassword(user.Password, _passwordProvider.GenerateSalt());

            // try and execute the command 
            BasicResult result = new BasicResult(true);
            try
            {
                _userStore.Users.Add(user);
                _userStore.Save();
            }
            //catch (ValidationException vex)
            //{
            //    result = new BasicResult(false, vex.Errors.ToArray());
            //}
            catch (Exception ex)
            {
                result = new BasicResult(false, ex.Message);
            }

            return Response.AsJson(result);
        }


    }
}
