<script setup>
import { computed, onMounted } from 'vue'
import { useAuth } from '../../composables/useAuth'
import { useConfirm } from '../../composables/useConfirm'
import { notifySuccess } from '../../composables/useNotify'
import { useUsers } from '../../composables/useUsers'
import { ClaimDescriptions, Claims, Roles } from '../../utils/constants'
import UserFormModal from './UserFormModal.vue'

const { currentUser, hasClaim, loadCurrentUser } = useAuth()
const { confirm } = useConfirm()
const { users, loading, loadUsers, deleteUser } = useUsers()
const overlay = useOverlay()
const userModal = overlay.create(UserFormModal)

const columns = [
  { accessorKey: 'userName', header: 'User name' },
  { accessorKey: 'role', header: 'Role' },
  { id: 'permissions', header: 'Permissions' },
  { id: 'actions', header: '' }
]

const canEdit = computed(() => hasClaim(Claims.UserEdit))
const canDelete = computed(() => hasClaim(Claims.UserDelete))

function permissionLabels(user) {
  if (user.role === Roles.Admin) return ['All']
  return ClaimDescriptions.filter((c) => user.claims.includes(c.value)).map((c) => c.label)
}

function userMenu(user) {
  const items = []
  if (canEdit.value) items.push({ label: 'Edit', icon: 'i-lucide-pencil', onSelect: () => openUser(user) })
  if (canDelete.value && user.id !== currentUser.value?.id) items.push({ label: 'Delete', icon: 'i-lucide-trash-2', color: 'error', onSelect: () => onDelete(user) })
  return items
}

async function openUser(user = null) {
  const saved = await userModal.open({ user }).result
  if (saved) {
    notifySuccess(user ? 'User updated' : 'User added', saved.userName)
    await loadUsers()
    // editing yourself may change your own permissions
    if (saved.id === currentUser.value?.id) await loadCurrentUser(true)
  }
}

async function onDelete(user) {
  const confirmed = await confirm({
    title: 'Delete user',
    description: `Are you sure you want to delete the user '${user.userName}'?`,
    confirmLabel: 'Delete',
    color: 'error'
  })
  if (!confirmed) return
  await deleteUser(user.id)
  notifySuccess('User deleted', user.userName)
  await loadUsers()
}

onMounted(loadUsers)
</script>

<template>
  <UDashboardPanel id="users">
    <template #header>
      <UDashboardNavbar title="Users">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>
        <template #right>
          <UButton v-if="hasClaim(Claims.UserAdd)" icon="i-lucide-user-plus" label="Add user" @click="openUser()" />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UTable :data="users" :columns="columns" :loading="loading" class="border border-default rounded-md">
        <template #userName-cell="{ row }">
          <div class="flex items-center gap-2 font-medium">
            <UIcon name="i-lucide-circle-user" class="size-5 text-muted" />
            {{ row.original.userName }}
            <UBadge v-if="row.original.id === currentUser?.id" label="You" color="neutral" variant="subtle" size="sm" />
          </div>
        </template>
        <template #role-cell="{ row }">
          <UBadge :label="row.original.role" :color="row.original.role === Roles.Admin ? 'primary' : 'neutral'" variant="subtle" />
        </template>
        <template #permissions-cell="{ row }">
          <div class="flex flex-wrap gap-1">
            <UBadge v-for="label in permissionLabels(row.original)" :key="label" :label="label" color="neutral" variant="outline" size="sm" />
            <span v-if="permissionLabels(row.original).length === 0" class="text-sm text-muted">Read only</span>
          </div>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end">
            <UDropdownMenu v-if="userMenu(row.original).length" :items="userMenu(row.original)">
              <UButton icon="i-lucide-ellipsis-vertical" color="neutral" variant="ghost" aria-label="User actions" />
            </UDropdownMenu>
          </div>
        </template>
      </UTable>
    </template>
  </UDashboardPanel>
</template>
