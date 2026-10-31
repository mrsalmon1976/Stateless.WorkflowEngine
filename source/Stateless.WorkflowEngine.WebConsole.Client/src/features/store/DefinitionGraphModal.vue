<script setup>
import { onMounted, ref } from 'vue'

const props = defineProps({
  title: { type: String, required: true },
  qualifiedName: { type: String, default: '' },
  // the workflow definition graph in DOT format
  graph: { type: String, required: true }
})

const emit = defineEmits(['close'])

const container = ref(null)
const error = ref('')
const loading = ref(true)

onMounted(async () => {
  try {
    // viz.js is large, so only load it when a graph is viewed
    const { instance } = await import('@viz-js/viz')
    const viz = await instance()
    const svg = viz.renderSVGElement(props.graph)
    svg.removeAttribute('width')
    svg.removeAttribute('height')
    svg.classList.add('max-w-full', 'h-auto', 'mx-auto')
    container.value.replaceChildren(svg)
  } catch (e) {
    error.value = `Unable to render the workflow graph: ${e.message}`
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <UModal :title="title" :description="qualifiedName" :ui="{ content: 'sm:max-w-5xl' }" @update:open="(open) => !open && emit('close')">
    <template #body>
      <USkeleton v-if="loading" class="h-96 w-full" />
      <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :title="error" />
      <div ref="container" class="definition-graph overflow-auto" />
    </template>
  </UModal>
</template>
