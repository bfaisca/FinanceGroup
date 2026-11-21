import { inject, onBeforeUnmount, provide, type InjectionKey } from 'vue'

/** Função de recarga de uma aba; devolve `true` se os dados foram atualizados sem erro. */
export type RefreshHandler = () => Promise<boolean>

interface GroupRefreshRegistry {
  register: (handler: RefreshHandler) => () => void
}

const KEY: InjectionKey<GroupRefreshRegistry> = Symbol('group-refresh')

/**
 * Página do grupo (D-050): mantém os recarregadores registrados pelas abas montadas e os executa
 * juntos quando o usuário aciona "Atualizar". Retorna `true` se todos terminaram sem erro.
 */
export function provideGroupRefresh(): { runAll: () => Promise<boolean> } {
  const handlers = new Set<RefreshHandler>()
  provide(KEY, {
    register(handler) {
      handlers.add(handler)
      return () => handlers.delete(handler)
    },
  })
  return {
    async runAll() {
      const results = await Promise.all(Array.from(handlers).map((handler) => handler().catch(() => false)))
      return results.every(Boolean)
    },
  }
}

/** Aba/seção: registra como recarregar seus dados quando o botão "Atualizar" da página for acionado. */
export function useGroupRefreshHandler(handler: RefreshHandler): void {
  const registry = inject(KEY, null)
  if (!registry) return
  const unregister = registry.register(handler)
  onBeforeUnmount(unregister)
}
