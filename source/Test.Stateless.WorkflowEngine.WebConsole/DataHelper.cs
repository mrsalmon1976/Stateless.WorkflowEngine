using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
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
            model.Password = "password";
            model.WorkflowStoreType = storeType;

            switch (storeType) {
                case WorkflowStoreType.MongoDb:
                    model.Port = 27017;
                    model.User = "test";
                    break;
            }

            return model;
        }
    }
}
