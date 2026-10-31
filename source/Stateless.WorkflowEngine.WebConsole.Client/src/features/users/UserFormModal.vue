<script setup>
import { computed, reactive, ref } from 'vue'
import { useUsers } from '../../composables/useUsers'
import { ClaimDescriptions, Roles } from '../../utils/constants'

const props = defineProps({
  // the user to edit - omit to add a new user
  user: { type: Object, default: null }
})

const emit = defineEmits(['close'])
const { saveUser } = useUsers()

const isEdit = computed(() => !!props.user)
const state = reactive({
  id: props.user?.id ?? null,
  userName: props.user?.userName ?? '',
  password: '',
  confirmPassword: '',
  role: props.user?.role ?? Roles.User,
  claims: [...(props.user?.claims ?? [])]
})

const errors = ref([])
const saving = ref(false)

const roleItems = [
  { label: 'Admin', value: Roles.Admin, description: 'Full access to all features' },
  { label: 'User', value: Roles.User, description: 'Access limited to the permissions below' }
]

async function onSave() {
  errors.value = []
  saving.value = true
  try {
    emit('close', await saveUser(state))
  } catch (e) {
    errors.value = e.errors
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <UModal :title="isEdit ? 'Edit user' : 'Add user'" @update:open="(open) => !open && emit('close', null)">
    <template #body>
      <UForm id="user-form" :state="state" class="space-y-4" @submit="onSave">
        <UFormField label="User name" name="userName" required>
          <UInput v-model="state.userName" autocomplete="off" autofocus class="w-full" />
        </UFormField>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <UFormField label="Password" name="password" :required="!isEdit" :help="isEdit ? 'Leave blank to keep the current password' : 'At least 6 characters'">
            <UInput v-model="state.password" type="password" autocomplete="new-password" class="w-full" />
          </UFormField>
          <UFormField label="Confirm password" name="confirmPassword" :required="!isEdit">
            <UInput v-model="state.confirmPassword" type="password" autocomplete="new-password" class="w-full" />
          </UFormField>
        </div>
        <UFormField label="Role" name="role" required>
          <URadioGroup v-model="state.role" :items="roleItems" variant="table" orientation="horizontal" />
        </UFormField>
        <UFormField v-if="state.role === Roles.User" label="Permissions" name="claims" help="Users can always view connections and workflows">
          <UCheckboxGroup v-model="state.claims" :items="ClaimDescriptions" class="grid grid-cols-1 sm:grid-cols-2 gap-1" />
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
        <UButton type="submit" form="user-form" label="Save" icon="i-lucide-save" :loading="saving" />
      </div>
    </template>
  </UModal>
</template>
