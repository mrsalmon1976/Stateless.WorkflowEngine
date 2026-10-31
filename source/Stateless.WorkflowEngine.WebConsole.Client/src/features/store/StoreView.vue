<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useLocalStorage, watchDebounced } from '@vueuse/core'
import { useAuth } from '../../composables/useAuth'
import { useConfirm } from '../../composables/useConfirm'
import { useConnections } from '../../composables/useConnections'
import { notifySuccess } from '../../composables/useNotify'
import { useWorkflows } from '../../composables/useWorkflows'
import { ActiveErrorThreshold, ActiveWarningThreshold, Claims, MaxWorkflowCount } from '../../utils/constants'
import { connectionName, formatDate, formatNumber } from '../../utils/format'
import CountBadge from '../../components/CountBadge.vue'
import SortHeader from '../../components/SortHeader.vue'
import DefinitionGraphModal from './DefinitionGraphModal.vue'
import WorkflowDetailSlideover from './WorkflowDetailSlideover.vue'

const props = defineProps({
  connectionId: { type: String, required: true }
})

const { hasClaim } = useAuth()
const { confirm } = useConfirm()
const { getConnection, getConnectionInfo } = useConnections()
const { loadWorkflows, getWorkflow, suspendWorkflows, unsuspendWorkflows, deleteWorkflows } = useWorkflows(props.connectionId)
const overlay = useOverlay()
const detailSlideover = overlay.create(WorkflowDetailSlideover)
const definitionModal = overlay.create(DefinitionGraphModal)

const connection = ref(null)
const info = ref(null)
const workflows = ref([])
const loading = ref(false)
const notFound = ref(false)
const globalFilter = ref('')
const rowSelection = ref({})
const sorting = ref([])
const count = useLocalStorage('workflowCount', 50)
// earlier versions allowed larger counts than the server now accepts
if (!(count.value > 0 && count.value <= MaxWorkflowCount)) count.value = 50

// a workflow looked up on the server by id, when the filter is an id that isn't in the loaded rows
const idLookup = ref(null)

// highlight suspended workflows
const tableMeta = { class: { tr: (row) => (row.original.isSuspended ? 'bg-error/10' : '') } }

const countItems = [25, 50, 100, 250, 500, MaxWorkflowCount].map((x) => ({ label: `Show ${x}`, value: x }))

const GuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const columns = [
  { id: 'select', enableSorting: false, enableGlobalFilter: false },
  { accessorKey: 'id', header: 'Id' },
  { accessorKey: 'name', header: 'Type' },
  { accessorKey: 'currentState', header: 'State' },
  { accessorKey: 'isSuspended', header: 'Suspended', enableGlobalFilter: false },
  { accessorKey: 'resumeTrigger', header: 'Trigger' },
  { accessorKey: 'resumeOn', header: 'Resumes', enableGlobalFilter: false },
  { accessorKey: 'priority', header: 'Priority', enableGlobalFilter: false },
  { accessorKey: 'retryCount', header: 'Retries', enableGlobalFilter: false },
  { accessorKey: 'retryIntervals', header: 'Retry intervals', enableSorting: false, enableGlobalFilter: false },
  { accessorKey: 'createdOn', header: 'Created', enableGlobalFilter: false }
]

const breadcrumb = computed(() => [
  { label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: { name: 'dashboard' } },
  { label: connection.value ? `${connection.value.host}:${connection.value.port}` : '…' },
  { label: connection.value?.database ?? '…' }
])

const selectedIds = computed(() => Object.keys(rowSelection.value).filter((id) => rowSelection.value[id]))

const tableData = computed(() => (idLookup.value?.workflow ? [idLookup.value.workflow] : workflows.value))

// active + suspended = all incomplete workflows in the store
const incompleteCount = computed(() => (info.value && !info.value.connectionError ? info.value.activeCount + info.value.suspendedCount : null))

/**
 * Looks a workflow up on the server when the filter is a full id that isn't in the loaded rows - the list only
 * holds the first N workflows, so filtering alone can't find it.
 */
async function lookupWorkflowById() {
  const id = globalFilter.value.trim()
  if (!GuidPattern.test(id) || workflows.value.some((x) => x.id.toLowerCase() === id.toLowerCase())) {
    idLookup.value = null
    return
  }
  idLookup.value = { id, loading: true, workflow: null }
  try {
    const result = await getWorkflow(id)
    if (globalFilter.value.trim() === id) idLookup.value = { id, loading: false, workflow: result.workflow }
  } catch (e) {
    if (globalFilter.value.trim() === id) idLookup.value = { id, loading: false, workflow: null, notFound: e.status === 404 }
  }
}

