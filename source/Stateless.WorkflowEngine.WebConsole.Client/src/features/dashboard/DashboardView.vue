<script setup>
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useDocumentVisibility, useIntervalFn, useLocalStorage } from '@vueuse/core'
import { useAuth } from '../../composables/useAuth'
import { useConfirm } from '../../composables/useConfirm'
import { useConnections } from '../../composables/useConnections'
import { notifySuccess } from '../../composables/useNotify'
import { useVersionCheck } from '../../composables/useVersionCheck'
import { ActiveErrorThreshold, ActiveWarningThreshold, AutoRefreshIntervalMs, Claims } from '../../utils/constants'
import { connectionName } from '../../utils/format'
import CountBadge from '../../components/CountBadge.vue'
import CountCell from '../../components/CountCell.vue'
import ConnectionFormModal from './ConnectionFormModal.vue'

const { hasClaim } = useAuth()
const { confirm } = useConfirm()
const { connections, loading, loadConnections, getConnectionInfo, deleteConnection } = useConnections()
const { versionCheck, checkForUpdate } = useVersionCheck()
const overlay = useOverlay()
const connectionModal = overlay.create(ConnectionFormModal)

const layout = useLocalStorage('dashboardLayout', 'cards')
const autoRefresh = useLocalStorage('dashboardAutoRefresh', false)

// workflow counts per connection id: { loading, info }
const infos = reactive({})
const refreshing = ref(false)

const layoutItems = [
  { label: 'Cards', value: 'cards', icon: 'i-lucide-layout-grid' },
  { label: 'Table', value: 'table', icon: 'i-lucide-list' }
]

const tableColumns = [
  { accessorKey: 'host', header: 'Connection' },
  { id: 'active', header: 'Active' },
  { id: 'suspended', header: 'Suspended' },
  { id: 'complete', header: 'Complete' },
  { id: 'actions', header: '' }
]

const canEdit = computed(() => hasClaim(Claims.ConnectionEdit))
const canDelete = computed(() => hasClaim(Claims.ConnectionDelete))

async function refresh() {
  refreshing.value = true
  try {
    await loadConnections()
    await Promise.all(connections.value.map(loadInfo))
  } finally {
    refreshing.value = false
  }
}

async function loadInfo(connection) {
  infos[connection.id] = { loading: true, info: infos[connection.id]?.info ?? null }
  try {
    infos[connection.id] = { loading: false, info: await getConnectionInfo(connection.id) }
  } catch (e) {
    infos[connection.id] = { loading: false, info: { connectionError: e.message } }
  }
}

function infoFor(connection) {
  return infos[connection.id]?.info ?? null
}

function isLoading(connection) {
  return infos[connection.id]?.loading ?? true
}

function connectionMenu(connection) {
  const items = []
  if (canEdit.value) items.push({ label: 'Edit', icon: 'i-lucide-pencil', onSelect: () => openConnection(connection) })
  if (canDelete.value) items.push({ label: 'Delete', icon: 'i-lucide-trash-2', color: 'error', onSelect: () => onDelete(connection) })
  return items
}

async function openConnection(connection = null) {
  const saved = await connectionModal.open({ connection }).result
  if (saved) {
    notifySuccess(connection ? 'Connection updated' : 'Connection added', connectionName(saved))
    await refresh()
  }
}

async function onDelete(connection) {
  const confirmed = await confirm({
    title: 'Delete connection',
    description: `Are you sure you want to delete the connection to ${connectionName(connection)}? Workflows in the store are not affected.`,
    confirmLabel: 'Delete',
    color: 'error'
  })
  if (!confirmed) return
  await deleteConnection(connection.id)
  notifySuccess('Connection deleted', connectionName(connection))
  await refresh()
}

// each refresh counts the workflows in every store, which can be expensive on large collections, so only
// auto refresh while the dashboard is actually visible
const visibility = useDocumentVisibility()
const { pause, resume } = useIntervalFn(refresh, AutoRefreshIntervalMs, { immediate: false })

function updateAutoRefresh() {
  autoRefresh.value && visibility.value === 'visible' ? resume() : pause()
}

function onAutoRefreshChange(value) {
  autoRefresh.value = value
  updateAutoRefresh()
}

watch(visibility, (value) => {
  // catch up straight away when returning to the tab
  if (value === 'visible' && autoRefresh.value) refresh()
  updateAutoRefresh()
})

onMounted(() => {
  refresh()
  checkForUpdate()
  updateAutoRefresh()
})
</script>

