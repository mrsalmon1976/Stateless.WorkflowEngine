import { ref } from 'vue'
import { api } from './useApi'

const versionCheck = ref(null)
let checked = false

export function useVersionCheck() {
  async function checkForUpdate() {
    if (checked) return versionCheck.value
    checked = true
    try {
      versionCheck.value = await api('/version/check', { silent: true })
    } catch {
      versionCheck.value = null
    }
    return versionCheck.value
  }

  return { versionCheck, checkForUpdate }
}
