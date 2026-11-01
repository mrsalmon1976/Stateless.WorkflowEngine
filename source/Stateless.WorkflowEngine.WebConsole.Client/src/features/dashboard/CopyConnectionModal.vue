<script setup>
import { computed, ref } from 'vue'
import { useConnections } from '../../composables/useConnections'
import { useDashboards } from '../../composables/useDashboards'
import { connectionName } from '../../utils/format'

const props = defineProps({
  connection: { type: Object, required: true }
})

const emit = defineEmits(['close'])
const { copyConnection } = useConnections()
const { dashboards } = useDashboards()

const dashboardItems = computed(() => dashboards.value
  .filter((d) => d.id !== props.connection.dashboardId)
  .map((d) => ({ label: d.name, value: d.id })))

const dashboardId = ref(dashboardItems.value[0]?.value ?? null)
const errors = ref([])
const copying = ref(false)

async function onCopy() {
  errors.value = []
  copying.value = true
  try {
    const copied = await copyConnection(props.connection.id, dashboardId.value)
    emit('close', { connection: copied, dashboard: dashboards.value.find((d) => d.id === dashboardId.value) })
  } catch (e) {
    errors.value = e.errors
  } finally {
    copying.value = false
  }
}
</script>

<template>
  <UModal title="Copy connection" :description="connectionName(connection)" @update:open="(open) => !open && emit('close', null)">
    <template #body>
      <UForm id="copy-connection-form" :state="{ dashboardId }" class="space-y-4" @submit="onCopy">
        <UFormField label="Copy to dashboard" name="dashboardId" required>
          <USelect v-model="dashboardId" :items="dashboardItems" class="w-full" />
        </UFormField>
        <UAlert v-if="errors.length" color="error" variant="subtle" icon="i-lucide-circle-alert" :title="errors.join(' ')" />
      </UForm>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton label="Cancel" color="neutral" variant="ghost" @click="emit('close', null)" />
        <UButton type="submit" form="copy-connection-form" label="Copy" icon="i-lucide-copy" :disabled="!dashboardId" :loading="copying" />
      </div>
    </template>
  </UModal>
</template>
