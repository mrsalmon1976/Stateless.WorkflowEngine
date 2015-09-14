using Stateless.WorkflowEngine.UI.Console.AppCode.Factories;
using Stateless.WorkflowEngine.UI.Console.Controls;
using Stateless.WorkflowEngine.UI.Console.Forms;
using Stateless.WorkflowEngine.UI.Console.Services.Workflow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Stateless.WorkflowEngine.UI.Console
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int _num = 0;

        #region Injected Properties
        private IDialogFactory _dialogFactory;

        #endregion

        public MainWindow(IDialogFactory dialogFactory)
        {
            InitializeComponent();

            _dialogFactory = dialogFactory;
        }

        private void AddConnection() 
        {
            IFormConnection frmConnection = _dialogFactory.GetConnectionDialog(this);
            if (frmConnection.ShowDialog() == true)
            {
                IWorkflowProvider workflowProvider = frmConnection.WorkflowProvider;

                WorkflowBrowser workflowBrowser = new WorkflowBrowser();
                workflowBrowser.WorkflowProvider = workflowProvider;

                TabItem item = new TabItem();
                item.Header = String.Format("{0}:{1} | {2}", workflowProvider.Connection.Host, workflowProvider.Connection.Port, workflowProvider.Connection.DatabaseName);
                item.Name = "Tab" + _num.ToString();
                item.Content = workflowBrowser;

                tabBrowsers.Items.Add(item);
                tabBrowsers.SelectedIndex = tabBrowsers.Items.Count - 1;

                _num++;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AddConnection();
        }

        private void OnMnuItemClick_File_Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnMnuItemClick_File_NewConnection(object sender, RoutedEventArgs e)
        {
            AddConnection();
        }
    }
}
