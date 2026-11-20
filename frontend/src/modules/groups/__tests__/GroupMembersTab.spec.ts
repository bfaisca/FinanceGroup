import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import GroupMembersTab from '../components/GroupMembersTab.vue'
import { makeGroupDetail, makeGroupMember, makeMemberViewDetail } from './fixtures'
import { button, mountAt } from './helpers'

const mountTab = (group = makeGroupDetail()) =>
  mountAt(GroupMembersTab, '/groups/g1', { path: '/groups/:id', name: 'group-detail' }, { props: { group } })

describe('GroupMembersTab', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  function rowOf(wrapper: Awaited<ReturnType<typeof mountTab>>['wrapper'], name: string) {
    return wrapper.findAll('li').find((li) => li.text().includes(name))!
  }

  it('Owner: promove Member, rebaixa Admin e remove ambos; nada no próprio Owner', async () => {
    const { wrapper } = await mountTab()

    expect(rowOf(wrapper, 'Ana Lima').text()).toContain('Tornar administrador')
    expect(rowOf(wrapper, 'Ana Lima').text()).toContain('Remover')
    expect(rowOf(wrapper, 'João Souza').text()).toContain('Tornar membro')
    expect(rowOf(wrapper, 'João Souza').text()).toContain('Remover')
    const me = rowOf(wrapper, 'Maria Silva')
    expect(me.text()).toContain('Você')
    expect(me.text()).not.toContain('Remover')
    // Owner não sai (canLeave=false).
    expect(me.text()).not.toContain('Sair do grupo')
    wrapper.unmount()
  })

  it('Admin: não rebaixa nem remove outro Admin', async () => {
    const group = makeGroupDetail({
      myRole: 'Admin',
      members: [
        makeGroupMember({ id: 'm1', userId: 'u1', name: 'Maria Silva', role: 'Owner' }),
        makeGroupMember({ id: 'm2', userId: 'u2', name: 'João Souza', role: 'Admin', isMe: true }),
        makeGroupMember({ id: 'm4', userId: 'u4', name: 'Pedro Admin', role: 'Admin' }),
        makeGroupMember({ id: 'm3', userId: 'u3', name: 'Ana Lima', role: 'Member' }),
      ],
    })
    const { wrapper } = await mountTab(group)

    expect(rowOf(wrapper, 'Pedro Admin').findAll('button')).toHaveLength(0)
    expect(rowOf(wrapper, 'Ana Lima').text()).toContain('Tornar administrador')
    expect(rowOf(wrapper, 'Maria Silva').findAll('button')).toHaveLength(0)
    wrapper.unmount()
  })

  it('Member comum não vê nenhuma ação de gestão, só "Sair do grupo" na própria linha', async () => {
    const { wrapper } = await mountTab(makeMemberViewDetail())

    const labels = wrapper.findAll('button').map((b) => b.text())
    expect(labels).toEqual(['Sair do grupo'])
    wrapper.unmount()
  })

  it('"Sair do grupo" da linha emite `leave`', async () => {
    const { wrapper } = await mountTab(makeMemberViewDetail())
    await button(wrapper.element, 'Sair do grupo')!.click()
    expect(wrapper.emitted('leave')).toHaveLength(1)
    wrapper.unmount()
  })

  it('promover chama o endpoint de papel e pede recarga do grupo', async () => {
    mock.onPut('/groups/g1/members/m3/role').reply(200, makeGroupMember({ id: 'm3', role: 'Admin' }))
    const { wrapper } = await mountTab()

    await button(rowOf(wrapper, 'Ana Lima').element, 'Tornar administrador')!.click()
    await flushPromises()

    expect(JSON.parse(mock.history.put[0]?.data)).toEqual({ role: 'Admin' })
    expect(wrapper.emitted('changed')).toHaveLength(1)
    wrapper.unmount()
  })

  it('remover exige confirmação antes de chamar o backend', async () => {
    mock.onDelete('/groups/g1/members/m3').reply(204)
    const { wrapper } = await mountTab()

    await button(rowOf(wrapper, 'Ana Lima').element, 'Remover')!.click()
    await flushPromises()
    expect(mock.history.delete).toHaveLength(0)
    expect(document.body.textContent).toContain('Remover participante')
    expect(document.body.textContent).toContain('histórico de movimentações e pagamentos é mantido')

    document.body.querySelector('.confirm-dialog')!.dispatchEvent(new Event('submit'))
    await flushPromises()
    expect(mock.history.delete).toHaveLength(1)
    expect(wrapper.emitted('changed')).toHaveLength(1)
    wrapper.unmount()
  })

  it('erro 403/422 do backend aparece como aviso e recarrega o grupo', async () => {
    mock
      .onPut('/groups/g1/members/m3/role')
      .reply(403, { title: 'Sem permissão.', detail: 'Só o proprietário pode.', code: 'forbidden' })
    const { wrapper } = await mountTab()
    await button(rowOf(wrapper, 'Ana Lima').element, 'Tornar administrador')!.click()
    await flushPromises()

    expect(wrapper.emitted('changed')).toHaveLength(1)
    wrapper.unmount()
  })
})
