<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import { Claims } from '../utils/constants'
import ChangePasswordModal from '../features/users/ChangePasswordModal.vue'

const router = useRouter()
const { currentUser, hasClaim, logout } = useAuth()
const overlay = useOverlay()
const changePasswordModal = overlay.create(ChangePasswordModal)

const navigation = computed(() => {
  const items = [{ label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: { name: 'dashboard' } }]
  if (hasClaim(Claims.UserList)) {
    items.push({ label: 'Users', icon: 'i-lucide-users', to: { name: 'users' } })
  }
  return items
})

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
        <UNavigationMenu :collapsed="collapsed" :items="navigation" orientation="vertical" />
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
