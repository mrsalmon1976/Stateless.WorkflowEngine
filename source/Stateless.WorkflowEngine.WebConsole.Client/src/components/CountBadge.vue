<script setup>
import { computed } from 'vue'
import { formatNumber } from '../utils/format'

const props = defineProps({
  label: { type: String, required: true },
  value: { type: Number, default: null },
  warningThreshold: { type: Number, default: null },
  errorThreshold: { type: Number, default: null },
  loading: { type: Boolean, default: false }
})

const color = computed(() => {
  if (props.value === null || props.value === undefined) return 'neutral'
  if (props.errorThreshold !== null && props.value > props.errorThreshold) return 'error'
  if (props.warningThreshold !== null && props.value > props.warningThreshold) return 'warning'
  return 'neutral'
})
</script>

<template>
  <div class="flex flex-col gap-1 rounded-md border border-default px-3 py-2 min-w-0">
    <span class="text-xs text-muted uppercase tracking-wide">{{ label }}</span>
    <USkeleton v-if="loading" class="h-6 w-12" />
    <span v-else class="text-lg font-semibold tabular-nums" :class="{ 'text-error': color === 'error', 'text-warning': color === 'warning' }">
      {{ formatNumber(value) }}
    </span>
  </div>
</template>
