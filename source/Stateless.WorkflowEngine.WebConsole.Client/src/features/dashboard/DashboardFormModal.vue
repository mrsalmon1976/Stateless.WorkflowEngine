<script setup>
import { computed, reactive, ref } from 'vue'
import { useDashboards } from '../../composables/useDashboards'

const props = defineProps({
  // the dashboard to rename - omit to add a new dashboard
  dashboard: { type: Object, default: null }
})

const emit = defineEmits(['close'])
const { saveDashboard } = useDashboards()

const isEdit = computed(() => !!props.dashboard)
const state = reactive({
  id: props.dashboard?.id ?? null,
  name: props.dashboard?.name ?? ''
})

const errors = ref([])
const saving = ref(false)

async function onSave() {
  errors.value = []
  saving.value = true
  try {
    emit('close', await saveDashboard(state))
  } catch (e) {
    errors.value = e.errors
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <UModal :title="isEdit ? 'Rename dashboard' : 'Add dashboard'" @update:open="(open) => !open && emit('close', null)">
    <template #body>
      <UForm id="dashboard-form" :state="state" class="space-y-4" @submit="onSave">
        <UFormField label="Name" name="name" required>
          <UInput v-model="state.name" :maxlength="100" autocomplete="off" autofocus class="w-full" />
        </UFormField>
        <UAlert v-if="errors.length" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Please correct the following">
          <template #description>
            <ul class="list-disc ps-4">
              <li v-for="error in errors" :key="error">{{ error }}</li>
            </ul>
          </template>
        </UAlert>
      </UForm>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton label="Cancel" color="neutral" variant="ghost" @click="emit('close', null)" />
        <UButton type="submit" form="dashboard-form" label="Save" icon="i-lucide-save" :loading="saving" />
      </div>
    </template>
  </UModal>
</template>
