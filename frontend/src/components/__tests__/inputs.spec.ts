import { describe, expect, it } from 'vitest'
import { defineComponent, ref } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import DateInput from '../DateInput.vue'
import MoneyInput from '../MoneyInput.vue'

const plugins = [[PrimeVue, { theme: 'none' }]] as never

describe('MoneyInput', () => {
  it('reformata ao sair do campo e preserva texto inválido', async () => {
    const Host = defineComponent({
      components: { MoneyInput },
      setup: () => ({ value: ref('') }),
      template: '<MoneyInput id="m" v-model="value" /><output>{{ value }}</output>',
    })
    const wrapper = mount(Host, { global: { plugins } })
    const input = wrapper.find('#m')

    await input.setValue('1234,5')
    await input.trigger('blur')
    expect((input.element as HTMLInputElement).value).toBe('1.234,50')

    await input.setValue('abc')
    await input.trigger('blur')
    expect((input.element as HTMLInputElement).value).toBe('abc')
  })

  it('usa teclado numérico decimal e prefixo R$ oculto para leitores de tela', () => {
    const wrapper = mount(MoneyInput, { props: { id: 'm', modelValue: '' }, global: { plugins } })
    expect(wrapper.find('#m').attributes('inputmode')).toBe('decimal')
    expect(wrapper.find('[aria-hidden="true"]').text()).toBe('R$')
  })
})

describe('DateInput', () => {
  it('exibe a data DateOnly sem deslocar o dia (sem passar por UTC)', () => {
    const wrapper = mount(DateInput, { props: { id: 'd', modelValue: '2026-11-01' }, global: { plugins } })
    expect((wrapper.find('#d').element as HTMLInputElement).value).toBe('01/11/2026')
  })

  it('aceita modelo vazio', () => {
    const wrapper = mount(DateInput, { props: { id: 'd', modelValue: null }, global: { plugins } })
    expect((wrapper.find('#d').element as HTMLInputElement).value).toBe('')
  })
})
