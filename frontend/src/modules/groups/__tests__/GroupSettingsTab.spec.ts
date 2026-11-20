import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import GroupSettingsTab from '../components/GroupSettingsTab.vue'
import { makeGroupDetail, makeMemberViewDetail } from './fixtures'
import { button, mountAt } from './helpers'

const mountTab = (group = makeGroupDetail()) =>
  mountAt(GroupSettingsTab, '/groups/g1', { path: '/groups/:id', name: 'group-detail' }, { props: { group } })

describe('GroupSettingsTab', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('Owner/Admin edita nome e descrição; emite o grupo atualizado', async () => {
    mock.onPut('/groups/g1').reply(200, makeGroupDetail({ name: 'Casa nova' }))
    const { wrapper } = await mountTab()

    expect((wrapper.find('input[name="name"]').element as HTMLInputElement).value).toBe('Casa')
    await wrapper.find('input[name="name"]').setValue('Casa nova')
    await wrapper.find('form[aria-label="Dados do grupo"]').trigger('submit')
    await flushPromises()

    expect(JSON.parse(mock.history.put[0]?.data)).toEqual({ name: 'Casa nova', description: 'Contas da casa' })
    expect(wrapper.emitted('updated')?.[0]?.[0]).toMatchObject({ name: 'Casa nova' })
    wrapper.unmount()
  })

  it('erros de validação do backend vão para o campo', async () => {
    mock.onPut('/groups/g1').reply(400, {
      title: 'Dados inválidos.',
      code: 'validation_error',
      errors: { name: ['Nome já em uso.'] },
    })
    const { wrapper } = await mountTab()
    await wrapper.find('form[aria-label="Dados do grupo"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Nome já em uso.')
    wrapper.unmount()
  })

  it('Member comum vê dados somente leitura, sem arquivar e com "Sair do grupo"', async () => {
    const { wrapper } = await mountTab(makeMemberViewDetail())

    expect(wrapper.find('form[aria-label="Dados do grupo"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Somente administradores podem editar')
    expect(wrapper.text()).not.toContain('Arquivar grupo')
    await button(wrapper.element, 'Sair do grupo')!.click()
    expect(wrapper.emitted('leave')).toHaveLength(1)
    wrapper.unmount()
  })

  it('Owner não pode sair e vê o motivo', async () => {
    const { wrapper } = await mountTab()
    expect(button(wrapper.element, 'Sair do grupo')).toBeUndefined()
    expect(wrapper.text()).toContain('O proprietário não pode sair do grupo')
    wrapper.unmount()
  })

  it('arquivar pede confirmação e chama /archive', async () => {
    mock.onPost('/groups/g1/archive').reply(200, makeGroupDetail({ isActive: false }))
    const { wrapper } = await mountTab()

    await button(wrapper.element, 'Arquivar grupo')!.click()
    await flushPromises()
    expect(mock.history.post).toHaveLength(0)

    document.body.querySelector('.confirm-dialog')!.dispatchEvent(new Event('submit'))
    await flushPromises()
    expect(mock.history.post[0]?.url).toBe('/groups/g1/archive')
    expect(wrapper.emitted('updated')?.[0]?.[0]).toMatchObject({ isActive: false })
    wrapper.unmount()
  })

  it('grupo arquivado oferece "Reativar grupo" (sem confirmação) via /unarchive', async () => {
    mock.onPost('/groups/g1/unarchive').reply(200, makeGroupDetail({ isActive: true }))
    const { wrapper } = await mountTab(makeGroupDetail({ isActive: false }))

    await button(wrapper.element, 'Reativar grupo')!.click()
    await flushPromises()

    expect(mock.history.post[0]?.url).toBe('/groups/g1/unarchive')
    expect(wrapper.emitted('updated')).toHaveLength(1)
    wrapper.unmount()
  })
})
