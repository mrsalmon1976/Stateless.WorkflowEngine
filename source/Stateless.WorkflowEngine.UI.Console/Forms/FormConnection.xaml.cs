using Stateless.WorkflowEngine.UI.Console.AppCode;
using Stateless.WorkflowEngine.UI.Console.AppCode.Providers;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using Stateless.WorkflowEngine.UI.Console.Services.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Shapes;

namespace Stateless.WorkflowEngine.UI.Console.Forms
{
    /// <summary>
    /// Interaction logic for FormConnection.xaml
    /// </summary>
    public partial class FormConnection : Window
    {
        private UserSettings _userSettings;

        protected ObservableCollection<WorkflowStoreConnection> WorkflowStoreConnections = new ObservableCollection<WorkflowStoreConnection>();

        public FormConnection(UserSettings userSettings)
        {
            InitializeComponent();

            this._userSettings = userSettings;
        }

        public IWorkflowProvider WorkflowProvider { get; set; }

        private WorkflowStoreConnection GetConnection()
        {
            WorkflowStoreConnection conn = null;

            string tabTag = ((TabItem)this.tabConnectionForms.SelectedItem).Tag.ToString().ToUpper();
            if (tabTag == "SAVED")
            {
                object selectedItem = lstConnections.SelectedItem;
                if (selectedItem != null)
                {
                    conn = (WorkflowStoreConnection)selectedItem;
                }
            }
            else if (tabTag == "MONGODB")
            {
                int port = 0;
                if (!Int32.TryParse(txtPort.Text, out port)) port = Constants.MongoDbDefaultPort;

                string pwd = WorkflowStoreConnection.EncryptPassword(txtPassword.Password);

                conn = new WorkflowStoreConnection(WorkflowStoreType.MongoDb, txtServer.Text, port, txtDatabase.Text, txtUser.Text,
                    pwd, txtActiveCollection.Text, txtCompletedCollection.Text);
            }
            else
            {
                throw new NotSupportedException();
            }
            return conn;
        }

        private void ToggleUI(bool isEnabled)
        {
            btnOK.IsEnabled = isEnabled;
            btnCancel.IsEnabled = isEnabled;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            ToggleUI(false);
            WorkflowStoreConnection conn = GetConnection();

            // try and connect
            try
            {

                lblMessage.Content = "Connecting...";
                this.WorkflowProvider = WorkflowProviderFactory.GetWorkflowService(conn);
                this.WorkflowProvider.GetActive(1);

                // add the connection to the user settings and save
                if (!_userSettings.Connections.Contains(conn)) 
                {
                    _userSettings.Connections.Add(conn);
                    _userSettings.Save();
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Connection error: " + ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                lblMessage.Content = "";
                ToggleUI(true);
            }


        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.WorkflowProvider = null;
            this.DialogResult = false;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtPort.Text = Constants.MongoDbDefaultPort.ToString();
            lblMessage.Content = String.Empty;

            foreach (WorkflowStoreConnection conn in _userSettings.Connections)
            {
                this.WorkflowStoreConnections.Add(conn);
            }
            lstConnections.ItemsSource = this.WorkflowStoreConnections;

        }
    }
}
