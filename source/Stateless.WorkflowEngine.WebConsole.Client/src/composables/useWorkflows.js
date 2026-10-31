import { api } from './useApi'

export function useWorkflows(connectionId) {
  const base = `/connections/${connectionId}`

  function loadWorkflows(count) {
    return api(`${base}/workflows?count=${count}`)
  }

  function getWorkflow(workflowId) {
    return api(`${base}/workflows/${workflowId}`, { silent: true })
  }

  function getDefinition(qualifiedName) {
    return api(`${base}/definitions?qualifiedName=${encodeURIComponent(qualifiedName)}`)
  }

  function suspendWorkflows(workflowIds) {
    return api(`${base}/workflows/suspend`, { method: 'POST', body: { workflowIds } })
  }

  function unsuspendWorkflows(workflowIds) {
    return api(`${base}/workflows/unsuspend`, { method: 'POST', body: { workflowIds } })
  }

  function deleteWorkflows(workflowIds) {
    return api(`${base}/workflows/delete`, { method: 'POST', body: { workflowIds } })
  }

  return { loadWorkflows, getWorkflow, getDefinition, suspendWorkflows, unsuspendWorkflows, deleteWorkflows }
}
