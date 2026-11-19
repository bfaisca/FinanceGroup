import { describe, expect, it } from 'vitest'
import { createPinia } from 'pinia'
import { makeAccount } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import AccountCard from '../components/AccountCard.vue'
import AccountForm from '../components/AccountForm.vue'
import {
  buildCreateAccountRequest,
  buildSnapshotRequest,
  validateAccountForm,
  validateSnapshotForm,
  type AccountFormValues,
} from '../validation'

describe('AccountCard', () => {
  it('mostra saldo informado, projetado com selo e explicação', () => {
    const { wrapper } = mountWithPlugins(AccountCard, { props: { account: makeAccount() }, pinia: createPinia() })
    const text = wrapper.text()
    expect(text).toMatch(/Saldo informado/)
    expect(text).toMatch(/R\$\s1\.000,00/)
    expect(text).toContain('em 01/11/2026')
    expect(text).toContain('Projetado')
    expect(wrapper.find('[data-testid="projected-balance"]').text()).toMatch(/R\$\s850,50/)
    expect(text).toContain('Não é o saldo bancário')
    expect(text).toContain('18/11/2026')
    wrapper.unmount()
  })

  it('conta sem snapshot mostra o convite para informar saldo, sem valores', () => {
    const { wrapper } = mountWithPlugins(AccountCard, {
      props: { account: makeAccount({ latestSnapshot: null, projectedBalance: null }) },
      pinia: createPinia(),
    })
    expect(wrapper.find('[data-testid="no-snapshot"]').text()).toContain('Informe um saldo para ver a projeção')
    expect(wrapper.find('[data-testid="projected-balance"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('conta inativa mostra badge e oferece Reativar', async () => {
    const { wrapper } = mountWithPlugins(AccountCard, {
      props: { account: makeAccount({ isActive: false }) },
      pinia: createPinia(),
    })
    expect(wrapper.text()).toContain('Inativa')
    const toggle = wrapper.findAll('button').find((b) => b.text() === 'Reativar')!
    await toggle.trigger('click')
    expect(wrapper.emitted('toggle')).toHaveLength(1)
    wrapper.unmount()
  })
})

describe('AccountForm', () => {
  it('cria conta sem saldo inicial (sem snapshot)', async () => {
    const { wrapper } = mountWithPlugins(AccountForm, { props: { today: '2026-11-18' }, pinia: createPinia() })
    await wrapper.find('#acc-name').setValue(' Carteira ')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({ name: 'Carteira', institution: null, type: 'Checking' })
    wrapper.unmount()
  })

  it('envia saldo inicial (aceita negativo) com data opcional', async () => {
    const { wrapper } = mountWithPlugins(AccountForm, { props: { today: '2026-11-18' }, pinia: createPinia() })
    await wrapper.find('#acc-name').setValue('Conta')
    await wrapper.find('#acc-balance').setValue('-1.500,25')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      name: 'Conta',
      institution: null,
      type: 'Checking',
      initialBalance: -1500.25,
    })
    wrapper.unmount()
  })

  it('edição não mostra campos de saldo e não os envia', async () => {
    const { wrapper } = mountWithPlugins(AccountForm, {
      props: { today: '2026-11-18', account: makeAccount() },
      pinia: createPinia(),
    })
    expect(wrapper.find('#acc-balance').exists()).toBe(false)
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      name: 'Nubank',
      institution: 'Nu Pagamentos',
      type: 'Checking',
    })
    wrapper.unmount()
  })

  it('exibe conflito de nome vindo do backend', async () => {
    const { wrapper } = mountWithPlugins(AccountForm, { props: { today: '2026-11-18' }, pinia: createPinia() })
    await wrapper.setProps({ errorMessage: 'Já existe uma conta com este nome.' })
    expect(wrapper.find('[data-testid="form-error"]').text()).toContain('Já existe')
    wrapper.unmount()
  })
})

describe('validação de contas e saldos', () => {
  const base: AccountFormValues = {
    name: 'Conta',
    institution: '',
    type: 'Checking',
    initialBalance: '',
    balanceReferenceDate: null,
  }

  it('data de referência exige saldo e não pode ser futura', () => {
    const options = { today: '2026-11-18', creating: true }
    expect(validateAccountForm({ ...base, balanceReferenceDate: '2026-11-10' }, options).initialBalance).toBeTruthy()
    expect(
      validateAccountForm({ ...base, initialBalance: '10', balanceReferenceDate: '2026-11-19' }, options)
        .balanceReferenceDate,
    ).toMatch(/futura/)
    expect(validateAccountForm({ ...base, initialBalance: '10', balanceReferenceDate: '2026-11-18' }, options)).toEqual(
      {},
    )
  })

  it('limites de nome e instituição', () => {
    const options = { today: '2026-11-18', creating: true }
    expect(validateAccountForm({ ...base, name: '' }, options).name).toBeTruthy()
    expect(validateAccountForm({ ...base, name: 'x'.repeat(101) }, options).name).toMatch(/100/)
    expect(validateAccountForm({ ...base, institution: 'x'.repeat(101) }, options).institution).toMatch(/100/)
  })

  it('buildCreateAccountRequest só envia a data junto do saldo', () => {
    expect(buildCreateAccountRequest({ ...base, initialBalance: '10,5', balanceReferenceDate: '2026-11-01' })).toEqual({
      name: 'Conta',
      institution: null,
      type: 'Checking',
      initialBalance: 10.5,
      balanceReferenceDate: '2026-11-01',
    })
  })

  it('novo saldo: valor obrigatório (pode ser negativo) e data ≤ hoje', () => {
    const options = { today: '2026-11-18' }
    expect(validateSnapshotForm({ balance: '', referenceDate: '2026-11-18' }, options).balance).toBeTruthy()
    expect(validateSnapshotForm({ balance: '-20', referenceDate: '2026-11-18' }, options)).toEqual({})
    expect(validateSnapshotForm({ balance: '5', referenceDate: '2026-11-19' }, options).referenceDate).toMatch(/futura/)
    expect(buildSnapshotRequest({ balance: '-20,10', referenceDate: '2026-11-18' })).toEqual({
      balance: -20.1,
      referenceDate: '2026-11-18',
    })
  })
})
