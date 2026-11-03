<script setup>
import { computed, onMounted, ref } from 'vue'
import hljs from 'highlight.js/lib/core'
import json from 'highlight.js/lib/languages/json'
import { useClipboard } from '@vueuse/core'
import { useAuth } from '../../composables/useAuth'
import { useWorkflows } from '../../composables/useWorkflows'
import { Claims } from '../../utils/constants'

hljs.registerLanguage('json', json)

const props = defineProps({
  connectionId: { type: String, required: true },
  workflow: { type: Object, required: true },
  // runs an action ('suspend', 'unsuspend', 'delete', 'complete') on the workflow, including its confirmation,
  // resolving to true if it was carried out
  runAction: { type: Function, required: true }
})

const emit = defineEmits(['close'])

const { hasClaim } = useAuth()
const { getWorkflow } = useWorkflows(props.connectionId)
const { copy, copied } = useClipboard()

const loading = ref(true)
const error = ref('')
const workflowJson = ref('')
const isSuspended = ref(false)
const busyAction = ref(null)

/**
 * Runs an action while the slideover stays open behind its confirmation, closing it once the action is done.  If
 * the action is cancelled or fails (the error is shown by the api), the slideover stays open.
 */
async function onAction(action) {
  busyAction.value = action
  try {
    if (await props.runAction(action)) emit('close')
  } catch {
    // already reported
  } finally {
    busyAction.value = null
  }
}

const formattedJson = computed(() => {
  try {
    return JSON.stringify(JSON.parse(workflowJson.value), null, 2)
  } catch {
    // mongo extended json (e.g. ISODate) isn't always valid json - show it as is
    return workflowJson.value
  }
})

const highlightedJson = computed(() => hljs.highlight(formattedJson.value, { language: 'json' }).value)

onMounted(async () => {
  try {
    const result = await getWorkflow(props.workflow.id)
    workflowJson.value = result.workflowJson
    isSuspended.value = result.isSuspended
  } catch (e) {
    error.value = e.status === 404 ? 'Workflow not found - it may have completed.' : e.message
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <USlideover :title="workflow.name ?? 'Workflow'" :description="workflow.id" :ui="{ content: 'sm:max-w-3xl' }" @update:open="(open) => !open && emit('close')">
    <template #body>
      <div v-if="loading" class="space-y-2">
        <USkeleton v-for="i in 12" :key="i" class="h-4" :class="i % 3 === 0 ? 'w-1/2' : 'w-full'" />
      </div>
      <UAlert v-else-if="error" color="warning" variant="subtle" icon="i-lucide-search-x" :title="error" />
      <div v-else class="relative">
        <UButton
          :icon="copied ? 'i-lucide-copy-check' : 'i-lucide-copy'"
          :label="copied ? 'Copied' : 'Copy'"
          color="neutral"
          variant="outline"
          size="xs"
          class="absolute top-2 right-2"
          @click="copy(formattedJson)"
        />
        <!-- eslint-disable-next-line vue/no-v-html -- highlight.js output is escaped -->
        <pre class="json-viewer text-xs leading-relaxed p-4 rounded-md bg-muted overflow-auto"><code v-html="highlightedJson" /></pre>
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-between gap-2">
        <div class="flex gap-2">
          <template v-if="!loading && !error">
            <UButton
              v-if="hasClaim(Claims.RemoveWorkflow)"
              label="Delete"
              icon="i-lucide-trash-2"
              color="error"
              variant="soft"
              :loading="busyAction === 'delete'"
              :disabled="!!busyAction"
              @click="onAction('delete')"
            />
            <UButton
              v-if="hasClaim(Claims.CompleteWorkflow)"
              label="Mark as completed"
              icon="i-lucide-circle-check"
              color="success"
              variant="soft"
              :loading="busyAction === 'complete'"
              :disabled="!!busyAction"
              @click="onAction('complete')"
            />
          </template>
        </div>
        <div class="flex gap-2">
          <template v-if="!loading && !error">
            <UButton
              v-if="isSuspended && hasClaim(Claims.UnsuspendWorkflow)"
              label="Unsuspend"
              icon="i-lucide-play"
              :loading="busyAction === 'unsuspend'"
              :disabled="!!busyAction"
              @click="onAction('unsuspend')"
            />
            <UButton
              v-if="!isSuspended && hasClaim(Claims.SuspendWorkflow)"
              label="Suspend"
              icon="i-lucide-pause"
              color="warning"
              :loading="busyAction === 'suspend'"
              :disabled="!!busyAction"
              @click="onAction('suspend')"
            />
          </template>
          <UButton label="Close" color="neutral" variant="ghost" @click="emit('close')" />
        </div>
      </div>
    </template>
  </USlideover>
</template>
