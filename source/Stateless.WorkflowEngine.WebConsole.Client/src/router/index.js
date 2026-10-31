import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import { Claims } from '../utils/constants'
import AppLayout from '../layouts/AppLayout.vue'
import PublicLayout from '../layouts/PublicLayout.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      // note: this must not be "/" - a bare layout record would match the root url before the app routes
      path: '/login',
      component: PublicLayout,
      children: [
        { path: '', name: 'login', component: () => import('../features/auth/LoginView.vue') }
      ]
    },
    {
      path: '/',
      component: AppLayout,
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: { name: 'dashboard' } },
        { path: 'dashboard', name: 'dashboard', component: () => import('../features/dashboard/DashboardView.vue') },
        { path: 'connections/:connectionId/workflows', name: 'store', component: () => import('../features/store/StoreView.vue'), props: true },
        { path: 'users', name: 'users', component: () => import('../features/users/UsersView.vue'), meta: { claim: Claims.UserList } }
      ]
    },
    { path: '/:pathMatch(.*)*', redirect: { name: 'dashboard' } }
  ]
})

router.beforeEach(async (to) => {
  const { loadCurrentUser, hasClaim } = useAuth()
  const user = await loadCurrentUser()

  if (to.name === 'login' && user) {
    return { name: 'dashboard' }
  }
  if (to.matched.some((r) => r.meta.requiresAuth) && !user) {
    return { name: 'login', query: to.fullPath !== '/' && to.fullPath !== '/dashboard' ? { returnUrl: to.fullPath } : {} }
  }
  if (to.meta.claim && !hasClaim(to.meta.claim)) {
    return { name: 'dashboard' }
  }
})

export default router
