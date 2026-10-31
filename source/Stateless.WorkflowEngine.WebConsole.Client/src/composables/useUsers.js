import { ref } from 'vue'
import { api } from './useApi'

const users = ref([])
const loading = ref(false)

export function useUsers() {
  async function loadUsers() {
    loading.value = true
    try {
      users.value = await api('/users')
    } finally {
      loading.value = false
    }
  }

  function saveUser(user) {
    return user.id
      ? api(`/users/${user.id}`, { method: 'PUT', body: user, silent: true })
      : api('/users', { method: 'POST', body: user, silent: true })
  }

  function deleteUser(id) {
    return api(`/users/${id}`, { method: 'DELETE' })
  }

  function changePassword(password, confirmPassword) {
    return api('/users/me/password', { method: 'POST', body: { password, confirmPassword }, silent: true })
  }

  return { users, loading, loadUsers, saveUser, deleteUser, changePassword }
}
