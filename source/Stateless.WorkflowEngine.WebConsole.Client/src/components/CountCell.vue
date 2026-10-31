<script setup>
import { computed } from 'vue'
import { formatNumber } from '../utils/format'

// a count coloured by threshold, for use in tables
const props = defineProps({
  value: { type: Number, default: null },
  warning: { type: Number, default: null },
  error: { type: Number, default: null }
})

const colorClass = computed(() => {
  if (props.value === null || props.value === undefined) return ''
  if (props.error !== null && props.value > props.error) return 'text-error font-semibold'
  if (props.warning !== null && props.value > props.warning) return 'text-warning font-semibold'
  return ''
})
</script>

<template>
  <span class="tabular-nums" :class="colorClass">{{ formatNumber(value) }}</span>
</template>