// distinct workflow types that have a definition graph
const definitions = computed(() => {
  const map = new Map()
  for (const wf of workflows.value) {
    if (wf.qualifiedName && wf.workflowGraph && !map.has(wf.qualifiedName)) {
      map.set(wf.qualifiedName, { label: wf.name ?? wf.qualifiedName, description: wf.qualifiedName, onSelect: () => openDefinition(wf) })
    }
  }
  return [...map.values()].sort((a, b) => a.label.localeCompare(b.label))
})

async function refresh() {
  loading.value = true
  try {
    const [connectionResult, infoResult, workflowResult] = await Promise.all([
      connection.value ? Promise.resolve(connection.value) : getConnection(props.connectionId),
      getConnectionInfo(props.connectionId).catch((e) => ({ connectionError: e.message })),
      loadWorkflows(count.value).catch(() => [])
    ])
    connection.value = connectionResult
    info.value = infoResult
    workflows.value = workflowResult
    // drop selections for workflows that are no longer in the list
    const ids = new Set(workflowResult.map((x) => x.id))
    rowSelection.value = Object.fromEntries(Object.entries(rowSelection.value).filter(([id]) => ids.has(id) || id === idLookup.value?.id))
    // refresh a looked-up workflow too, as it may have been suspended, unsuspended or deleted
    if (idLookup.value) await lookupWorkflowById()
  } catch (e) {
    notFound.value = e.status === 404
  } finally {
    loading.value = false
  }
}

async function runBulkAction(action, ids) {
  const actions = {
    suspend: { fn: suspendWorkflows, verb: 'Suspend', done: 'suspended', color: 'warning' },
    unsuspend: { fn: unsuspendWorkflows, verb: 'Unsuspend', done: 'unsuspended', color: 'primary' },
    delete: { fn: deleteWorkflows, verb: 'Delete', done: 'deleted', color: 'error' }
  }
  const { fn, verb, done, color } = actions[action]
  const description = action === 'delete'
    ? `Are you sure you want to permanently delete ${ids.length} workflow${ids.length === 1 ? '' : 's'}? This cannot be undone.`
    : `Are you sure you want to ${verb.toLowerCase()} ${ids.length} workflow${ids.length === 1 ? '' : 's'}?`

  if (!(await confirm({ title: `${verb} workflows`, description, confirmLabel: verb, color }))) return false
  await fn(ids)
  notifySuccess(`${ids.length} workflow${ids.length === 1 ? '' : 's'} ${done}`)
  await refresh()
  return true
}

async function openWorkflow(workflow) {
  const action = await detailSlideover.open({ connectionId: props.connectionId, workflow }).result
  if (action) {
    await runBulkAction(action, [workflow.id])
  }
}

function openDefinition(workflow) {
  definitionModal.open({ title: workflow.name ?? workflow.qualifiedName, qualifiedName: workflow.qualifiedName, graph: workflow.workflowGraph })
}

function onSelectRow(e, row) {
  // ignore clicks on the checkbox and links in the row
  if (e.target.closest('button, a, [role=checkbox]')) return
  openWorkflow(row.original)
}

watch(count, refresh)
watchDebounced(globalFilter, lookupWorkflowById, { debounce: 300 })
onMounted(refresh)
</script>

