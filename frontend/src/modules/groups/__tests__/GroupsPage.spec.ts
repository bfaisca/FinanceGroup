import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import GroupsPage from '../pages/GroupsPage.vue'
import { makeGroupDetail, makeGroupSummary } from './fixtures'
import { button, mountAt } from './helpers'

const mountPage = (url = '/groups') => mountAt(GroupsPage, url, { path: '/groups', name: 'groups' })

describe('GroupsPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('lista grupos com papel, número de membros e selo de arquivado', async () => {
    mock
      .onGet('/groups')
      .reply(200, [
        makeGroupSummary({ id: 'g1', name: 'Casa', myRole: 'Owner', memberCount: 3 }),
        makeGroupSummary({ id: 'g2', name: 'Viagem', myRole: 'Member', memberCount: 1, isActive: false }),
      ])
    const { wrapper } = await mountPage()

    const text = wrapper.text()
    expect(text).toContain('Casa')
    expect(text).toContain('Proprietário')
    expect(text).toContain('3 membros')
    expect(text).toContain('Viagem')
    expect(text).toContain('Membro')
    expect(text).toContain('1 membro')
    expect(text).toContain('Arquivado')
    expect(wrapper.findAll('a').some((a) => a.attributes('href') === '/groups/g1')).toBe(true)
    // Por padrão não pede arquivados.
    expect(mock.history.get[0]?.params).toEqual({})
    wrapper.unmount()
  })

  it('"Mostrar grupos arquivados" fica na URL e pede includeArchived ao backend', async () => {
    mock.onGet('/groups').reply(200, [])
    const { wrapper, router } = await mountPage()

    await wrapper.find('#groups-archived').setValue(true)
    await flushPromises()

    expect(router.currentRoute.value.query.archived).toBe('1')
    expect(mock.history.get.at(-1)?.params).toEqual({ includeArchived: true })
    wrapper.unmount()
  })

  it('lê `?archived=1` da URL ao abrir a página', async () => {
    mock.onGet('/groups').reply(200, [])
    const { wrapper } = await mountPage('/groups?archived=1')
    expect(mock.history.get[0]?.params).toEqual({ includeArchived: true })
    expect((wrapper.find('#groups-archived').element as HTMLInputElement).checked).toBe(true)
    wrapper.unmount()
  })

  it('estado vazio explica que grupos não dividem dívidas', async () => {
    mock.onGet('/groups').reply(200, [])
    const { wrapper } = await mountPage()
    const text = wrapper.text()
    expect(text).toContain('Você ainda não participa de nenhum grupo')
    expect(text).toContain('NÃO dividem dívidas')
    expect(text).toContain('transparência')
    wrapper.unmount()
  })

  it('mostra erro e permite tentar novamente', async () => {
    mock.onGet('/groups').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountPage()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/groups').reply(200, [makeGroupSummary()])
    await button(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.text()).toContain('Casa')
    wrapper.unmount()
  })

  it('cria um grupo pelo diálogo e navega para ele', async () => {
    mock.onGet('/groups').reply(200, [])
    mock.onPost('/groups').reply(201, makeGroupDetail({ id: 'novo', name: 'Viagem' }))
    const { wrapper, router } = await mountPage()

    await button(wrapper.element, 'Novo grupo')!.click()
    await flushPromises()
    const name = document.body.querySelector<HTMLInputElement>('input[name="name"]')!
    name.value = ' Viagem '
    name.dispatchEvent(new Event('input'))
    document.body.querySelector('form')!.dispatchEvent(new Event('submit'))
    await flushPromises()

    expect(JSON.parse(mock.history.post[0]?.data)).toEqual({ name: 'Viagem', description: null })
    expect(router.currentRoute.value.fullPath).toBe('/groups/novo')
    wrapper.unmount()
  })

  it('valida o nome antes de enviar', async () => {
    mock.onGet('/groups').reply(200, [])
    const { wrapper } = await mountPage()
    await button(wrapper.element, 'Novo grupo')!.click()
    await flushPromises()
    document.body.querySelector('form')!.dispatchEvent(new Event('submit'))
    await flushPromises()

    expect(document.body.textContent).toContain('Informe o nome do grupo.')
    expect(mock.history.post).toHaveLength(0)
    wrapper.unmount()
  })
})
