import { describe, expect, it } from 'vitest'
import { mountWithPlugins } from '@/test/mount'
import LimitBar from '../components/LimitBar.vue'

function mountBar(props: { usedLimit: number; creditLimit: number; availableLimit: number }) {
  return mountWithPlugins(LimitBar, { props }).wrapper
}

describe('LimitBar', () => {
  it('mostra utilizado × limite com valores, percentual e disponível', () => {
    const wrapper = mountBar({ usedLimit: 1250, creditLimit: 5000, availableLimit: 3750 })
    const text = wrapper.text()
    expect(text).toMatch(/Utilizado\s*R\$\s*1\.250,00\s*de\s*R\$\s*5\.000,00\s*\(25,0%\)/)
    expect(wrapper.find('[data-testid="limit-available"]').text()).toMatch(/Disponível\s*R\$\s*3\.750,00/)
    expect(wrapper.find('[data-testid="limit-exceeded"]').exists()).toBe(false)
    const bar = wrapper.find('[role="progressbar"]')
    expect(bar.attributes('aria-valuenow')).toBe('25')
    expect(bar.attributes('aria-valuetext')).toContain('25,0%')
    wrapper.unmount()
  })

  it('limite excedido é comunicado por texto e ícone (não só por cor)', () => {
    const wrapper = mountBar({ usedLimit: 6000, creditLimit: 5000, availableLimit: -1000 })
    const alert = wrapper.find('[data-testid="limit-exceeded"]')
    expect(alert.text()).toMatch(/Limite excedido em R\$\s*1\.000,00/)
    expect(alert.find('i.pi-exclamation-triangle').exists()).toBe(true)
    expect(wrapper.find('[data-testid="limit-available"]').exists()).toBe(false)
    expect(wrapper.find('[role="progressbar"]').attributes('aria-valuenow')).toBe('100')
    expect(wrapper.classes()).toContain('limit-bar--exceeded')
    wrapper.unmount()
  })

  it('limite zero não mostra percentual', () => {
    const wrapper = mountBar({ usedLimit: 0, creditLimit: 0, availableLimit: 0 })
    expect(wrapper.find('.limit-bar__summary').text()).not.toContain('%')
    wrapper.unmount()
  })
})
