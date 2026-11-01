<script setup>
import { computed, reactive, ref, watch } from 'vue'
import { useConnections } from '../../composables/useConnections'
import { DefaultPorts, WorkflowStoreTypes } from '../../utils/constants'

const props = defineProps({
  // the connection to edit - omit to add a new connection
  connection: { type: Object, default: null },
  // the dashboard to add a new connection to
  dashboardId: { type: String, default: null }
})

const emit = defineEmits(['close'])
const { saveConnection, testConnection } = useConnections()

const isEdit = computed(() => !!props.connection)
const state = reactive({
  id: props.connection?.id ?? null,
  dashboardId: props.connection?.dashboardId ?? props.dashboardId,
  workflowStoreType: props.connection?.workflowStoreType ?? WorkflowStoreTypes.MongoDb,
  host: props.connection?.host ?? '',
  port: props.connection?.port ?? DefaultPorts[WorkflowStoreTypes.MongoDb],
  database: props.connection?.database ?? '',
  user: props.connection?.user ?? '',
  certificatePath: props.connection?.certificatePath ?? '',
  password: '',
  passwordConfirm: '',
  replicaSet: props.connection?.replicaSet ?? '',
  activeCollection: props.connection?.activeCollection ?? 'Workflows',
  completedCollection: props.connection?.completedCollection ?? 'CompletedWorkflows',
  workflowDefinitionCollection: props.connection?.workflowDefinitionCollection ?? ''
})

const errors = ref([])
const testResult = ref(null)
const saving = ref(false)
const testing = ref(false)

const isRavenDb = computed(() => state.workflowStoreType === WorkflowStoreTypes.RavenDb)
const passwordLabel = computed(() => isRavenDb.value ? 'Certificate password' : 'Password')
const passwordHelp = computed(() => isEdit.value && props.connection?.hasPassword ? 'Leave blank to keep the existing password' : undefined)

// switch to the new store type's default port, unless a custom port has been entered
watch(() => state.workflowStoreType, (storeType, previous) => {
  if (state.port === DefaultPorts[previous]) {
    state.port = DefaultPorts[storeType]
  }
  testResult.value = null
})

async function onTest() {
  errors.value = []
  testResult.value = null
  testing.value = true
  try {
    testResult.value = await testConnection(state)
  } catch (e) {
    errors.value = e.errors
  } finally {
    testing.value = false
  }
}

async function onSave() {
  errors.value = []
  saving.value = true
  try {
    const saved = await saveConnection(state)
    emit('close', saved)
  } catch (e) {
    errors.value = e.errors
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <UModal :title="isEdit ? 'Edit connection' : 'Add connection'" :ui="{ content: 'sm:max-w-2xl' }" @update:open="(open) => !open && emit('close', null)">
    <template #body>
      <UForm id="connection-form" :state="state" class="grid grid-cols-1 sm:grid-cols-6 gap-4" @submit="onSave">
        <UFormField label="Store type" name="workflowStoreType" class="sm:col-span-2">
          <USelect v-model="state.workflowStoreType" :items="Object.values(WorkflowStoreTypes)" class="w-full" />
        </UFormField>
        <UFormField label="Host" name="host" required class="sm:col-span-3">
          <UInput v-model="state.host" placeholder="localhost" class="w-full" autofocus />
        </UFormField>
        <UFormField label="Port" name="port" required class="sm:col-span-1">
          <UInputNumber v-model="state.port" :min="1" :max="65535" :format-options="{ useGrouping: false }" orientation="vertical" class="w-full" />
        </UFormField>

        <UFormField label="Database" name="database" required class="sm:col-span-3">
          <UInput v-model="state.database" class="w-full" />
        </UFormField>
        <UFormField v-if="!isRavenDb" label="Replica set" name="replicaSet" class="sm:col-span-3">
          <UInput v-model="state.replicaSet" class="w-full" />
        </UFormField>

        <UFormField
          v-if="isRavenDb"
          label="Client certificate"
          name="certificatePath"
          help="Path to the .pfx file on the console's server - required for secured servers, which are connected to over https"
          class="sm:col-span-6"
        >
          <UInput v-model="state.certificatePath" placeholder="C:\Certificates\client.pfx" autocomplete="off" class="w-full" />
        </UFormField>
        <UFormField v-else label="User" name="user" class="sm:col-span-2">
          <UInput v-model="state.user" autocomplete="off" class="w-full" />
        </UFormField>
        <UFormField :label="passwordLabel" name="password" :help="passwordHelp" :class="isRavenDb ? 'sm:col-span-3' : 'sm:col-span-2'">
          <UInput v-model="state.password" type="password" autocomplete="new-password" class="w-full" />
        </UFormField>
        <UFormField label="Confirm password" name="passwordConfirm" :class="isRavenDb ? 'sm:col-span-3' : 'sm:col-span-2'">
          <UInput v-model="state.passwordConfirm" type="password" autocomplete="new-password" class="w-full" />
        </UFormField>

        <template v-if="!isRavenDb">
          <UFormField label="Active collection" name="activeCollection" required class="sm:col-span-2">
            <UInput v-model="state.activeCollection" class="w-full" />
          </UFormField>
          <UFormField label="Completed collection" name="completedCollection" required class="sm:col-span-2">
            <UInput v-model="state.completedCollection" class="w-full" />
          </UFormField>
          <UFormField label="Definition collection" name="workflowDefinitionCollection" class="sm:col-span-2">
            <UInput v-model="state.workflowDefinitionCollection" placeholder="WorkflowDefinitions" class="w-full" />
          </UFormField>
        </template>

        <div class="sm:col-span-6 space-y-2">
          <UAlert v-if="errors.length" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Please correct the following">
            <template #description>
              <ul class="list-disc ps-4">
                <li v-for="error in errors" :key="error">{{ error }}</li>
              </ul>
            </template>
          </UAlert>
          <UAlert
            v-if="testResult"
            :color="testResult.success ? 'success' : 'error'"
            variant="subtle"
            :icon="testResult.success ? 'i-lucide-plug-zap' : 'i-lucide-unplug'"
            :title="testResult.message"
          />
        </div>
      </UForm>
    </template>

    <template #footer>
      <div class="flex w-full justify-between gap-2">
        <UButton label="Test connection" icon="i-lucide-plug-zap" color="neutral" variant="outline" :loading="testing" @click="onTest" />
        <div class="flex gap-2">
          <UButton label="Cancel" color="neutral" variant="ghost" @click="emit('close', null)" />
          <UButton type="submit" form="connection-form" label="Save" icon="i-lucide-save" :loading="saving" />
        </div>
      </div>
    </template>
  </UModal>
</template>
