using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.User
{
    public static class UserMappingExtensions
    {
        public static UserModel ToUserModel(this UserViewModel viewModel)
        {
            return new UserModel
            {
                Id = viewModel.Id,
                UserName = viewModel.UserName,
                Password = viewModel.Password,
                Role = viewModel.Role,
            };
        }
    }
}
