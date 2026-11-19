import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import TransactionAmount from '../components/TransactionAmount.vue'

describe('TransactionAmount', () => {
  it('despesa: sinal de menos, ícone e texto para leitor de tela', () => {
    const wrapper = mount(TransactionAmount, { props: { amount: 1234.5, type: 'Expense' } })
    expect(wrapper.text()).toMatch(/Despesa:\s*−\s*R\$\s*1\.234,50/)
    expect(wrapper.find('.sr-only').text()).toContain('Despesa')
    expect(wrapper.classes()).toContain('money--expense')
  })

  it('receita: sinal de mais', () => {
    const wrapper = mount(TransactionAmount, { props: { amount: 10, type: 'Income' } })
    expect(wrapper.text()).toMatch(/Receita:\s*\+\s*R\$\s*10,00/)
    expect(wrapper.classes()).toContain('money--income')
  })
})
