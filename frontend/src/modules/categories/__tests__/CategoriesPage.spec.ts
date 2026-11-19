import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { makeCategory } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import CategoriesPage from '../pages/CategoriesPage.vue'

const categories = [
  makeCategory({ id: 'moradia', name: 'Moradia' }),
  makeCategory({ id: 'pets', name: 'Pets', isDefault: false }),
  makeCategory({ id: 'salario', name: 'Salário', type: 'Income' }),
]

async function mountPage() {
  const { wrapper } = mountWithPlugins(CategoriesPage, { pinia: createPinia() })
  await flushPromises()
  return wrapper
}

describe('CategoriesPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('começa por Despesas e alterna para Receitas', async () => {
    mock.onGet('/categories').reply(200, categories)
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('Moradia')
    expect(wrapper.text()).not.toContain('Salário')

    const incomes = wrapper.findAll('[role="radio"], .p-togglebutton').find((b) => b.text() === 'Receitas')!
    await incomes.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Salário')
    expect(wrapper.text()).not.toContain('Moradia')
    wrapper.unmount()
  })

  it('desativar avisa que subcategorias também serão desativadas e só então chama a API', async () => {
    mock.onGet('/categories').reply(200, categories)
    mock.onPost('/categories/pets/deactivate').reply(200, makeCategory({ id: 'pets', isActive: false }))
    const wrapper = await mountPage()

    await wrapper.find('[aria-label="Desativar Pets"]').trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('As subcategorias também serão desativadas')
    expect(mock.history.post).toHaveLength(0)

    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(mock.history.post.map((r) => r.url)).toEqual(['/categories/pets/deactivate'])
    wrapper.unmount()
  })

  it('mostra erro e estado vazio por tipo', async () => {
    mock.onGet('/categories').replyOnce(500, { title: 'Erro interno.' })
    const wrapper = await mountPage()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/categories').reply(200, [categories[0]])
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    await wrapper
      .findAll('[role="radio"], .p-togglebutton')
      .find((b) => b.text() === 'Receitas')!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Nenhuma categoria de receita')
    wrapper.unmount()
  })
})
