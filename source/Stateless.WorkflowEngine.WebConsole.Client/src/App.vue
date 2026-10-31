<script setup>
import { setToast } from './composables/useNotify'

setToast(useToast())
</script>

<template>
  <UApp>
    <RouterView />
  </UApp>
</template>
