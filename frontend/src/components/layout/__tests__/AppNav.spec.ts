import { describe, expect, it } from 'vitest'
import { mountWithPlugins } from '@/test/mount'
import { NAV_ITEMS } from '@/layouts/navigation'
import AppNav from '../AppNav.vue'

async function activeLabels(path: string): Promise<string[]> {
  const { wrapper, router } = mountWithPlugins(AppNav, {
    props: { items: NAV_ITEMS.filter((i) => i.routeName !== 'settings') },
  })
  await router.push(path)
  await router.isReady()
  await wrapper.vm.$nextTick()
  const labels = wrapper.findAll('.app-nav__link--active').map((l) => l.text())
  wrapper.unmount()
  return labels
}

describe('AppNav', () => {
  it('destaca apenas "Dashboard" na própria rota', async () => {
    expect(await activeLabels('/dashboard')).toEqual(['Dashboard'])
  })

  it('mantém "Grupos" ativo no detalhe de um grupo e não destaca o Dashboard', async () => {
    expect(await activeLabels('/groups/abc')).toEqual(['Grupos'])
  })

  it('destaca a página atual em rotas de lista', async () => {
    expect(await activeLabels('/groups')).toEqual(['Grupos'])
  })
})
