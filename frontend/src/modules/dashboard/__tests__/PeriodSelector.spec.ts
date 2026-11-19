import { describe, expect, it } from 'vitest'
import { createPinia } from 'pinia'
import { mountWithPlugins } from '@/test/mount'
import PeriodSelector from '../components/PeriodSelector.vue'

function setup(modelValue = { from: '2026-11-01', to: '2026-11-30' }) {
  return mountWithPlugins(PeriodSelector, { props: { modelValue, today: '2026-11-18' }, pinia: createPinia() }).wrapper
}

describe('PeriodSelector', () => {
  it('mostra o mês por extenso e navega para o anterior/próximo', async () => {
    const wrapper = setup()
    expect(wrapper.find('[data-testid="period-label"]').text()).toBe('novembro de 2026')

    await wrapper.find('button[aria-label="Mês anterior"]').trigger('click')
    await wrapper.find('button[aria-label="Próximo mês"]').trigger('click')
    expect(wrapper.emitted('update:modelValue')).toEqual([
      [{ from: '2026-10-01', to: '2026-10-31' }],
      [{ from: '2026-12-01', to: '2026-12-31' }],
    ])
    wrapper.unmount()
  })

  it('"Mês atual" volta ao mês do hoje e fica desativado quando já é o atual', async () => {
    const other = setup({ from: '2026-09-01', to: '2026-09-30' })
    await other
      .findAll('button')
      .find((b) => b.text() === 'Mês atual')!
      .trigger('click')
    expect(other.emitted('update:modelValue')?.[0]).toEqual([{ from: '2026-11-01', to: '2026-11-30' }])
    other.unmount()

    const current = setup()
    expect(
      (current.findAll('button').find((b) => b.text() === 'Mês atual')!.element as HTMLButtonElement).disabled,
    ).toBe(true)
    current.unmount()
  })

  it('período personalizado: exibe intervalo e desativa navegação mensal', () => {
    const wrapper = setup({ from: '2026-11-05', to: '2026-11-20' })
    expect(wrapper.find('[data-testid="period-label"]').text()).toBe('05/11/2026 a 20/11/2026')
    expect((wrapper.find('button[aria-label="Mês anterior"]').element as HTMLButtonElement).disabled).toBe(true)
    wrapper.unmount()
  })

  it('personalizado: aplica um intervalo válido', async () => {
    const wrapper = setup()
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Personalizado'))!
      .trigger('click')
    await wrapper.find('#period-from').setValue('01/11/2026')
    await wrapper.find('#period-to').setValue('15/11/2026')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([{ from: '2026-11-01', to: '2026-11-15' }])
    wrapper.unmount()
  })

  it('personalizado: recusa intervalo maior que o limite do contrato (366 dias)', async () => {
    const wrapper = setup()
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Personalizado'))!
      .trigger('click')
    await wrapper.find('#period-from').setValue('01/01/2024')
    await wrapper.find('#period-to').setValue('01/01/2026')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect(wrapper.find('#period-from-error').text()).toContain('366')
    wrapper.unmount()
  })
})
