import type { Component } from 'vue'
import { mount, type ComponentMountingOptions } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import PrimeVue from 'primevue/config'
import ToastService from 'primevue/toastservice'
import { createMemoryHistory, createRouter, type Router, type RouteRecordRaw } from 'vue-router'
import { defineComponent } from 'vue'

const Blank = defineComponent({ template: '<div />' })

/** Router em memória com as rotas usadas pelos componentes (links, navegação). */
export function makeRouter(routes: RouteRecordRaw[] = []) {
  const names = ['dashboard', 'accounts', 'transactions', 'categories']
  const defaults: RouteRecordRaw[] = names.map((name) => ({ path: `/${name}`, name, component: Blank }))
  defaults.push({ path: '/accounts/:id', name: 'account-detail', component: Blank })
  defaults.push({ path: '/:pathMatch(.*)*', name: 'catch-all', component: Blank })
  return createRouter({ history: createMemoryHistory(), routes: [...defaults, ...routes] })
}

/** Monta um componente com PrimeVue, Pinia, Toast e router de teste. */
export function mountWithPlugins<T extends Component>(
  component: T,
  options: ComponentMountingOptions<T> & { pinia?: Pinia; router?: Router } = {} as never,
) {
  const pinia = options.pinia ?? createPinia()
  setActivePinia(pinia)
  const {
    pinia: _pinia,
    router: customRouter,
    global,
    ...rest
  } = options as ComponentMountingOptions<T> & {
    pinia?: Pinia
    router?: Router
  }
  const router = customRouter ?? makeRouter()
  const wrapper = mount(component, {
    attachTo: document.body,
    ...rest,
    global: {
      ...global,
      plugins: [[PrimeVue, { theme: 'none' }], ToastService, pinia, router, ...(global?.plugins ?? [])],
    },
  } as never)
  return { wrapper, pinia, router }
}
