import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { nextTick, ref } from 'vue'
import { http } from '@/services/http'
import { useResource } from '../composables'

describe('useResource', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  const fetchItem = (id: string) => async () => (await http.get<{ id: string }>(`/x/${id}`)).data

  it('carrega ao iniciar, guarda o status do erro e permite recarregar', async () => {
    mock.onGet('/x/1').replyOnce(404, { title: 'Grupo não encontrado.', code: 'not_found' })
    const res = useResource(fetchItem('1'), () => '1')
    await flushPromises()
    expect(res.data.value).toBeNull()
    expect(res.error.value).toBe('Grupo não encontrado.')
    expect(res.errorStatus.value).toBe(404)

    mock.onGet('/x/1').reply(200, { id: '1' })
    await res.reload()
    expect(res.data.value).toEqual({ id: '1' })
    expect(res.error.value).toBeNull()
    expect(res.errorStatus.value).toBeNull()
    expect(res.loadedOnce.value).toBe(true)
  })

  it('descarta respostas fora de ordem', async () => {
    let releaseSlow: (v: [number, unknown]) => void = () => undefined
    mock.onGet('/x/slow').reply(() => new Promise((resolve) => (releaseSlow = resolve)))
    mock.onGet('/x/fast').reply(200, { id: 'fast' })

    const key = ref('slow')
    const res = useResource(
      async () => (await http.get<{ id: string }>(`/x/${key.value}`)).data,
      () => key.value,
    )
    await nextTick()
    key.value = 'fast'
    await flushPromises()
    releaseSlow([200, { id: 'slow' }])
    await flushPromises()

    expect(res.data.value).toEqual({ id: 'fast' })
  })

  it('clearOnKeyChange descarta o dado anterior ao trocar a chave; sem ele mantém', async () => {
    mock.onGet('/x/a').reply(200, { id: 'a' })
    mock.onGet('/x/b').reply(() => new Promise(() => undefined))

    for (const [clear, expected] of [
      [true, null],
      [false, { id: 'a' }],
    ] as const) {
      const key = ref('a')
      const res = useResource(
        async () => (await http.get<{ id: string }>(`/x/${key.value}`)).data,
        () => key.value,
        { clearOnKeyChange: clear },
      )
      await flushPromises()
      expect(res.data.value).toEqual({ id: 'a' })
      key.value = 'b'
      await nextTick()
      expect(res.data.value).toEqual(expected)
    }
  })

  it('chave nula não carrega nada', async () => {
    const res = useResource(fetchItem('1'), () => null)
    await flushPromises()
    expect(mock.history.get).toHaveLength(0)
    expect(res.loading.value).toBe(false)
  })
})