<template>
  <UDashboardPanel id="dashboard">
    <template #header>
      <UDashboardNavbar title="Dashboard">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>
        <template #right>
          <USwitch :model-value="autoRefresh" label="Auto refresh" @update:model-value="onAutoRefreshChange" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" aria-label="Refresh" :loading="refreshing" @click="refresh" />
          <UButton v-if="hasClaim(Claims.ConnectionAdd)" icon="i-lucide-plus" label="Add connection" @click="openConnection()" />
        </template>
      </UDashboardNavbar>
      <UDashboardToolbar>
        <template #left>
          <span class="text-sm text-muted">{{ connections.length }} connection{{ connections.length === 1 ? '' : 's' }}</span>
        </template>
        <template #right>
          <UTabs v-model="layout" :items="layoutItems" :content="false" size="xs" />
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UAlert
        v-if="versionCheck?.isNewVersionAvailable"
        color="info"
        variant="subtle"
        icon="i-lucide-circle-arrow-up"
        :title="`Version ${versionCheck.latestReleaseVersionNumber} is available`"
        description="To upgrade, run Update.ps1 as an administrator in the console's install folder on the server."
        :actions="versionCheck.latestReleaseUrl ? [{ label: 'Release notes', to: versionCheck.latestReleaseUrl, target: '_blank', color: 'info', variant: 'outline' }] : []"
      />

      <UEmpty
        v-if="!loading && connections.length === 0"
        icon="i-lucide-database"
        title="No connections"
        description="Add a connection to a workflow store to start monitoring workflows."
        :actions="hasClaim(Claims.ConnectionAdd) ? [{ label: 'Add connection', icon: 'i-lucide-plus', onClick: () => openConnection() }] : []"
      />

      <div v-else-if="layout === 'cards'" class="grid grid-cols-1 md:grid-cols-2 2xl:grid-cols-3 gap-4">
        <UCard
          v-for="connection in connections"
          :key="connection.id"
          :class="{ 'ring-error': infoFor(connection)?.connectionError }"
          :ui="{ body: 'space-y-4' }"
        >
          <div class="flex items-start justify-between gap-2">
            <RouterLink :to="{ name: 'store', params: { connectionId: connection.id } }" class="flex items-center gap-3 min-w-0 group">
              <UIcon name="i-lucide-database" class="size-8 shrink-0" :class="infoFor(connection)?.connectionError ? 'text-error' : 'text-primary'" />
              <div class="min-w-0">
                <div class="font-semibold truncate group-hover:text-primary">{{ connection.database }}</div>
                <div class="text-sm text-muted truncate">{{ connection.host }}:{{ connection.port }} · {{ connection.workflowStoreType }}</div>
              </div>
            </RouterLink>
            <UDropdownMenu v-if="connectionMenu(connection).length" :items="connectionMenu(connection)">
              <UButton icon="i-lucide-ellipsis-vertical" color="neutral" variant="ghost" aria-label="Connection actions" />
            </UDropdownMenu>
          </div>

          <UAlert
            v-if="infoFor(connection)?.connectionError"
            color="error"
            variant="subtle"
            icon="i-lucide-unplug"
            title="Unable to connect"
            :description="infoFor(connection).connectionError"
            :ui="{ description: 'line-clamp-3 break-all' }"
          />
          <div v-else class="grid grid-cols-3 gap-2">
            <CountBadge label="Active" :value="infoFor(connection)?.activeCount" :warning-threshold="ActiveWarningThreshold" :error-threshold="ActiveErrorThreshold" :loading="isLoading(connection) && !infoFor(connection)" />
            <CountBadge label="Suspended" :value="infoFor(connection)?.suspendedCount" :error-threshold="0" :loading="isLoading(connection) && !infoFor(connection)" />
            <CountBadge label="Complete" :value="infoFor(connection)?.completeCount" :loading="isLoading(connection) && !infoFor(connection)" />
          </div>
        </UCard>
      </div>

      <UTable v-else :data="connections" :columns="tableColumns" :loading="loading" class="border border-default rounded-md">
        <template #host-cell="{ row }">
          <RouterLink :to="{ name: 'store', params: { connectionId: row.original.id } }" class="flex items-center gap-2 font-medium hover:text-primary">
            <UIcon name="i-lucide-database" :class="infoFor(row.original)?.connectionError ? 'text-error' : 'text-primary'" />
            {{ connectionName(row.original) }}
          </RouterLink>
        </template>
        <template #active-cell="{ row }">
          <UTooltip v-if="infoFor(row.original)?.connectionError" :text="infoFor(row.original).connectionError">
            <UBadge color="error" variant="subtle" label="Connection error" icon="i-lucide-unplug" />
          </UTooltip>
          <CountCell v-else :value="infoFor(row.original)?.activeCount" :warning="ActiveWarningThreshold" :error="ActiveErrorThreshold" />
        </template>
        <template #suspended-cell="{ row }">
          <CountCell v-if="!infoFor(row.original)?.connectionError" :value="infoFor(row.original)?.suspendedCount" :error="0" />
        </template>
        <template #complete-cell="{ row }">
          <CountCell v-if="!infoFor(row.original)?.connectionError" :value="infoFor(row.original)?.completeCount" />
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end">
            <UDropdownMenu v-if="connectionMenu(row.original).length" :items="connectionMenu(row.original)">
              <UButton icon="i-lucide-ellipsis-vertical" color="neutral" variant="ghost" aria-label="Connection actions" />
            </UDropdownMenu>
          </div>
        </template>
      </UTable>
    </template>
  </UDashboardPanel>
</template>

