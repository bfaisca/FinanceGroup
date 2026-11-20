import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { makeCreditCard } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import CreditCardForm from '../components/CreditCardForm.vue'
import CreditCardFormDialog from '../components/CreditCardFormDialog.vue'

const toastAdd = vi.hoisted(() => vi.fn())
vi.mock('primevue/usetoast', () => ({ useToast: () => ({ add: toastAdd }) }))

const request = { name: 'Roxinho', institution: null, creditLimit: 5000, closingDay: 5, dueDay: 12, accountId: null }

describe('CreditCardFormDialog', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/accounts').reply(200, [])
    toastAdd.mockClear()
  })
  afterEach(() => mock.restore())

  function open(props: Record<string, unknown> = {}) {
    return mountWithPlugins(CreditCardFormDialog, { props: { visible: true, ...props }, pinia: createPinia() }).wrapper
  }

  it('cria o cartão (POST), recarrega a lista, fecha e avisa', async () => {
    mock.onPost('/credit-cards').reply(201, makeCreditCard())
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    const wrapper = open()
    await flushPromises()

    wrapper.findComponent(CreditCardForm).vm.$emit('submit', request)
    await flushPromises()

    expect(JSON.parse(mock.history.post[0]?.data as string)).toEqual(request)
    expect(wrapper.emitted('saved')).toHaveLength(1)
    expect(wrapper.emitted('update:visible')?.at(-1)).toEqual([false])
    expect(toastAdd).toHaveBeenCalledWith(expect.objectContaining({ summary: 'Cartão criado' }))
    wrapper.unmount()
  })

  it('edita o cartão com PUT', async () => {
    mock.onPut('/credit-cards/cc1').reply(200, makeCreditCard())
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    const wrapper = open({ card: makeCreditCard() })
    await flushPromises()
    wrapper.findComponent(CreditCardForm).vm.$emit('submit', request)
    await flushPromises()
    expect(mock.history.put).toHaveLength(1)
    expect(toastAdd).toHaveBeenCalledWith(expect.objectContaining({ summary: 'Cartão atualizado' }))
    wrapper.unmount()
  })

  it('400 vira erro de campo; 409 (nome duplicado) vira aviso do formulário', async () => {
    mock.onPost('/credit-cards').replyOnce(400, {
      title: 'Dados inválidos.',
      code: 'validation_error',
      status: 400,
      errors: { dueDay: ['Dia inválido.'] },
    })
    const wrapper = open()
    await flushPromises()
    wrapper.findComponent(CreditCardForm).vm.$emit('submit', request)
    await flushPromises()
    expect(document.body.querySelector('#card-due-error')?.textContent).toContain('Dia inválido.')
    expect(document.body.querySelector('[data-testid="form-error"]')).toBeNull()

    mock.onPost('/credit-cards').replyOnce(409, {
      title: 'Conflito.',
      detail: 'Já existe um cartão com este nome.',
      code: 'conflict',
      status: 409,
    })
    wrapper.findComponent(CreditCardForm).vm.$emit('submit', request)
    await flushPromises()
    expect(document.body.querySelector('[data-testid="form-error"]')?.textContent).toContain('Já existe um cartão')
    expect(wrapper.emitted('saved')).toBeUndefined()
    wrapper.unmount()
  })
})
