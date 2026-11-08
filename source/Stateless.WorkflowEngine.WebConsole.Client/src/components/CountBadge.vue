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
  <div
    class="flex flex-col gap-1 rounded-md border px-3 py-2 min-w-0"
    :class="{
      'border-default': color === 'neutral',
      'border-error/50 bg-error/10': color === 'error',
      'border-warning/50 bg-warning/10': color === 'warning'
    }"
  >
    <span class="flex items-center gap-1 text-xs uppercase tracking-wide" :class="{ 'text-muted': color === 'neutral', 'text-error': color === 'error', 'text-warning': color === 'warning' }">
      <UIcon v-if="color !== 'neutral'" name="i-lucide-triangle-alert" class="size-3.5 shrink-0" />
      {{ label }}
    </span>
    <USkeleton v-if="loading" class="h-6 w-12" />
    <span v-else class="text-lg font-semibold tabular-nums" :class="{ 'text-error': color === 'error', 'text-warning': color === 'warning' }">
      {{ formatNumber(value) }}
    </span>
  </div>
</template>
