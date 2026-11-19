import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { nextTick, ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import { makeTransaction } from '@/test/fixtures'
import { useTransactionList } from '../composables/useTransactionList'

describe('useTransactionList', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('carrega ao iniciar e recarrega quando a query muda', async () => {
    mock.onGet('/transactions').reply((config) => [
      200,
      {
        items: [makeTransaction({ description: `p${config.params.page}` })],
        page: config.params.page,
        pageSize: 20,
        totalCount: 40,
        totalPages: 2,
      },
    ])
    const page = ref(1)
    const list = useTransactionList(() => ({ page: page.value, pageSize: 20 }))
    await flushPromises()
    expect(list.items.value[0]?.description).toBe('p1')
    expect(list.totalPages.value).toBe(2)

    page.value = 2
    await nextTick()
    await flushPromises()
    expect(list.items.value[0]?.description).toBe('p2')
    expect(mock.history.get).toHaveLength(2)
  })

  it('descarta resposta obsoleta que chega depois da mais nova', async () => {
    const resolvers: Record<string, (value: [number, unknown]) => void> = {}
    mock.onGet('/transactions').reply(
      (config) =>
        new Promise<[number, unknown]>((resolve) => {
          resolvers[config.params.search] = resolve
        }),
    )
    const search = ref('a')
    const list = useTransactionList(() => ({ search: search.value }))
    await flushPromises()
    search.value = 'ab'
    await nextTick()
    await flushPromises()

    const body = (description: string): [number, unknown] => [
      200,
      { items: [makeTransaction({ description })], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
    ]
    resolvers.ab!(body('novo'))
    await flushPromises()
    resolvers.a!(body('antigo'))
    await flushPromises()

    expect(list.items.value[0]?.description).toBe('novo')
    expect(list.loading.value).toBe(false)
  })

  it('expõe a mensagem de erro e permite repetir', async () => {
    mock.onGet('/transactions').replyOnce(500, { title: 'Erro inesperado.', detail: 'Tente mais tarde.' })
    const list = useTransactionList(() => ({}))
    await flushPromises()
    expect(list.error.value).toBe('Tente mais tarde.')

    mock.onGet('/transactions').reply(200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
    await list.reload()
    expect(list.error.value).toBeNull()
    expect(list.loadedOnce.value).toBe(true)
  })
})
