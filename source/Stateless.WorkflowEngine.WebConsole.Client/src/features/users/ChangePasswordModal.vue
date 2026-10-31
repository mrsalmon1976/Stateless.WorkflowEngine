<script setup>
import { reactive, ref } from 'vue'
import { notifySuccess } from '../../composables/useNotify'
import { useUsers } from '../../composables/useUsers'

const emit = defineEmits(['close'])
const { changePassword } = useUsers()

const state = reactive({ password: '', confirmPassword: '' })
const errors = ref([])
const saving = ref(false)

async function onSave() {
  errors.value = []
  saving.value = true
  try {
    await changePassword(state.password, state.confirmPassword)
    notifySuccess('Password changed')
    emit('close')
  } catch (e) {
    errors.value = e.errors
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <UModal title="Change password" @update:open="(open) => !open && emit('close')">
    <template #body>
      <UForm id="password-form" :state="state" class="space-y-4" @submit="onSave">
        <UFormField label="New password" name="password" help="At least 6 characters" required>
          <UInput v-model="state.password" type="password" autocomplete="new-password" autofocus class="w-full" />
        </UFormField>
        <UFormField label="Confirm password" name="confirmPassword" required>
          <UInput v-model="state.confirmPassword" type="password" autocomplete="new-password" class="w-full" />
        </UFormField>
        <UAlert v-if="errors.length" color="error" variant="subtle" icon="i-lucide-circle-alert" :title="errors.join(' ')" />
      </UForm>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton label="Cancel" color="neutral" variant="ghost" @click="emit('close')" />
        <UButton type="submit" form="password-form" label="Change password" :loading="saving" />
      </div>
    </template>
  </UModal>
</template>
