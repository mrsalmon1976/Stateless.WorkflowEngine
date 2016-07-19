using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class DashboardModule : WebConsoleSecureModule
    {
        public DashboardModule() : base()
        {
            Get[Actions.Dashboard.Default] = (x) =>
            {
                this.RequiresAnyClaim(Roles.AllRoles);
                AddScript(Scripts.DashboardView);
                return this.View[Views.Dashboard.Default, this.Default()];
            };
            Get[Actions.Dashboard.Connections] = (x) =>
            {
                this.RequiresAnyClaim(Roles.AllRoles);
                return this.View[Views.Dashboard.ConnectionList, this.ConnectionList()];
            };
        }

        public DashboardViewModel Default()
        {

            //var userCount = _unitOfWork.UserRepo.GetUserCountAsync();
            //var docCount = _unitOfWork.DocumentRepo.GetCountAsync();
            //var notifications = _unitOfWork.AuditLogRepo.GetLatest(10);

            //await Task.WhenAll(userCount, docCount, notifications);

            DashboardViewModel model = new DashboardViewModel();
            //model.UserCount = userCount.Result;
            //model.DocumentCount = docCount.Result;
            //model.Notifications.AddRange(notifications.Result);
            return model;

        }

        public DashboardConnectionViewModel ConnectionList()
        {

            //var userCount = _unitOfWork.UserRepo.GetUserCountAsync();
            //var docCount = _unitOfWork.DocumentRepo.GetCountAsync();
            //var notifications = _unitOfWork.AuditLogRepo.GetLatest(10);

            //await Task.WhenAll(userCount, docCount, notifications);

            DashboardConnectionViewModel model = new DashboardConnectionViewModel();
            Random r = new Random();
            for (int i = 0; i < r.Next(7, 13); i++)
            {
                if (i == 5 || i == 3)
                {
                    model.WorkflowStores.Add(new WorkflowStoreModel()
                    {
                        Server = "Server " + r.Next(1, 999).ToString(),
                        Port = r.Next(20000, 29999),
                        Database = "Database" + r.Next(1, 999).ToString(),
                        ConnectionError = "An error occurred connecting to the database"
                    });
                    continue;
                }
                model.WorkflowStores.Add(new WorkflowStoreModel()
                {
                    Server = "Server " + r.Next(1, 999).ToString(),
                    Port = r.Next(20000, 29999),
                    Database = "Database" + r.Next(1, 999).ToString(),
                    ActiveCount = r.Next(0, 1000),
                    SuspendedCount = r.Next(0, 5),
                    CompletedCount = r.Next(0, 50000),
                });
            }
            //model.WorkflowStores = model.WorkflowStores.OrderBy(x => x.Server);
            //model.UserCount = userCount.Result;
            //model.DocumentCount = docCount.Result;
            //model.Notifications.AddRange(notifications.Result);
            return model;

        } 

    }
}
