<script setup>
import { computed } from 'vue'

const props = defineProps({
  column: { type: Object, required: true },
  label: { type: String, required: true }
})

const icon = computed(() => {
  const sorted = props.column.getIsSorted()
  if (sorted === 'asc') return 'i-lucide-arrow-up-narrow-wide'
  if (sorted === 'desc') return 'i-lucide-arrow-down-wide-narrow'
  return 'i-lucide-arrow-up-down'
})
</script>

<template>
  <UButton
    :label="label"
    :trailing-icon="icon"
    color="neutral"
    variant="ghost"
    size="sm"
    class="-mx-2.5 font-semibold"
    @click="column.toggleSorting(column.getIsSorted() === 'asc')"
  />
</template>
