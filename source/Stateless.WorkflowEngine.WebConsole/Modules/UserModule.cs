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

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class UserModule : WebConsoleSecureModule
    {
        public UserModule(IRootPathProvider pathProvider) : base()
        {
            //this.RequiresClaims(new[] { Roles.Admin });

            //Get[Actions.User.Default] = (x) =>
            //{
            //    AddScript(Scripts.UserView);
            //    return this.HandleResult(userController.HandleUserGet(Request.Query["id"]));
            //};
            //Get[Actions.User.List] = (x) =>
            //{
            //    return this.HandleResult(userController.HandleUserGetList());
            //};
            //Post[Actions.User.Default] = (x) =>
            //{
            //    var model = this.Bind<UserViewModel>();
            //    return this.HandleResult(userController.HandleUserPost(model, this.Context.CurrentUser));
            //};
        }

        //public IControllerResult HandleUserGet(Guid? userId)
        //{
        //    UserViewModel model = new UserViewModel();
        //    var categories = this._unitOfWork.CategoryRepo.GetAll();
        //    var options = categories.Select(x => new MultiSelectItem(x.Id.ToString(), x.Name, true));
        //    model.CategoryOptions.AddRange(options);
        //    model.Roles.AddRange(Roles.AllRoles);
        //    model.IsPermissionPanelVisible = true;
        //    model.SelectedRole = Roles.User;
        //    return new ViewResult(Views.User.Default, model);
        //}


        //public IControllerResult HandleUserGetList()
        //{
        //    IEnumerable<UserSearchResult> categories = _unitOfWork.UserRepo.GetAllExtended();
        //    UserListViewModel model = new UserListViewModel();
        //    model.Users.AddRange(categories);
        //    return new ViewResult(Views.User.ListPartial, model);
        //}

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
