import type { Component } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { createPinia, type Pinia } from 'pinia'
import { createMemoryHistory, createRouter, type RouteRecordRaw } from 'vue-router'
import { useClockStore } from '@/stores/clock'
import { ChartStub, mountWithPlugins } from '@/test/mount'

const Blank = { template: '<div />' }

/** Monta um componente/página de grupos em um router em memória, já posicionado em `url`. */
export async function mountAt(
  component: Component,
  url: string,
  route: { path: string; name: string },
  options: { props?: Record<string, unknown>; pinia?: Pinia; extraRoutes?: RouteRecordRaw[] } = {},
) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: route.path, name: route.name, component },
      { path: '/groups', name: 'groups', component: Blank },
      { path: '/groups/:id', name: 'group-detail', component: Blank },
      { path: '/login', name: 'login', component: Blank },
      ...(options.extraRoutes ?? []),
    ],
  })
  await router.push(url)
  await router.isReady()
  const pinia = options.pinia ?? createPinia()
  useClockStore(pinia).today = '2026-11-18'
  const mounted = mountWithPlugins(
    component as never,
    {
      pinia,
      router,
      props: options.props,
      global: { stubs: { BaseChart: ChartStub } },
    } as never,
  )
  await flushPromises()
  return { ...mounted, router, pinia }
}

/** Botão pelo texto exato, dentro de `root` (use `document.body` para diálogos/drawers teleportados). */
export function button(root: Element | Document, label: string | RegExp): HTMLButtonElement | undefined {
  return Array.from(root.querySelectorAll('button')).find((b) =>
    typeof label === 'string' ? b.textContent?.trim() === label : label.test(b.textContent ?? ''),
  )
}

export function buttonLabels(root: Element | Document): string[] {
  return Array.from(root.querySelectorAll('button')).map((b) => b.textContent?.trim() ?? '')
}
