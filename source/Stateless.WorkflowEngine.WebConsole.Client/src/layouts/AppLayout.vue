<script setup>
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import { dashboardRoute, useDashboards } from '../composables/useDashboards'
import { notifySuccess } from '../composables/useNotify'
import { Claims } from '../utils/constants'
import DashboardFormModal from '../features/dashboard/DashboardFormModal.vue'
import ChangePasswordModal from '../features/users/ChangePasswordModal.vue'

const router = useRouter()
const { currentUser, hasClaim, logout } = useAuth()
const { dashboards, loadDashboards } = useDashboards()
const overlay = useOverlay()
const changePasswordModal = overlay.create(ChangePasswordModal)
const dashboardModal = overlay.create(DashboardFormModal)

// exact matching, otherwise the default dashboard (/dashboard) would also be active on the other dashboards
const dashboardItems = computed(() => dashboards.value.length
  ? dashboards.value.map((d) => ({ label: d.name, icon: 'i-lucide-layout-dashboard', to: dashboardRoute(d), exact: true }))
  : [{ label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: { name: 'dashboard' }, exact: true }])

// the admin section is only shown when the user can see at least one of these
const adminItems = computed(() => {
  const items = []
  if (hasClaim(Claims.UserList)) {
    items.push({ label: 'Users', icon: 'i-lucide-users', to: { name: 'users' } })
  }
  return items
})

async function addDashboard() {
  const saved = await dashboardModal.open().result
  if (saved) {
    notifySuccess('Dashboard added', saved.name)
    await loadDashboards()
    await router.push(dashboardRoute(saved))
  }
}

onMounted(loadDashboards)

const userMenu = computed(() => [
  [{ type: 'label', label: currentUser.value?.userName, description: currentUser.value?.role }],
  [{ label: 'Change password', icon: 'i-lucide-key-round', onSelect: () => changePasswordModal.open() }],
  [{ label: 'Sign out', icon: 'i-lucide-log-out', onSelect: signOut }]
])

async function signOut() {
  await logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <UDashboardGroup>
    <UDashboardSidebar collapsible :ui="{ footer: 'border-t border-default' }">
      <template #header="{ collapsed }">
        <div class="flex items-center gap-2 min-w-0">
          <UIcon name="i-lucide-workflow" class="size-6 shrink-0 text-primary" />
          <div v-if="!collapsed" class="min-w-0">
            <div class="font-semibold truncate">Workflow Console</div>
            <div class="text-xs text-muted">v{{ currentUser?.version }}</div>
          </div>
        </div>
      </template>

      <template #default="{ collapsed }">
        <nav class="space-y-4">
          <div>
            <div v-if="!collapsed" class="flex items-center justify-between h-7 ps-2.5 mb-1">
              <span class="text-xs font-semibold tracking-wider text-dimmed">DASHBOARDS</span>
              <UTooltip v-if="hasClaim(Claims.DashboardAdd)" text="Add dashboard">
                <UButton icon="i-lucide-plus" color="neutral" variant="ghost" size="xs" aria-label="Add dashboard" @click="addDashboard" />
              </UTooltip>
            </div>
            <UNavigationMenu :collapsed="collapsed" :items="dashboardItems" orientation="vertical" />
          </div>

          <div v-if="adminItems.length">
            <USeparator v-if="collapsed" class="mb-4" />
            <div v-else class="flex items-center h-7 ps-2.5 mb-1">
              <span class="text-xs font-semibold tracking-wider text-dimmed">ADMIN</span>
            </div>
            <UNavigationMenu :collapsed="collapsed" :items="adminItems" orientation="vertical" />
          </div>
        </nav>
      </template>

      <template #footer="{ collapsed }">
        <UDropdownMenu :items="userMenu" :content="{ align: 'start', side: 'top' }">
          <UButton
            :label="collapsed ? undefined : currentUser?.userName"
            icon="i-lucide-circle-user"
            color="neutral"
            variant="ghost"
            block
            :square="collapsed"
            class="justify-start"
          />
        </UDropdownMenu>
        <UColorModeButton v-if="!collapsed" />
      </template>
    </UDashboardSidebar>

    <RouterView />
  </UDashboardGroup>
</template>
