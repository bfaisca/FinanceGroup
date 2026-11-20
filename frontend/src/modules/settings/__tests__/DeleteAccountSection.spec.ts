import { describe, expect, it } from 'vitest'
import { createPinia } from 'pinia'
import { mountWithPlugins } from '@/test/mount'
import DeleteAccountSection from '../components/DeleteAccountSection.vue'

describe('DeleteAccountSection (D-014)', () => {
  it('informa que os dados individuais são apagados definitivamente e o usuário vira "Usuário removido"', () => {
    const { wrapper } = mountWithPlugins(DeleteAccountSection, { pinia: createPinia() })
    const text = wrapper.text().replace(/\s+/g, ' ')
    expect(text).toContain('apagados definitivamente')
    expect(text).toContain('contas')
    expect(text).toContain('movimentações')
    expect(text).toContain('categorias personalizadas')
    expect(text).toContain('Usuário removido')
    wrapper.unmount()
  })

  it('menciona cartões, parcelamentos e recorrências entre os dados apagados (D-029)', () => {
    const { wrapper } = mountWithPlugins(DeleteAccountSection, { pinia: createPinia() })
    const text = wrapper.text().replace(/\s+/g, ' ')
    expect(text).toContain('cartões de crédito')
    expect(text).toContain('parcelamentos')
    expect(text).toContain('recorrências')
    wrapper.unmount()
  })

  it('mantém a confirmação por senha antes de excluir', async () => {
    const { wrapper } = mountWithPlugins(DeleteAccountSection, { pinia: createPinia() })
    await wrapper.find('button').trigger('click')
    expect(document.body.querySelector('input[type="password"]')).not.toBeNull()
    const submit = document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement
    expect(submit.disabled).toBe(true)
    wrapper.unmount()
  })
})
