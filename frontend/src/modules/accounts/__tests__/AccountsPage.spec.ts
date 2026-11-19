import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { makeAccount } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import AccountsPage from '../pages/AccountsPage.vue'

async function mountPage() {
  const { wrapper } = mountWithPlugins(AccountsPage, { pinia: createPinia() })
  await flushPromises()
  return wrapper
}

describe('AccountsPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('estado vazio convida a criar a primeira conta', async () => {
    mock.onGet('/accounts').reply(200, [])
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('Nenhuma conta cadastrada')
    wrapper.unmount()
  })

  it('lista contas em cartões e esconde inativas até pedir', async () => {
    mock.onGet('/accounts').reply(200, [makeAccount(), makeAccount({ id: 'a2', name: 'Antiga', isActive: false })])
    const wrapper = await mountPage()
    expect(wrapper.findAll('article')).toHaveLength(1)

    await wrapper.find('#show-inactive').setValue(true)
    expect(wrapper.findAll('article')).toHaveLength(2)
    expect(wrapper.text()).toContain('Inativa')
    wrapper.unmount()
  })

  it('desativar exige confirmação e chama a rota de desativação', async () => {
    mock.onGet('/accounts').reply(200, [makeAccount()])
    mock.onPost('/accounts/a1/deactivate').reply(200, makeAccount({ isActive: false }))
    const wrapper = await mountPage()

    await wrapper
      .findAll('button')
      .find((b) => b.text() === 'Desativar')!
      .trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('Desativar conta')
    expect(mock.history.post).toHaveLength(0)

    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(mock.history.post.map((r) => r.url)).toEqual(['/accounts/a1/deactivate'])
    wrapper.unmount()
  })

  it('mostra erro de carregamento com opção de tentar novamente', async () => {
    mock.onGet('/accounts').replyOnce(500, { title: 'Erro interno.' })
    const wrapper = await mountPage()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')
    mock.onGet('/accounts').reply(200, [makeAccount()])
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.findAll('article')).toHaveLength(1)
    wrapper.unmount()
  })
})
