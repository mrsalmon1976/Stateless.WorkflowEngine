<script setup>
defineProps({
  title: { type: String, required: true },
  description: { type: String, default: '' },
  confirmLabel: { type: String, default: 'Confirm' },
  color: { type: String, default: 'primary' }
})

const emit = defineEmits(['close'])
</script>

<template>
  <UModal :title="title" :description="description" :close="false" :dismissible="false">
    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton color="neutral" variant="ghost" label="Cancel" @click="emit('close', false)" />
        <UButton :color="color" :label="confirmLabel" @click="emit('close', true)" />
      </div>
    </template>
  </UModal>
</template>
