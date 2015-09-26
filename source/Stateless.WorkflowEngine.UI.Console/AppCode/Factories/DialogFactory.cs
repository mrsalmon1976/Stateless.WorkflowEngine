using Stateless.WorkflowEngine.UI.Console.AppCode.Providers;
using Stateless.WorkflowEngine.UI.Console.AppCode.Services;
using Stateless.WorkflowEngine.UI.Console.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Stateless.WorkflowEngine.UI.Console.AppCode.Factories
{
    public interface IDialogFactory
    {
        IFormConnection GetConnectionDialog(Window owner);
    }

    public class DialogFactory : IDialogFactory
    {
        private UserSettings _userSettings;
        private IUIConnectionService _uiConnectionService;

        public DialogFactory(UserSettings userSettings, IUIConnectionService workflowProviderFactory)
        {
            _userSettings = userSettings;
            _uiConnectionService = workflowProviderFactory;
        }

        public IFormConnection GetConnectionDialog(Window owner)
        {
            FormConnection form = new FormConnection(_userSettings, _uiConnectionService);
            form.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            form.Owner = owner;
            return form;
        }
    }
}
