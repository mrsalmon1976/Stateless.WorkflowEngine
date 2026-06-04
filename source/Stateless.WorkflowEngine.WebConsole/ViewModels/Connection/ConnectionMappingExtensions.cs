using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Connection
{
    public static class ConnectionMappingExtensions
    {
        public static ConnectionModel ToConnectionModel(this ConnectionViewModel viewModel)
        {
            return new ConnectionModel
            {
                Id = viewModel.Id,
                WorkflowStoreType = viewModel.WorkflowStoreType,
                Host = viewModel.Host,
                Database = viewModel.Database,
                User = viewModel.User,
                Password = viewModel.Password,
                Port = viewModel.Port,
                ReplicaSet = viewModel.ReplicaSet,
                ActiveCollection = viewModel.ActiveCollection,
                CompletedCollection = viewModel.CompletedCollection,
            };
        }

        public static ConnectionViewModel ToConnectionViewModel(this ConnectionModel model)
        {
            return new ConnectionViewModel
            {
                Id = model.Id,
                WorkflowStoreType = model.WorkflowStoreType,
                Host = model.Host,
                Database = model.Database,
                User = model.User,
                Port = model.Port,
                ReplicaSet = model.ReplicaSet,
                ActiveCollection = model.ActiveCollection,
                CompletedCollection = model.CompletedCollection,
                // Password and PasswordConfirm are intentionally not mapped
            };
        }
    }
}
