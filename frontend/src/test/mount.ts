/* eslint-disable vue/one-component-per-file -- utilitários de teste (Blank e ChartStub) */
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
  const names = [
    'dashboard',
    'accounts',
    'transactions',
    'categories',
    'credit-cards',
    'installments',
    'recurrences',
    'reports',
    'groups',
    'notifications',
  ]
  const defaults: RouteRecordRaw[] = names.map((name) => ({ path: `/${name}`, name, component: Blank }))
  defaults.push({ path: '/accounts/:id', name: 'account-detail', component: Blank })
  defaults.push({ path: '/groups/:id', name: 'group-detail', component: Blank })
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

/** Substitui o gráfico real (canvas) por um marcador que expõe `aria-label` e `option` para asserts. */
export const ChartStub = defineComponent({
  name: 'BaseChart',
  props: {
    option: { type: Object, default: () => ({}) },
    ariaLabel: { type: String, default: '' },
    empty: Boolean,
    emptyMessage: { type: String, default: '' },
  },
  template:
    '<div class="chart-stub" role="img" :aria-label="ariaLabel">{{ empty ? emptyMessage || "vazio" : "grafico" }}</div>',
})

/** Simula viewport ≥ 1024px (tabelas) — `matchMedia` responde `matches: true`. Restaure com o retorno. */
export function useDesktopViewport(): () => void {
  const original = window.matchMedia
  window.matchMedia = ((query: string) => ({
    matches: true,
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
  })) as unknown as typeof window.matchMedia
  return () => {
    window.matchMedia = original
  }
}
