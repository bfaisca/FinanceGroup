import { createRouter, createWebHistory, type Router } from 'vue-router'
import { routes } from './routes'
import { authGuard, pageTitle } from './guards'

export function createAppRouter(): Router {
  const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes,
    scrollBehavior(_to, _from, savedPosition) {
      return savedPosition ?? { top: 0 }
    },
  })

  router.beforeEach(authGuard)
  router.afterEach((to) => {
    document.title = pageTitle(to)
  })

  return router
}
