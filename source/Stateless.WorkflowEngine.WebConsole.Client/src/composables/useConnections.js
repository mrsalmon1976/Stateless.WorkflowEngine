import { ref } from 'vue'
import { api } from './useApi'

const connections = ref([])
const loading = ref(false)

export function useConnections() {
  async function loadConnections(dashboardId) {
    loading.value = true
    try {
      connections.value = await api(dashboardId ? `/connections?dashboardId=${dashboardId}` : '/connections')
    } finally {
      loading.value = false
    }
  }

  function getConnection(id) {
    return api(`/connections/${id}`)
  }

  function getConnectionInfo(id) {
    return api(`/connections/${id}/info`, { silent: true })
  }

  function saveConnection(connection) {
    return connection.id
      ? api(`/connections/${connection.id}`, { method: 'PUT', body: connection, silent: true })
      : api('/connections', { method: 'POST', body: connection, silent: true })
  }

  function copyConnection(id, dashboardId) {
    return api(`/connections/${id}/copy`, { method: 'POST', body: { dashboardId }, silent: true })
  }

  function testConnection(connection) {
    return api('/connections/test', { method: 'POST', body: connection, silent: true })
  }

  function deleteConnection(id) {
    return api(`/connections/${id}`, { method: 'DELETE' })
  }

  return { connections, loading, loadConnections, getConnection, getConnectionInfo, saveConnection, copyConnection, testConnection, deleteConnection }
}
