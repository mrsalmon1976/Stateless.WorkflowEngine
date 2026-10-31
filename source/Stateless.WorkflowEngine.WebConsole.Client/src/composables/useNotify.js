// The toast instance is captured once in App.vue so that non-component code (e.g. the api wrapper) can
// show notifications.
let toast = null

export function setToast(instance) {
  toast = instance
}

export function notifySuccess(title, description) {
  toast?.add({ title, description, color: 'success', icon: 'i-lucide-circle-check' })
}

export function notifyError(title, description) {
  toast?.add({ title, description, color: 'error', icon: 'i-lucide-circle-alert', duration: 10000 })
}
