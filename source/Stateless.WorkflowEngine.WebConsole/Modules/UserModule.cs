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

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class UserModule : WebConsoleSecureModule
    {
        private IUserStore _userStore;
        private IPasswordProvider _passwordProvider;

        public UserModule(IUserStore userStore, IPasswordProvider passwordProvider) : base()
        {
            _userStore = userStore;
            _passwordProvider = passwordProvider;

            this.RequiresClaims(new[] { Roles.Admin });

            Get[Actions.User.Default] = (x) =>
            {
                AddScript(Scripts.UserView);
                return Default();
            };
            Get[Actions.User.List] = (x) =>
            {
                return List();
            };
            Post[Actions.User.ChangePassword] = (x) =>
            {
                return ChangePassword();
            };
            Post[Actions.User.Default] = (x) =>
            {
                var model = this.Bind<UserViewModel>();
                //return this.HandleResult(userController.HandleUserPost(model, this.Context.CurrentUser));
                throw new NotImplementedException();
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

        //public IControllerResult HandleUserPost(UserViewModel model, IUserIdentity currentUser)
        //{

        //    // do first level validation - if it fails then we need to exit
        //    List<string> validationErrors = this._userViewModelValidator.Validate(model);
        //    if (validationErrors.Count > 0)
        //    {
        //        var vresult = new BasicResult(false, validationErrors.ToArray());
        //        return new JsonResult(vresult);
        //    }

        //    UserEntity user = Mapper.Map<UserViewModel, UserEntity>(model);
        //    // try and execute the command 
        //    BasicResult result = new BasicResult(true);
        //    try
        //    {
        //        _unitOfWork.BeginTransaction();
        //        _saveUserCommand.User = user;
        //        _saveUserCommand.CurrentUserId = ((UserIdentity)currentUser).Id;
        //        _saveUserCommand.CategoryIds = model.CategoryIds;
        //        _saveUserCommand.Execute();
        //        _unitOfWork.Commit();
        //    }
        //    catch (ValidationException vex)
        //    {
        //        result = new BasicResult(false, vex.Errors.ToArray());
        //    }
        //    catch (Exception ex)
        //    {
        //        result = new BasicResult(false, ex.Message);
        //    }

        //    return new JsonResult(result);
        //}


    }
}
