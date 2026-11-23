import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { mountWithPlugins } from '@/test/mount'
import TelegramSection from '../components/TelegramSection.vue'

const unlinked = { configured: true, linked: false, linkedAt: null, botUsername: 'groupfinance_bot' }

describe('TelegramSection (contrato §18)', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => {
    mock.restore()
    vi.useRealTimers()
  })

  async function mountSection() {
    const { wrapper } = mountWithPlugins(TelegramSection, { pinia: createPinia() })
    await flushPromises()
    return wrapper
  }

  it('avisa quando o bot não está habilitado no ambiente', async () => {
    mock.onGet('/telegram/link').reply(200, { ...unlinked, configured: false, botUsername: null })
    const wrapper = await mountSection()
    expect(wrapper.find('[data-testid="telegram-disabled"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('Gerar código')
    wrapper.unmount()
  })

  it('gera o código, mostra o valor, o tempo restante e o link para abrir o Telegram', async () => {
    mock.onGet('/telegram/link').reply(200, unlinked)
    mock.onPost('/telegram/link-code').reply(200, {
      code: 'K7M2QX9P',
      expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
      botUsername: 'groupfinance_bot',
      deepLink: 'https://t.me/groupfinance_bot?start=K7M2QX9P',
    })
    const wrapper = await mountSection()
    expect(wrapper.text()).toContain('@groupfinance_bot')

    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Gerar código'))!
      .trigger('click')
    await flushPromises()

    const box = wrapper.find('[data-testid="telegram-code"]')
    expect(box.text()).toContain('K7M2QX9P')
    expect(box.text()).toMatch(/expira em \d+:\d{2}/)
    const link = wrapper.find('a[href^="https://t.me/"]')
    expect(link.attributes('href')).toBe('https://t.me/groupfinance_bot?start=K7M2QX9P')
    expect(link.attributes('rel')).toContain('noopener')
    wrapper.unmount()
  })

  it('mostra o vínculo e permite desvincular', async () => {
    mock.onGet('/telegram/link').replyOnce(200, { ...unlinked, linked: true, linkedAt: '2026-11-20T12:00:00Z' })
    mock.onDelete('/telegram/link').reply(204)
    mock.onGet('/telegram/link').reply(200, unlinked)
    const wrapper = await mountSection()
    expect(wrapper.find('[data-testid="telegram-linked"]').text()).toContain('vinculado desde')

    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Desvincular'))!
      .trigger('click')
    await flushPromises()

    expect(mock.history.delete).toHaveLength(1)
    expect(wrapper.find('[data-testid="telegram-linked"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Gerar código')
    wrapper.unmount()
  })

  it('mostra erro com opção de tentar novamente quando o estado não carrega', async () => {
    mock.onGet('/telegram/link').replyOnce(500).onGet('/telegram/link').reply(200, unlinked)
    const wrapper = await mountSection()
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Gerar código')
    wrapper.unmount()
  })

  it('informa quando o código expirou', async () => {
    mock.onGet('/telegram/link').reply(200, unlinked)
    mock.onPost('/telegram/link-code').reply(200, {
      code: 'K7M2QX9P',
      expiresAt: new Date(Date.now() - 1000).toISOString(),
      botUsername: 'groupfinance_bot',
      deepLink: 'https://t.me/groupfinance_bot?start=K7M2QX9P',
    })
    const wrapper = await mountSection()
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Gerar código'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('O código expirou')
    expect(wrapper.find('[data-testid="telegram-code"]').exists()).toBe(false)
    wrapper.unmount()
  })
})
