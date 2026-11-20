import { describe, expect, it } from 'vitest'
import { mountWithPlugins } from '@/test/mount'
import ReportState from '../components/ReportState.vue'

const slot = { default: '<p class="content">conteúdo</p>' }

describe('ReportState', () => {
  it('carregando sem dados mostra indicador e marca aria-busy', () => {
    const { wrapper } = mountWithPlugins(ReportState, {
      props: { loading: true, error: null, loaded: false },
      slots: slot,
    })
    expect(wrapper.find('[aria-busy="true"]').exists()).toBe(true)
    expect(wrapper.find('.content').exists()).toBe(false)
    wrapper.unmount()
  })

  it('erro mostra alerta e emite retry', async () => {
    const { wrapper } = mountWithPlugins(ReportState, {
      props: { loading: false, error: 'Falhou.', loaded: false },
      slots: slot,
    })
    expect(wrapper.find('[role="alert"]').text()).toContain('Falhou.')
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
    wrapper.unmount()
  })

  it('vazio mostra o estado vazio em vez do conteúdo', () => {
    const { wrapper } = mountWithPlugins(ReportState, {
      props: { loading: false, error: null, loaded: true, empty: true, emptyTitle: 'Nada aqui' },
      slots: slot,
    })
    expect(wrapper.text()).toContain('Nada aqui')
    expect(wrapper.find('.content').exists()).toBe(false)
    wrapper.unmount()
  })

  it('com dados mostra o conteúdo, mantendo-o durante recargas; erro tardio mantém conteúdo e avisa', () => {
    const { wrapper } = mountWithPlugins(ReportState, {
      props: { loading: true, error: null, loaded: true },
      slots: slot,
    })
    expect(wrapper.find('.content').exists()).toBe(true)
    wrapper.unmount()
    const again = mountWithPlugins(ReportState, { props: { loading: false, error: 'Erro', loaded: true }, slots: slot })
    expect(again.wrapper.find('.content').exists()).toBe(true)
    expect(again.wrapper.find('[role="alert"]').exists()).toBe(true)
    again.wrapper.unmount()
  })
})
