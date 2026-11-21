import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { defineComponent } from 'vue'
import { http } from '@/services/http'
import { mountWithPlugins } from '@/test/mount'
import { useNotificationRefresh } from '../composables'
import { useNotificationsStore } from '../store'
import { authedPinia } from './helpers'
import { createPinia } from 'pinia'

const Host = defineComponent({
  setup() {
    useNotificationRefresh()
    return () => null
  },
})

describe('useNotificationRefresh', () => {
  let mock: MockAdapter
  let now = 5_000_000
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/notifications/unread-count').reply(200, { count: 2 })
    now = 5_000_000
    vi.spyOn(Date, 'now').mockImplementation(() => now)
  })
  afterEach(() => {
    mock.restore()
    vi.restoreAllMocks()
  })

  const calls = () => mock.history.get.filter((r) => r.url === '/notifications/unread-count').length

  it('busca ao montar, ao trocar de rota e ao voltar o foco, respeitando o intervalo mínimo', async () => {
    const { wrapper, router } = mountWithPlugins(Host, { pinia: authedPinia() })
    await flushPromises()
    expect(calls()).toBe(1)
    expect(useNotificationsStore().unreadCount).toBe(2)

    // Rajada dentro do intervalo: ignorada.
    await router.push('/accounts')
    window.dispatchEvent(new Event('focus'))
    document.dispatchEvent(new Event('visibilitychange'))
    await flushPromises()
    expect(calls()).toBe(1)

    now += 6000
    await router.push('/reports')
    await flushPromises()
    expect(calls()).toBe(2)

    now += 6000
    window.dispatchEvent(new Event('focus'))
    await flushPromises()
    expect(calls()).toBe(3)

    now += 6000
    document.dispatchEvent(new Event('visibilitychange'))
    await flushPromises()
    expect(calls()).toBe(4)
    wrapper.unmount()
  })

  it('não consulta sem sessão', async () => {
    const { wrapper, router } = mountWithPlugins(Host, { pinia: createPinia() })
    await router.push('/accounts')
    window.dispatchEvent(new Event('focus'))
    await flushPromises()
    expect(calls()).toBe(0)
    wrapper.unmount()
  })

  it('remove os ouvintes ao desmontar', async () => {
    const { wrapper } = mountWithPlugins(Host, { pinia: authedPinia() })
    await flushPromises()
    wrapper.unmount()
    now += 6000
    window.dispatchEvent(new Event('focus'))
    await flushPromises()
    expect(calls()).toBe(1)
  })
})
