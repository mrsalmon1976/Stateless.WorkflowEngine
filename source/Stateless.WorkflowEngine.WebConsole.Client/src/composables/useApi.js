import router from '../router'
import { notifyError } from './useNotify'
import { clearCurrentUser } from './useAuth'

export class ApiError extends Error {
  constructor(status, errors) {
    super(errors.join(', '))
    this.status = status
    this.errors = errors
  }
}

/**
 * Calls the console api.  The session cookie is sent automatically.  A 401 sends the user back to the login
 * page; other failures throw an ApiError and, unless `silent` is set, show an error toast.
 */
export async function api(path, { method = 'GET', body, silent = false } = {}) {
  const response = await fetch(`/api${path}`, {
    method,
    credentials: 'same-origin',
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body)
  })

  const text = await response.text()
  const data = text ? JSON.parse(text) : null

  if (response.ok) {
    return data
  }

  if (response.status === 401 && !path.startsWith('/auth/')) {
    clearCurrentUser()
    const returnUrl = router.currentRoute.value.fullPath
    await router.push({ name: 'login', query: returnUrl && returnUrl !== '/' ? { returnUrl } : {} })
    throw new ApiError(401, ['Your session has expired'])
  }

  const errors = data?.errors ?? [data?.error ?? (response.status === 403 ? 'You do not have permission to do this' : `Server error (${response.status})`)]
  if (!silent) {
    notifyError('Request failed', errors.join(' '))
  }
  throw new ApiError(response.status, errors)
}
