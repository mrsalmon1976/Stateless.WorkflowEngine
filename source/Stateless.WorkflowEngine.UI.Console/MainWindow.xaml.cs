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
        private List<TabItem> _tabItems = new List<TabItem>();
        private int _num = 0;

        #region Injected Properties
        private FormConnection _frmConnection;

        #endregion

        public MainWindow(FormConnection frmConnection)
        {
            InitializeComponent();

            _frmConnection = frmConnection;
        }

        private void AddConnection() 
        {
            _frmConnection.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            _frmConnection.Owner = this;
            if (_frmConnection.ShowDialog() == true)
            {
                IWorkflowProvider workflowProvider = _frmConnection.WorkflowProvider;

                WorkflowBrowser workflowBrowser = new WorkflowBrowser();
                workflowBrowser.WorkflowProvider = workflowProvider;

                TabItem item = new TabItem();
                item.Header = _frmConnection.txtServer.Text + " | " + _frmConnection.txtDatabase.Text;
                item.Name = "Tab" + _num.ToString();
                item.Content = workflowBrowser;
                //item.HeaderTemplate = tabBrowsers.FindResource("TabHeader") as DataTemplate;
                _tabItems.Add(item);

                tabBrowsers.DataContext = _tabItems;
                tabBrowsers.SelectedIndex = (_tabItems.Count - 1);

                _num++;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AddConnection();
        }
    }
}
