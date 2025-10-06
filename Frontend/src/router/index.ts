import AuthView from '@/views/AuthView.vue'
import Dashboard from '@/views/DashboardView.vue'
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/auth',
      component: AuthView,
    },
    {
      path: '/dashboard',
      component: Dashboard,
    },
  ],
})

export default router
