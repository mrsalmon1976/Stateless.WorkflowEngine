<script setup>
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from '../../composables/useAuth'

const route = useRoute()
const router = useRouter()
const { login } = useAuth()

const state = reactive({ userName: '', password: '' })
const error = ref('')
const submitting = ref(false)

async function onSubmit() {
  error.value = ''
  submitting.value = true
  try {
    await login(state.userName, state.password)
    const returnUrl = typeof route.query.returnUrl === 'string' && route.query.returnUrl.startsWith('/') ? route.query.returnUrl : '/dashboard'
    await router.replace(returnUrl)
  } catch (e) {
    error.value = e.errors?.join(' ') ?? 'Unable to sign in'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <UCard class="w-full max-w-sm">
    <template #header>
      <div class="flex flex-col items-center gap-2 text-center">
        <UIcon name="i-lucide-workflow" class="size-10 text-primary" />
        <h1 class="text-xl font-semibold">Stateless Workflow Engine</h1>
        <p class="text-sm text-muted">Sign in to the console</p>
      </div>
    </template>

    <UForm :state="state" class="space-y-4" @submit="onSubmit">
      <UFormField label="User name" name="userName" required>
        <UInput v-model="state.userName" autocomplete="username" autofocus class="w-full" />
      </UFormField>
      <UFormField label="Password" name="password" required>
        <UInput v-model="state.password" type="password" autocomplete="current-password" class="w-full" />
      </UFormField>
      <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :title="error" />
      <UButton type="submit" label="Sign in" block :loading="submitting" :disabled="!state.userName || !state.password" />
    </UForm>
  </UCard>
</template>
