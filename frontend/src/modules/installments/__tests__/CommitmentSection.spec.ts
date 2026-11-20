import { describe, expect, it } from 'vitest'
import { mountWithPlugins, ChartStub } from '@/test/mount'
import type { ReportInstallmentsDto } from '@/types/api'
import CommitmentSection from '../components/CommitmentSection.vue'

const report: ReportInstallmentsDto = {
  openPlans: 2,
  totalRemaining: 4500.5,
  commitmentByMonth: [
    { month: '2026-11', amount: 800, installmentsCount: 3 },
    { month: '2026-12', amount: 650.5, installmentsCount: 2 },
  ],
  plans: [],
}

function mountSection(value: ReportInstallmentsDto) {
  return mountWithPlugins(CommitmentSection, {
    props: { report: value },
    global: { stubs: { BaseChart: ChartStub } },
  }).wrapper
}

describe('CommitmentSection', () => {
  it('mostra total restante, nº de planos, gráfico com aria e tabela alternativa', () => {
    const wrapper = mountSection(report)
    expect(wrapper.text()).toMatch(/Total restante/)
    expect(wrapper.text()).toMatch(/R\$\s*4\.500,50/)
    expect(wrapper.find('[data-testid="open-plans"]').text()).toBe('2')
    const chart = wrapper.find('.chart-stub')
    expect(chart.attributes('aria-label')).toContain('novembro de 2026: R$')
    expect(chart.attributes('aria-label')).toContain('em 3 parcelas')

    const rows = wrapper.findAll('[data-testid="commitment-table"] tbody tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]!.text()).toContain('novembro de 2026')
    expect(rows[1]!.text()).toMatch(/R\$\s*650,50/)
    wrapper.unmount()
  })

  it('sem compromissos mostra a mensagem de vazio e não renderiza a tabela', () => {
    const wrapper = mountSection({
      ...report,
      openPlans: 0,
      totalRemaining: 0,
      commitmentByMonth: [{ month: '2026-11', amount: 0, installmentsCount: 0 }],
    })
    expect(wrapper.text()).toContain('Nenhum compromisso de parcelas')
    expect(wrapper.find('[data-testid="commitment-table"]').exists()).toBe(false)
    wrapper.unmount()
  })
})
