import { computed, ref } from 'vue'
import { api } from './useApi'

// module-level so the sidebar and dashboard view share the list
const dashboards = ref([])
const loading = ref(false)

/**
 * The route for a dashboard - the default dashboard uses the plain /dashboard url so that links to it don't
 * need to know its id.
 */
export function dashboardRoute(dashboard) {
  return !dashboard || dashboard.isDefault ? { name: 'dashboard' } : { name: 'dashboard', params: { dashboardId: dashboard.id } }
}

export function useDashboards() {
  const defaultDashboard = computed(() => dashboards.value.find((d) => d.isDefault) ?? null)

  async function loadDashboards() {
    loading.value = true
    try {
      dashboards.value = await api('/dashboards')
    } finally {
      loading.value = false
    }
  }

  function saveDashboard(dashboard) {
    return dashboard.id
      ? api(`/dashboards/${dashboard.id}`, { method: 'PUT', body: dashboard, silent: true })
      : api('/dashboards', { method: 'POST', body: dashboard, silent: true })
  }

  function deleteDashboard(id) {
    return api(`/dashboards/${id}`, { method: 'DELETE' })
  }

  return { dashboards, defaultDashboard, loading, loadDashboards, saveDashboard, deleteDashboard }
}
