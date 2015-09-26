using Stateless.WorkflowEngine.UI.Console.AppCode;
using Stateless.WorkflowEngine.UI.Console.AppCode.Factories;
using Stateless.WorkflowEngine.UI.Console.AppCode.Models.Workflow;
using Stateless.WorkflowEngine.UI.Console.AppCode.Providers;
using Stateless.WorkflowEngine.UI.Console.AppCode.Services;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using Stateless.WorkflowEngine.UI.Console.Services.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public interface IFormConnection
    {
        IWorkflowProvider WorkflowProvider { get; set; }

        bool? ShowDialog();
    }

    /// <summary>
    /// Interaction logic for FormConnection.xaml
    /// </summary>
    public partial class FormConnection : Window, IFormConnection
    {
        private UserSettings _userSettings;
        private IUIConnectionService _uiConnectionService;

        protected ObservableCollection<WorkflowStoreConnection> WorkflowStoreConnections = new ObservableCollection<WorkflowStoreConnection>();

        public FormConnection(UserSettings userSettings, IUIConnectionService uiConnectionService)
        {
            InitializeComponent();

            this._userSettings = userSettings;
            this._uiConnectionService = uiConnectionService;
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
            lblMessage.Content = "Connecting...";
            _uiConnectionService.RunAsyncConnection(conn, OnConnectionComplete);

        }

        private void OnConnectionComplete(ConnectionResult result)
        {
            lblMessage.Content = "";
            ToggleUI(true);
            if (result.Exception != null)
            {
                this.DialogResult = false;
                MessageBox.Show(this, "Connection error: " + result.Exception.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                this.WorkflowProvider = result.WorkflowProvider;
                this.DialogResult = true;
                this.Hide();
            }

        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.WorkflowProvider = null;
            this.DialogResult = false;
            this.Hide();
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
            txtServer.Focus();

        }

        private void OnLstConnections_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var conn = ((FrameworkElement)e.OriginalSource).DataContext as WorkflowStoreConnection;
            if (conn != null)
            {
                ToggleUI(false);
                _uiConnectionService.RunAsyncConnection(conn, OnConnectionComplete);

            }
        }

        private void miDelete_Click(object sender, RoutedEventArgs e)
        {
            ToggleUI(false);
            if (MessageBox.Show(this, "Are you sure you want to delete the selected connection?", "Delete Connection", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                WorkflowStoreConnection conn = lstConnections.SelectedItem as WorkflowStoreConnection;
                if (conn != null)
                {
                    this._userSettings.Connections.Remove(conn);
                    this._userSettings.Save();
                    this.WorkflowStoreConnections.Remove(conn);
                }
            }
            ToggleUI(true);
        }

    }
}
