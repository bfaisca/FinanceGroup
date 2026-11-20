import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import GroupInvitationsTab from '../components/GroupInvitationsTab.vue'
import { makeGroupDetail, makeInvitation } from './fixtures'
import { button, mountAt } from './helpers'

const mountTab = () =>
  mountAt(
    GroupInvitationsTab,
    '/groups/g1',
    { path: '/groups/:id', name: 'group-detail' },
    {
      props: { group: makeGroupDetail() },
    },
  )

async function submitInvite(wrapper: Awaited<ReturnType<typeof mountTab>>['wrapper'], email: string) {
  await wrapper.find('#invite-email').setValue(email)
  await wrapper.find('form[aria-label="Convidar por e-mail"]').trigger('submit')
  await flushPromises()
}

describe('GroupInvitationsTab', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('lista convites com status e ações por status', async () => {
    mock
      .onGet('/groups/g1/invitations')
      .reply(200, [
        makeInvitation({ id: 'i1', email: 'pendente@x.com', status: 'Pending' }),
        makeInvitation({ id: 'i2', email: 'expirado@x.com', status: 'Expired' }),
        makeInvitation({ id: 'i3', email: 'aceito@x.com', status: 'Accepted' }),
        makeInvitation({ id: 'i4', email: 'revogado@x.com', status: 'Revoked' }),
      ])
    const { wrapper } = await mountTab()

    const items = wrapper.findAll('li')
    const of = (email: string) => items.find((li) => li.text().includes(email))!
    expect(of('pendente@x.com').text()).toContain('Pendente')
    expect(of('pendente@x.com').text()).toContain('Reenviar')
    expect(of('pendente@x.com').text()).toContain('Revogar')
    expect(of('expirado@x.com').text()).toContain('Expirado')
    expect(of('expirado@x.com').text()).toContain('Reenviar')
    expect(of('expirado@x.com').text()).not.toContain('Revogar')
    expect(of('aceito@x.com').findAll('button')).toHaveLength(0)
    expect(of('revogado@x.com').findAll('button')).toHaveLength(0)
    wrapper.unmount()
  })

  it('estado vazio e erro com nova tentativa', async () => {
    mock.onGet('/groups/g1/invitations').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountTab()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/groups/g1/invitations').reply(200, [])
    await button(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.text()).toContain('Nenhum convite enviado')
    wrapper.unmount()
  })

  it('cria convite com e-mail normalizado, limpa o campo e recarrega a lista', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [])
    mock.onPost('/groups/g1/invitations').reply(201, makeInvitation({ emailSent: true }))
    const { wrapper } = await mountTab()

    await submitInvite(wrapper, '  Convidado@Exemplo.com ')

    expect(JSON.parse(mock.history.post[0]?.data)).toEqual({ email: 'convidado@exemplo.com' })
    expect(mock.history.get.filter((r) => r.url === '/groups/g1/invitations')).toHaveLength(2)
    expect((wrapper.find('#invite-email').element as HTMLInputElement).value).toBe('')
    expect(wrapper.find('[data-testid="email-warning"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('valida o e-mail antes de enviar', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [])
    const { wrapper } = await mountTab()

    await submitInvite(wrapper, 'sem-arroba')

    expect(wrapper.text()).toContain('Informe um e-mail válido.')
    expect(mock.history.post).toHaveLength(0)
    wrapper.unmount()
  })

  it('avisa quando o convite foi criado mas o e-mail NÃO foi enviado (emailSent=false)', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [])
    mock.onPost('/groups/g1/invitations').reply(201, makeInvitation({ emailSent: false, email: 'a@b.com' }))
    const { wrapper } = await mountTab()

    await submitInvite(wrapper, 'a@b.com')

    const warning = wrapper.find('[data-testid="email-warning"]')
    expect(warning.exists()).toBe(true)
    expect(warning.text()).toContain('e-mail não pôde ser enviado')
    expect(warning.text()).toContain('Reenviar')
    wrapper.unmount()
  })

  it('409 mostra mensagem de convite pendente já existente', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [])
    mock.onPost('/groups/g1/invitations').reply(409, { title: 'Conflito', code: 'conflict' })
    const { wrapper } = await mountTab()

    await submitInvite(wrapper, 'a@b.com')

    expect(wrapper.find('[data-testid="invite-error"]').text()).toContain('Já existe um convite pendente')
    wrapper.unmount()
  })

  it('422 (e-mail já é membro) mostra a mensagem do backend', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [])
    mock.onPost('/groups/g1/invitations').reply(422, {
      title: 'Regra violada',
      detail: 'Esta pessoa já participa do grupo.',
      code: 'business_rule_violation',
    })
    const { wrapper } = await mountTab()

    await submitInvite(wrapper, 'a@b.com')

    expect(wrapper.find('[data-testid="invite-error"]').text()).toBe('Esta pessoa já participa do grupo.')
    wrapper.unmount()
  })

  it('reenviar chama o endpoint e avisa se o e-mail falhou', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [makeInvitation({ id: 'i1', email: 'a@b.com' })])
    mock
      .onPost('/groups/g1/invitations/i1/resend')
      .reply(200, makeInvitation({ id: 'i1', email: 'a@b.com', emailSent: false }))
    const { wrapper } = await mountTab()

    await button(wrapper.element, 'Reenviar')!.click()
    await flushPromises()

    expect(mock.history.post[0]?.url).toBe('/groups/g1/invitations/i1/resend')
    expect(wrapper.find('[data-testid="email-warning"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('revogar pede confirmação e só então chama o backend', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [makeInvitation({ id: 'i1', email: 'a@b.com' })])
    mock.onDelete('/groups/g1/invitations/i1').reply(204)
    const { wrapper } = await mountTab()

    await button(wrapper.element, 'Revogar')!.click()
    await flushPromises()
    expect(mock.history.delete).toHaveLength(0)
    expect(document.body.textContent).toContain('O link deixará de funcionar')

    document.body.querySelector('.confirm-dialog')!.dispatchEvent(new Event('submit'))
    await flushPromises()
    expect(mock.history.delete).toHaveLength(1)
    wrapper.unmount()
  })

  it('filtra por status enviando o parâmetro `status`', async () => {
    mock.onGet('/groups/g1/invitations').reply(200, [])
    const { wrapper } = await mountTab()

    const select = wrapper.findComponent({ name: 'Select' })
    select.vm.$emit('update:modelValue', 'Pending')
    await flushPromises()

    expect(mock.history.get.at(-1)?.params).toEqual({ status: 'Pending' })
    wrapper.unmount()
  })
})