<template>
  <UDashboardPanel id="store">
    <template #header>
      <UDashboardNavbar :title="connection ? connectionName(connection) : 'Workflows'">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>
        <template #right>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" aria-label="Refresh" :loading="loading" @click="refresh" />
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <template #left>
          <UBreadcrumb :items="breadcrumb" />
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UEmpty v-if="notFound" icon="i-lucide-search-x" title="Connection not found" description="The connection may have been deleted." :actions="[{ label: 'Back to dashboard', to: { name: 'dashboard' } }]" />

      <template v-else>
        <UAlert v-if="info?.connectionError" color="error" variant="subtle" icon="i-lucide-unplug" title="Unable to connect" :description="info.connectionError" />

        <div v-else class="grid grid-cols-2 sm:grid-cols-3 gap-3 max-w-xl">
          <CountBadge label="Active" :value="info?.activeCount" :warning-threshold="ActiveWarningThreshold" :error-threshold="ActiveErrorThreshold" :loading="!info" />
          <CountBadge label="Suspended" :value="info?.suspendedCount" :error-threshold="0" :loading="!info" />
          <CountBadge label="Complete" :value="info?.completeCount" :loading="!info" />
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <UInput
            v-model="globalFilter"
            icon="i-lucide-search"
            placeholder="Filter loaded rows, or paste a workflow id…"
            :loading="idLookup?.loading"
            class="w-full sm:w-80"
          />
          <USelect v-model="count" :items="countItems" class="w-32" />
          <UDropdownMenu v-if="definitions.length" :items="definitions">
            <UButton label="Definitions" icon="i-lucide-git-fork" trailing-icon="i-lucide-chevron-down" color="neutral" variant="outline" />
          </UDropdownMenu>

          <div v-if="selectedIds.length" class="flex flex-wrap items-center gap-2 sm:ms-auto">
            <span class="text-sm text-muted">{{ selectedIds.length }} selected</span>
            <UButton v-if="hasClaim(Claims.SuspendWorkflow)" label="Suspend" icon="i-lucide-pause" color="warning" variant="soft" @click="runBulkAction('suspend', selectedIds)" />
            <UButton v-if="hasClaim(Claims.UnsuspendWorkflow)" label="Unsuspend" icon="i-lucide-play" color="primary" variant="soft" @click="runBulkAction('unsuspend', selectedIds)" />
            <UButton v-if="hasClaim(Claims.RemoveWorkflow)" label="Delete" icon="i-lucide-trash-2" color="error" variant="soft" @click="runBulkAction('delete', selectedIds)" />
          </div>
        </div>

        <UAlert
          v-if="idLookup?.workflow"
          color="info"
          variant="subtle"
          icon="i-lucide-search-check"
          title="Found by id"
          description="This workflow isn't in the loaded rows - it was looked up directly. Clear the filter to return to the list."
        />
        <UAlert
          v-else-if="idLookup?.notFound"
          color="warning"
          variant="subtle"
          icon="i-lucide-search-x"
          title="No incomplete workflow with this id"
          description="It may have completed or been deleted - completed workflows are not shown in the console."
        />
        <p v-else class="text-sm text-muted">
          Showing the first {{ formatNumber(workflows.length) }}
          <template v-if="incompleteCount !== null">of {{ formatNumber(incompleteCount) }}</template>
          incomplete workflows. Sorting and filtering apply to these rows only - paste a full workflow id to find one that isn't listed.
        </p>

        <UTable
          v-model:global-filter="globalFilter"
          v-model:row-selection="rowSelection"
          v-model:sorting="sorting"
          :data="tableData"
          :columns="columns"
          :loading="loading"
          :get-row-id="(row) => row.id"
          sticky
          empty="No incomplete workflows"
          class="border border-default rounded-md flex-1"
          :meta="tableMeta"
          :ui="{ tr: 'cursor-pointer' }"
          @select="onSelectRow"
        >
          <template #select-header="{ table }">
            <UCheckbox
              :model-value="table.getIsSomePageRowsSelected() ? 'indeterminate' : table.getIsAllPageRowsSelected()"
              aria-label="Select all"
              @update:model-value="(value) => table.toggleAllPageRowsSelected(!!value)"
            />
          </template>
          <template #select-cell="{ row }">
            <UCheckbox :model-value="row.getIsSelected()" aria-label="Select workflow" @update:model-value="(value) => row.toggleSelected(!!value)" />
          </template>

          <template v-for="column in columns.filter((c) => c.accessorKey && c.enableSorting !== false)" :key="column.accessorKey" #[`${column.accessorKey}-header`]="{ column: tableColumn }">
            <SortHeader :column="tableColumn" :label="column.header" />
          </template>

          <template #id-cell="{ row }">
            <span class="font-mono text-xs">{{ row.original.id }}</span>
          </template>
          <template #name-cell="{ row }">
            <UButton
              v-if="row.original.workflowGraph"
              :label="row.original.name"
              variant="link"
              size="sm"
              class="p-0"
              trailing-icon="i-lucide-git-fork"
              @click="openDefinition(row.original)"
            />
            <span v-else>{{ row.original.name }}</span>
          </template>
          <template #isSuspended-cell="{ row }">
            <UBadge v-if="row.original.isSuspended" color="error" variant="subtle" label="Suspended" icon="i-lucide-pause" />
          </template>
          <template #resumeOn-cell="{ row }">
            {{ formatDate(row.original.resumeOn) }}
          </template>
          <template #retryIntervals-cell="{ row }">
            <span class="text-xs text-muted">{{ row.original.retryIntervals?.join(', ') }}</span>
          </template>
          <template #createdOn-cell="{ row }">
            {{ formatDate(row.original.createdOn) }}
          </template>
        </UTable>
      </template>
    </template>
  </UDashboardPanel>
</template>
