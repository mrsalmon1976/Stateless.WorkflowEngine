import { computed, ref } from 'vue'
import { api } from './useApi'

// module-level so all components share the signed in user
const currentUser = ref(null)
let loaded = false

export function clearCurrentUser() {
  currentUser.value = null
}

export function useAuth() {
  const isAuthenticated = computed(() => currentUser.value !== null)

  /**
   * Loads the signed in user from the session cookie - only calls the server once unless forced.
   */
  async function loadCurrentUser(force = false) {
    if (loaded && !force) {
      return currentUser.value
    }
    try {
      currentUser.value = await api('/auth/me', { silent: true })
    } catch {
      currentUser.value = null
    }
    loaded = true
    return currentUser.value
  }

  async function login(userName, password) {
    currentUser.value = await api('/auth/login', { method: 'POST', body: { userName, password }, silent: true })
    loaded = true
    return currentUser.value
  }

  async function logout() {
    try {
      await api('/auth/logout', { method: 'POST', silent: true })
    } finally {
      currentUser.value = null
    }
  }

  function hasClaim(claim) {
    return currentUser.value?.claims?.includes(claim) ?? false
  }

  return { currentUser, isAuthenticated, loadCurrentUser, login, logout, hasClaim }
}
