export const Claims = {
  ConnectionAdd: 'ConnectionAdd',
  ConnectionDelete: 'ConnectionDelete',
  ConnectionEdit: 'ConnectionEdit',
  DashboardAdd: 'DashboardAdd',
  DashboardDelete: 'DashboardDelete',
  DashboardEdit: 'DashboardEdit',
  RemoveWorkflow: 'RemoveWorkflow',
  SuspendWorkflow: 'SuspendWorkflow',
  UnsuspendWorkflow: 'UnsuspendWorkflow',
  UserAdd: 'UserAdd',
  UserDelete: 'UserDelete',
  UserEdit: 'UserEdit',
  UserList: 'UserList'
}

export const ClaimDescriptions = [
  { value: Claims.ConnectionAdd, label: 'Add connections' },
  { value: Claims.ConnectionEdit, label: 'Edit connections' },
  { value: Claims.ConnectionDelete, label: 'Delete connections' },
  { value: Claims.DashboardAdd, label: 'Add dashboards' },
  { value: Claims.DashboardEdit, label: 'Rename dashboards' },
  { value: Claims.DashboardDelete, label: 'Delete dashboards' },
  { value: Claims.SuspendWorkflow, label: 'Suspend workflows' },
  { value: Claims.UnsuspendWorkflow, label: 'Unsuspend workflows' },
  { value: Claims.RemoveWorkflow, label: 'Delete workflows' },
  { value: Claims.UserList, label: 'View users' },
  { value: Claims.UserAdd, label: 'Add users' },
  { value: Claims.UserEdit, label: 'Edit users' },
  { value: Claims.UserDelete, label: 'Delete users' }
]

export const Roles = {
  Admin: 'Admin',
  User: 'User'
}

// must match WorkflowsController.MaxCount on the server
export const MaxWorkflowCount = 1000

// how often the dashboard refreshes when auto refresh is on
export const AutoRefreshIntervalMs = 60000

// thresholds used to colour workflow counts on the dashboard
export const ActiveWarningThreshold = 100
export const ActiveErrorThreshold = 10000
