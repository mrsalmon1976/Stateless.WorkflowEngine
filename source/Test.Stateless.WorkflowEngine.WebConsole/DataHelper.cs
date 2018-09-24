using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole
{
    public class DataHelper
    {
        public static ConnectionModel CreateConnectionModel(WorkflowStoreType storeType)
        {
            ConnectionModel model = new ConnectionModel();
            model.ActiveCollection = "Workflows";
            model.CompletedCollection = "CompletedWorkflows";
            model.Host = "localhost";
            model.Database = "MyWorkflowStore";
            model.User = "test";
            model.Password = "password";
            model.WorkflowStoreType = storeType;

            switch (storeType) {
                case WorkflowStoreType.MongoDb:
                    model.Port = 27017;
                    break;
            }

            return model;
        }

        public static UserModel CreateUserModel()
        {
            UserModel model = new UserModel();
            model.UserName = "TestUser";
            model.Password = "password";
            model.Role = Roles.User;
            return model;
        }

    }
}
