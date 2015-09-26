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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Stateless.WorkflowEngine.UI.Console.Controls
{
    /// <summary>
    /// Interaction logic for WorkflowBrowser.xaml
    /// </summary>
    public partial class WorkflowBrowser : UserControl
    {
        protected ObservableCollection<UIWorkflowContainer> workflows = new ObservableCollection<UIWorkflowContainer>();

        public WorkflowBrowser()
        {
            InitializeComponent();
        }

        public IWorkflowProvider WorkflowProvider { get; set; }

        private void RefreshWorkflows()
        {
            int count = 0;
            Int32.TryParse(txtWorkflowCount.Text, out count);
            workflows.Clear();
            foreach (UIWorkflowContainer wc in this.WorkflowProvider.GetActive(count))
            {
                workflows.Add(wc);
            }
            chkSelectAll.IsChecked = false;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ToggleUI(false);
            grdWorkflows.ItemsSource = workflows;
            RefreshWorkflows();
            ToggleUI(true);
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ToggleUI(false);
            RefreshWorkflows();
            ToggleUI(true);
        }

        private void btnSuspend_Click(object sender, RoutedEventArgs e)
        {
            ToggleUI(false);
            IEnumerable<UIWorkflowContainer> workflows = this.workflows.Where(x => x.IsSelected);
            if (workflows.Count() > 0)
            {
                if (MessageBox.Show(MainWindow.GetWindow(this), "Are you sure you want to suspend the selected workflows?", "Suspend Workflows", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    foreach (UIWorkflowContainer wc in workflows)
                    {
                        if (!wc.Workflow.IsSuspended)
                        {
                            this.WorkflowProvider.SuspendWorkflow(wc.Workflow.Id);
                        }
                    }
                    RefreshWorkflows();
                }
            }
            ToggleUI(true);
        }

        private void btnUnsuspend_Click(object sender, RoutedEventArgs e)
        {
            ToggleUI(false);
            IEnumerable<UIWorkflowContainer> workflows = this.workflows.Where(x => x.IsSelected);
            if (workflows.Count() > 0)
            {
                if (MessageBox.Show(MainWindow.GetWindow(this), "Are you sure you want to unsuspend the selected workflows?", "Suspend Workflows", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    foreach (UIWorkflowContainer wc in workflows)
                    {
                        if (wc.Workflow.IsSuspended)
                        {
                            this.WorkflowProvider.UnsuspendWorkflow(wc.Workflow.Id);
                        }
                    }
                    RefreshWorkflows();
                }
            }
            ToggleUI(true);
        }

        private void ToggleUI(bool isEnabled)
        {
            btnRefresh.IsEnabled = isEnabled;
        }

        private void ToggleCheckBoxes(bool isChecked)
        {
            foreach (UIWorkflowContainer wc in this.workflows)
            {
                wc.IsSelected = isChecked;
            }
        }


        private void txtWorkflowCount_LostFocus(object sender, RoutedEventArgs e)
        {
            int count = 0;
            if (!Int32.TryParse(txtWorkflowCount.Text, out count))
            {
                txtWorkflowCount.Text = "50";
            }

        }

        private void chkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            ToggleCheckBoxes(true);
        }

        private void chkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            ToggleCheckBoxes(false);
        }

    }
}
