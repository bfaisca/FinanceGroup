import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { makeAccount, makeCategory, makeCreditCard, makeRecurrence } from '@/test/fixtures'
import { mountWithPlugins, useDesktopViewport } from '@/test/mount'
import RecurrencesPage from '../pages/RecurrencesPage.vue'

const toastAdd = vi.hoisted(() => vi.fn())
vi.mock('primevue/usetoast', () => ({ useToast: () => ({ add: toastAdd }) }))

async function mountAt(url = '/recurrences') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/recurrences', name: 'recurrences', component: RecurrencesPage },
      { path: '/transactions', name: 'transactions', component: { template: '<div />' } },
    ],
  })
  await router.push(url)
  await router.isReady()
  const { wrapper } = mountWithPlugins(RecurrencesPage, { pinia: createPinia(), router })
  await flushPromises()
  return { wrapper, router }
}

function button(root: Element | Document, label: string): HTMLButtonElement | undefined {
  return Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === label)
}

describe('RecurrencesPage', () => {
  let mock: MockAdapter
  let restore: (() => void) | null = null
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [makeCategory({ id: 'c-lazer', name: 'Lazer' })])
    mock.onGet('/accounts').reply(200, [makeAccount()])
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    toastAdd.mockClear()
  })
  afterEach(() => {
    mock.restore()
    restore?.()
    restore = null
  })

  it('mobile: cartões com valor com sinal, frequência legível, próxima ocorrência e geradas', async () => {
    mock.onGet('/recurrences').reply(200, [
      makeRecurrence(),
      makeRecurrence({
        id: 'r2',
        description: 'Academia',
        frequency: 'Weekly',
        interval: 2,
        creditCardName: null,
        nextOccurrenceDate: null,
        isActive: false,
      }),
    ])
    const { wrapper } = await mountAt()

    expect(mock.history.get.find((r) => r.url === '/recurrences')?.params).toEqual({ includeInactive: true })
    const cards = wrapper.findAll('.rec-card')
    // Inativas ficam ocultas por padrão.
    expect(cards).toHaveLength(1)
    expect(cards[0]!.text()).toContain('Netflix')
    expect(cards[0]!.text()).toContain('Mensal')
    expect(cards[0]!.text()).toMatch(/−\s*R\$\s*55,90/)
    expect(cards[0]!.text()).toContain('Próxima: 10/12/2026')
    expect(cards[0]!.text()).toContain('Geradas: 12')
    expect(cards[0]!.text()).toContain('Ativa')

    await wrapper.find('#show-inactive-rec').trigger('click')
    await flushPromises()
    const all = wrapper.findAll('.rec-card')
    expect(all).toHaveLength(2)
    expect(all[1]!.text()).toContain('A cada 2 semanas')
    expect(all[1]!.text()).toContain('Inativa')
    expect(all[1]!.text()).toContain('Próxima: —')
    wrapper.unmount()
  })

  it('desktop: tabela com cabeçalhos e ações com cliques reais (editar, ocorrências, desativar)', async () => {
    restore = useDesktopViewport()
    mock.onGet('/recurrences').reply(200, [makeRecurrence()])
    mock.onPost('/recurrences/r1/deactivate').reply(200, makeRecurrence({ isActive: false }))
    const { wrapper, router } = await mountAt()

    expect(wrapper.findAll('th').map((h) => h.text())).toEqual(
      expect.arrayContaining(['Descrição', 'Valor', 'Frequência', 'Próxima ocorrência', 'Geradas', 'Situação']),
    )

    // "Ver ocorrências" → /transactions?recurrenceRuleId=…
    await wrapper
      .findAll('tbody button')
      .find((b) => b.text().includes('Ocorrências'))!
      .trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('transactions')
    expect(router.currentRoute.value.query).toEqual({ recurrenceRuleId: 'r1' })
    wrapper.unmount()
  })

  it('desativar pede confirmação que explica as ocorrências (D-027) e chama POST /deactivate', async () => {
    mock.onGet('/recurrences').reply(200, [makeRecurrence()])
    mock.onPost('/recurrences/r1/deactivate').reply(200, makeRecurrence({ isActive: false }))
    const { wrapper } = await mountAt()

    await wrapper
      .findAll('button')
      .find((b) => b.text() === 'Desativar')!
      .trigger('click')
    await flushPromises()
    const text = document.body.textContent ?? ''
    expect(text).toContain('futuras sem pagamento e que nunca foram editadas manualmente serão removidas')
    expect(text).toContain('pagas, vencidas, canceladas ou editadas permanecem')
    expect(mock.history.post).toHaveLength(0)

    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(mock.history.post.map((r) => r.url)).toEqual(['/recurrences/r1/deactivate'])
    wrapper.unmount()
  })

  it('ativar pede confirmação e chama POST /activate', async () => {
    mock.onGet('/recurrences').reply(200, [makeRecurrence({ isActive: false })])
    mock.onPost('/recurrences/r1/activate').reply(200, makeRecurrence())
    const { wrapper } = await mountAt()
    await wrapper.find('#show-inactive-rec').trigger('click')

    await wrapper
      .findAll('button')
      .find((b) => b.text() === 'Ativar')!
      .trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('geradas novamente conforme a regra')
    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(mock.history.post.map((r) => r.url)).toEqual(['/recurrences/r1/activate'])
    wrapper.unmount()
  })

  it('estado vazio incentiva cadastrar e abre o diálogo de criação', async () => {
    mock.onGet('/recurrences').reply(200, [])
    const { wrapper } = await mountAt()
    expect(wrapper.text()).toContain('Nenhuma recorrência cadastrada')
    await wrapper
      .findAll('button')
      .filter((b) => b.text().includes('Nova recorrência'))[1]!
      .trigger('click')
    await flushPromises()
    expect(document.body.querySelector('form[aria-label="Nova recorrência"]')).not.toBeNull()
    wrapper.unmount()
  })

  it('mostra erro com "Tentar novamente"', async () => {
    mock.onGet('/recurrences').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountAt()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/recurrences').reply(200, [makeRecurrence()])
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Netflix')
    wrapper.unmount()
  })

  it('falha ao ativar/desativar mostra o `detail` do backend', async () => {
    mock.onGet('/recurrences').reply(200, [makeRecurrence()])
    mock.onPost('/recurrences/r1/deactivate').reply(422, {
      title: 'Regra de negócio violada.',
      detail: 'Não foi possível desativar.',
      code: 'business_rule_violation',
      status: 422,
    })
    const { wrapper } = await mountAt()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === 'Desativar')!
      .trigger('click')
    await flushPromises()
    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'error', detail: 'Não foi possível desativar.' }),
    )
    wrapper.unmount()
  })

  it('`?open=<id>` (link de uma ocorrência) abre a regra para edição e limpa o parâmetro', async () => {
    mock.onGet('/recurrences').reply(200, [makeRecurrence({ categoryId: 'c-lazer' })])
    const { wrapper, router } = await mountAt('/recurrences?open=r1')
    expect(document.body.querySelector('form[aria-label="Editar recorrência"]')).not.toBeNull()
    expect(router.currentRoute.value.query).toEqual({})
    expect(button(document, 'Salvar alterações')).toBeDefined()
    wrapper.unmount()
  })

  it('`?open=<id>` inexistente avisa em vez de falhar', async () => {
    mock.onGet('/recurrences').reply(200, [makeRecurrence()])
    const { wrapper } = await mountAt('/recurrences?open=nao-existe')
    expect(toastAdd).toHaveBeenCalledWith(expect.objectContaining({ summary: 'Recorrência não encontrada' }))
    wrapper.unmount()
  })
})
