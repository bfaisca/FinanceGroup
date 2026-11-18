import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'
import PrimeVue from 'primevue/config'
import ToastService from 'primevue/toastservice'
import Tooltip from 'primevue/tooltip'
import AppLayout from '../AppLayout.vue'
import { NAV_ITEMS } from '../navigation'
import { useAuthStore } from '@/stores/auth'
import { DARK_MODE_CLASS } from '@/plugins/primevue'

const Stub = defineComponent({ template: '<p>conteúdo</p>' })

async function mountLayout() {
  const pinia = createPinia()
  setActivePinia(pinia)
  const auth = useAuthStore()
  auth.setSession({
    accessToken: 't',
    accessTokenExpiresAt: '2026-11-18T23:00:00Z',
    user: { id: 'u1', name: 'Maria Silva', email: 'maria@exemplo.com', createdAt: '2026-11-18T22:40:00Z' },
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        component: AppLayout,
        children: NAV_ITEMS.map((i) => ({ path: i.routeName, name: i.routeName, component: Stub })),
      },
      { path: '/login', name: 'login', component: Stub },
    ],
  })
  await router.push('/dashboard')
  await router.isReady()
  const warn = vi.spyOn(console, 'warn')
  const wrapper = mount(
    { template: '<RouterView />' },
    {
      attachTo: document.body,
      global: {
        plugins: [pinia, router, [PrimeVue, { theme: 'none' }], ToastService],
        directives: { tooltip: Tooltip },
      },
    },
  )
  await flushPromises()
  return { wrapper, warn }
}

describe('AppLayout', () => {
  afterEach(() => {
    document.body.innerHTML = ''
    document.documentElement.classList.remove(DARK_MODE_CLASS)
  })

  it('renderiza skip-link, conteúdo principal e todos os 10 itens do menu', async () => {
    const { wrapper, warn } = await mountLayout()

    expect(wrapper.find('a.skip-link').attributes('href')).toBe('#main-content')
    expect(wrapper.find('main#main-content').text()).toContain('conteúdo')

    const sidebar = wrapper.find('nav[aria-label="Navegação principal"]')
    const labels = sidebar.findAll('a').map((a) => a.text())
    expect(labels).toEqual(NAV_ITEMS.map((i) => i.label))
    expect(labels).toHaveLength(10)

    expect(wrapper.find('[aria-label="Abrir menu de navegação"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Maria Silva')
    expect(warn).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('alterna o tema escuro e persiste a preferência', async () => {
    const { wrapper } = await mountLayout()
    const toggle = wrapper.find('[aria-label="Ativar tema escuro"]')
    expect(toggle.exists()).toBe(true)

    await toggle.trigger('click')

    expect(document.documentElement.classList.contains(DARK_MODE_CLASS)).toBe(true)
    expect(window.localStorage.getItem('gf-theme')).toBe('dark')
    expect(wrapper.find('[aria-label="Ativar tema claro"]').exists()).toBe(true)
    wrapper.unmount()
  })
})
